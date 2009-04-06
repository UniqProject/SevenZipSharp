﻿/*  This file is part of SevenZipSharp.

    SevenZipSharp is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    SevenZipSharp is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with SevenZipSharp.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Diagnostics;
using SevenZip.ComRoutines;
using SevenZip.Sdk;

namespace SevenZip
{
    /// <summary>
    /// Callback to handle the archive opening
    /// </summary>
    internal sealed class ArchiveOpenCallback : SevenZipBase, IArchiveOpenCallback, ICryptoGetTextPassword
    {
        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class
        /// </summary>
        public ArchiveOpenCallback() : base() { }
        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class
        /// </summary>
        /// <param name="password">Password for the archive</param>
        public ArchiveOpenCallback(string password) : base(password) { }

        #region ICryptoGetTextPassword Members
        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        #endregion

        #region IArchiveOpenCallback Members

        public void SetTotal(IntPtr files, IntPtr bytes) { }

        public void SetCompleted(IntPtr files, IntPtr bytes) { }

        #endregion
    }

    /// <summary>
    /// Archive extraction callback to handle the process of unpacking files
    /// </summary>
    internal sealed class ArchiveExtractCallback : SevenZipBase, IArchiveExtractCallback, ICryptoGetTextPassword, IDisposable
    {
        private OutStreamWrapper _FileStream;
        private FakeOutStreamWrapper _FakeStream;
        private IInArchive _Archive;
        private string _Directory;
        private uint? _FileIndex;
        private int _FilesCount;
        private List<uint> _ActualIndexes;
        /// <summary>
        /// For Compressing event
        /// </summary>
        private long _BytesCount;
        private long _BytesWritten;
        private long _BytesWrittenOld;
        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _DoneRate;
        private SevenZipExtractor _Extractor;

        #region Events
        /// <summary>
        /// Occurs when a new file is going to be unpacked
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an output stream for a new file to unpack in</remarks>
        public event EventHandler<IndexEventArgs> FileExtractionStarted;
        /// <summary>
        /// Occurs when a file has been successfully unpacked
        /// </summary>
        public event EventHandler FileExtractionFinished;
        /// <summary>
        /// Occurs when the archive is opened and 7-zip sends the size of unpacked data
        /// </summary>
        public event EventHandler<OpenEventArgs> Open;
        /// <summary>
        /// Occurs when the extraction is performed
        /// </summary>
        public event EventHandler<ProgressEventArgs> Extracting;
        /// <summary>
        /// Occurs during the extraction when a file already exists
        /// </summary>
        public event EventHandler<FileNameEventArgs> FileExists;

        private void OnFileExists(FileNameEventArgs e)
        {
            if (FileExists != null)
            {
                FileExists(this, e);
            }
        }

        private void OnOpen(OpenEventArgs e)
        {
            if (Open != null)
            {
                Open(this, e);
            }
        }

        private void OnFileExtractionStarted(IndexEventArgs e)
        {
            if (FileExtractionStarted != null)
            {
                FileExtractionStarted(this, e);
            }
        }

        private void OnFileExtractionFinished(EventArgs e)
        {
            if (FileExtractionFinished != null)
            {
                FileExtractionFinished(this, e);
            }
        }

        private void OnExtracting(ProgressEventArgs e)
        {
            if (Extracting != null)
            {
                Extracting(this, e);
            }
        }
        #endregion

        /// <summary>
        /// Ensures that the directory to the file name is valid and creates intermediate directories if necessary
        /// </summary>
        /// <param name="fileName">File name</param>
        private static void ValidateFileName(string fileName)
        {
            List<string> splittedFileName = new List<string>(fileName.Split('\\'));
            if (fileName.StartsWith(@"\\", StringComparison.CurrentCultureIgnoreCase))
            {
                splittedFileName.RemoveAt(0);
                splittedFileName.RemoveAt(0);
                splittedFileName[0] = @"\\" + splittedFileName[0];
            }

            if (splittedFileName.Count > 2)
            {
                string tfn = splittedFileName[0];
                for (int i = 1; i < splittedFileName.Count - 1; i++)
                {
                    tfn += '\\' + splittedFileName[i];
                    if (!Directory.Exists(tfn))
                    {
                        Directory.CreateDirectory(tfn);
                    }
                }
            }
        }

        private void Init(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes, SevenZipExtractor extractor)
        {
            _Archive = archive;
            _Directory = directory;
            _FilesCount = filesCount;
            _ActualIndexes = actualIndexes;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (!directory.EndsWith("\\", StringComparison.CurrentCulture))
            {
                _Directory += '\\';
            }
            _FakeStream = new FakeOutStreamWrapper();
            _FakeStream.BytesWritten += new EventHandler<IntEventArgs>(IntEventArgsHandler);
            _Extractor = extractor;
        }

        private void Init(IInArchive archive, Stream stream, int filesCount, uint fileIndex, SevenZipExtractor extractor)
        {
            _Archive = archive;
            _FileStream = new OutStreamWrapper(stream, false);
            _FileStream.BytesWritten += new EventHandler<IntEventArgs>(IntEventArgsHandler);
            _FilesCount = filesCount;
            _FileIndex = fileIndex;
            _FakeStream = new FakeOutStreamWrapper();
            _FakeStream.BytesWritten += new EventHandler<IntEventArgs>(IntEventArgsHandler);
            _Extractor = extractor;
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>'
        /// <param name="extractor">The owner of the callback</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes, SevenZipExtractor extractor)
            : base()
        {
            Init(archive, directory, filesCount, actualIndexes, extractor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="password">Password for the archive</param>
        /// <param name="extractor">The owner of the callback</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes, string password, SevenZipExtractor extractor)
            : base(password)
        {
            Init(archive, directory, filesCount, actualIndexes, extractor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="stream">The stream where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="fileIndex">The file index for the stream</param>
        /// <param name="extractor">The owner of the callback</param>
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex, SevenZipExtractor extractor)
            : base()
        {
            Init(archive, stream, filesCount, fileIndex, extractor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="stream">The stream where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>
        /// <param name="fileIndex">The file index for the stream</param>
        /// <param name="password">Password for the archive</param>
        /// <param name="extractor">The owner of the callback</param>
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex, string password, SevenZipExtractor extractor)
            : base(password)
        {
            Init(archive, stream, filesCount, fileIndex, extractor);
        }
        #region IArchiveExtractCallback Members
        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        public void SetTotal(ulong total)
        {
            _BytesCount = (long)total;
            OnOpen(new OpenEventArgs(total));
        }

        public void SetCompleted(ref ulong completeValue) { }

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            byte pold = (byte)((_BytesWrittenOld * 100) / _BytesCount);
            _BytesWritten += e.Value;
            byte pnow = (byte)((_BytesWritten * 100) / _BytesCount);
            if (pnow > pold)
            {
                _BytesWrittenOld = _BytesWritten;
                OnExtracting(new ProgressEventArgs(pnow, (byte)(pnow - pold)));
            }
        }

        /// <summary>
        /// Sets output stream for writing unpacked data
        /// </summary>
        /// <param name="index">Current file index</param>
        /// <param name="outStream">Output stream pointer</param>
        /// <param name="askExtractMode">Extraction mode</param>
        /// <returns>0 if OK</returns>
        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;
            if (askExtractMode == AskMode.Extract)
            {
                string fileName = _Directory;
                for (int i = 0; i < 1; i++)
                {
                    if (!_FileIndex.HasValue)
                    {
                        #region Extraction of a file
                        if (_ActualIndexes == null || _ActualIndexes.Contains(index))
                        {
                            fileName = _Directory;
                            PropVariant Data = new PropVariant();
                            _Archive.GetProperty(index, ItemPropId.Path, ref Data);
                            fileName += (string)Data.Object;
                            _Archive.GetProperty(index, ItemPropId.IsFolder, ref Data);
                            ValidateFileName(fileName);
                            if (!NativeMethods.SafeCast<bool>(Data.Object, false))
                            {
                                _Archive.GetProperty(index, ItemPropId.LastWriteTime, ref Data);
                                DateTime time = NativeMethods.SafeCast<DateTime>(Data.Object, DateTime.Now);
                                if (File.Exists(fileName))
                                {
                                    FileNameEventArgs fnea = new FileNameEventArgs(fileName);
                                    OnFileExists(fnea);
                                    if (!fnea.Overwrite)
                                    {
                                        outStream = _FakeStream;
                                        break;
                                    }
                                }
                                try
                                {
                                    _FileStream = new OutStreamWrapper(File.Create(fileName), fileName, time, true);
                                }
                                catch (FileNotFoundException)
                                {
                                    Trace.WriteLine("The file \"" + fileName + "\" was not extracted due to the File.Create fail.");
                                    outStream = _FakeStream;
                                    break;
                                }
                                _FileStream.BytesWritten += new EventHandler<IntEventArgs>(IntEventArgsHandler);
                                outStream = _FileStream;
                            }
                            else
                            {
                                if (!Directory.Exists(fileName))
                                {
                                    Directory.CreateDirectory(fileName);
                                    outStream = _FakeStream;
                                }
                            }
                        }
                        else
                        {
                            outStream = _FakeStream;
                        }
                        #endregion
                    }
                    else
                    {
                        #region Extraction of a stream
                        if (index == _FileIndex)
                        {
                            outStream = _FileStream;
                        }
                        else
                        {
                            outStream = _FakeStream;
                        }
                        #endregion
                    }
                }
                _DoneRate += 1.0f / _FilesCount;
                IndexEventArgs iea = new IndexEventArgs(index, PercentDoneEventArgs.ProducePercentDone(_DoneRate));
                OnFileExtractionStarted(iea);
                if (iea.Cancel)
                {
                    if (!String.IsNullOrEmpty(fileName))
                    {
                        _FileStream.Dispose();
                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }
                    }
                    _Extractor.Cancelled = true;                    
                    return -1;
                }
            }
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode) { }
        /// <summary>
        /// Called when the archive was extracted
        /// </summary>
        /// <param name="operationResult"></param>
        public void SetOperationResult(OperationResult operationResult)
        {
            if (operationResult != OperationResult.Ok && ReportErrors)
            {
                switch (operationResult)
                {
                    case OperationResult.CrcError:
                        throw new ExtractionFailedException("File is corrupted. Crc check has failed.");
                    case OperationResult.DataError:
                        throw new ExtractionFailedException("File is corrupted. Data error has occured.");
                    case OperationResult.UnsupportedMethod:
                        throw new ExtractionFailedException("Unsupported method error has occured.");
                }
            }
            else
            {
                if (_FileStream != null)
                {
                    _FileStream.Dispose();
                }
                OnFileExtractionFinished(EventArgs.Empty);
            }
        }

        #endregion

        #region ICryptoGetTextPassword Members
        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_FileStream != null)
            {
                _FileStream.Dispose();
            }
            if (_FakeStream != null)
            {
                _FakeStream.Dispose();
            }
        }

        #endregion
    }

    /// <summary>
    /// Archive update callback to handle the process of packing files
    /// </summary>
    internal sealed class ArchiveUpdateCallback : SevenZipBase, IArchiveUpdateCallback, ICryptoGetTextPassword2, IDisposable
    {
        private InStreamWrapper _FileStream;
        /// <summary>
        /// Array of files to pack
        /// </summary>
        private FileInfo[] _Files;
        /// <summary>
        /// The names of the archive entries
        /// </summary>
        private string[] _Entries;
        /// <summary>
        /// Input streams
        /// </summary>
        private Stream[] _Streams; 
        /// <summary>
        /// _Files.Count if do not count directories
        /// </summary>
        private int _ActualFilesCount;
        /// <summary>
        /// Common file names root length
        /// </summary>
        private int _RootLength;
        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _DoneRate;
        /// <summary>
        /// For Compressing event
        /// </summary>
        private long _BytesCount;
        private long _BytesWritten;
        private long _BytesWrittenOld;
        private SevenZipCompressor _Compressor;

        private void Init(FileInfo[] files, int rootLength, SevenZipCompressor compressor)
        {
            _Files = files;
            _RootLength = rootLength;
            foreach (FileInfo fi in files)
            {
                if (fi.Exists)
                {
                    _BytesCount += fi.Length;
                    if ((fi.Attributes & FileAttributes.Directory) == 0)
                    {
                        _ActualFilesCount++;
                    }
                }
            }
            _Compressor = compressor;
        }

        private void Init(Stream stream, SevenZipCompressor compressor)
        {            
            _FileStream = new InStreamWrapper(stream, false);
            _FileStream.BytesRead += new EventHandler<IntEventArgs>(IntEventArgsHandler);
            _ActualFilesCount = 1;
            try
            {
                _BytesCount = stream.Length;
            }
            catch (NotSupportedException)
            {
                _BytesCount = -1;
            }
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (NotSupportedException)
            {
                _BytesCount = -1;
            }
            _Compressor = compressor;
        }

        private void Init(Dictionary<Stream, string> streamDict, SevenZipCompressor compressor)
        {
            _Streams = new Stream[streamDict.Count];
            streamDict.Keys.CopyTo(_Streams, 0);
            _Entries = new string[streamDict.Count];
            streamDict.Values.CopyTo(_Entries, 0);
            _ActualFilesCount = streamDict.Count;
            foreach (Stream str in _Streams)
            {
                _BytesCount += str.Length;
            }
            _Compressor = compressor;
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(FileInfo[] files, int rootLength, SevenZipCompressor compressor)
            : base()
        {
            Init(files, rootLength, compressor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(FileInfo[] files, int rootLength, string password, SevenZipCompressor compressor)
            : base(password)
        {
            Init(files, rootLength, compressor);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(Stream stream, SevenZipCompressor compressor)
            : base()
        {
            Init(stream, compressor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(Stream stream, string password, SevenZipCompressor compressor)
            : base(password)
        {
            Init(stream, compressor);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="streamDict">Dictionary&lt;file stream, name of the archive entry&gt;</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(Dictionary<Stream, string> streamDict, SevenZipCompressor compressor)
            : base()
        {
            Init(streamDict, compressor);
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="streamDict">Dictionary&lt;file stream, name of the archive entry&gt;</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        public ArchiveUpdateCallback(Dictionary<Stream, string> streamDict, string password, SevenZipCompressor compressor)
            : base(password)
        {
            Init(streamDict, compressor);
        }
        /// <summary>
        /// Occurs when the next file is going to be packed
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an input stream for the next file to pack it</remarks>
        public event EventHandler<FileInfoEventArgs> FileCompressionStarted;
        /// <summary>
        /// Occurs when data are being compressed
        /// </summary>
        public event EventHandler<ProgressEventArgs> Compressing;

        private void OnFileCompression(FileInfoEventArgs e)
        {
            if (FileCompressionStarted != null)
            {
                FileCompressionStarted(this, e);
            }
        }

        private void OnCompressing(ProgressEventArgs e)
        {
            if (Compressing != null)
            {
                Compressing(this, e);
            }
        }

        #region IArchiveUpdateCallback Members

        public void SetTotal(ulong total) { }

        public void SetCompleted(ref ulong completeValue) { }

        public int GetUpdateItemInfo(uint index, ref int newData, ref int newProperties, ref uint indexInArchive)
        {
            newData = 1;
            newProperties = 1;
            indexInArchive = UInt32.MaxValue;
            return 0;
        }

        public int GetProperty(uint index, ItemPropId propID, ref PropVariant value)
        {
            switch (propID)
            {
                case ItemPropId.IsAnti:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = 0;
                    break;
                case ItemPropId.Path:
                    value.VarType = VarEnum.VT_BSTR;
                    string val = "default";
                    if (_Files == null)
                    {
                        if (_Entries != null)
                        {
                            val = _Entries[index];
                        }
                    }
                    else
                    {
                        if (_RootLength > 0)
                        {
                            val = _Files[index].FullName.Substring(_RootLength);
                        }
                        else
                        {
                            val = _Files[index].FullName[0] + _Files[index].FullName.Substring(2);
                        }
                    }
                    value.Value = Marshal.StringToBSTR(val);
                    break;
                case ItemPropId.IsFolder:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = _Files == null ? 
                        (ulong)0 : (byte)(_Files[index].Attributes & FileAttributes.Directory);
                    break;
                case ItemPropId.Size:
                    value.VarType = VarEnum.VT_UI8;
                    UInt64 size = 0;
                    if (_Files == null)
                    {
                        if (_Streams == null)
                        {
                            size = _BytesCount > 0 ? (ulong)_BytesCount : 0;
                        }
                        else
                        {
                            size = (ulong)_Streams[index].Length;
                        }
                    }
                    else
                    {
                        size = (_Files[index].Attributes & FileAttributes.Directory) == 0 ?
                        (ulong)_Files[index].Length : 0;
                    }
                    value.UInt64Value = size;                 
                    break;
                case ItemPropId.Attributes:
                    value.VarType = VarEnum.VT_UI4;
                    value.UInt32Value = _Files == null ? 
                        32 : (uint)_Files[index].Attributes;
                    break;
                case ItemPropId.CreationTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files == null ?
                        0 : _Files[index].CreationTime.ToFileTime();
                    break;
                case ItemPropId.LastAccessTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files == null ?
                        0 : _Files[index].LastAccessTime.ToFileTime();
                    break;
                case ItemPropId.LastWriteTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files == null ?
                        0 : _Files[index].LastWriteTime.ToFileTime();
                    break;
                case ItemPropId.Extension:
                    value.VarType = VarEnum.VT_BSTR;
                    try
                    {
                        val = _Files != null ? _Files[index].Extension.Substring(1) :
                                     _Entries == null ? "" :
                                     Path.GetExtension(_Entries[index]);
                        value.Value = Marshal.StringToBSTR(val);
                    }
                    catch (ArgumentException)
                    {
                        value.Value = Marshal.StringToBSTR("");
                    }
                    break;
            }
            return 0;
        }

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            if (_BytesCount > -1)
            {
                byte pold = (byte)((_BytesWrittenOld * 100) / _BytesCount);
                _BytesWritten += e.Value;
                byte pnow = (byte)((_BytesWritten * 100) / _BytesCount);
                if (pnow > pold)
                {
                    _BytesWrittenOld = _BytesWritten;
                    OnCompressing(new ProgressEventArgs(pnow, (byte)(pnow - pold)));
                }
            }
            else
            {
                OnCompressing(new ProgressEventArgs(0, 0));
            }
        }

        /// <summary>
        /// Gets the stream for 7-zip library
        /// </summary>
        /// <param name="index">File index</param>
        /// <param name="inStream">Input file stream</param>
        /// <returns>Zero if Ok</returns>
        public int GetStream(uint index, out ISequentialInStream inStream)
        {
            if (_Files != null)
            {
                if ((_Files[index].Attributes & FileAttributes.Directory) == 0)
                {
                    _FileStream = new InStreamWrapper(File.OpenRead(_Files[index].FullName), true);
                    _FileStream.BytesRead += new EventHandler<IntEventArgs>(IntEventArgsHandler);
                    inStream = _FileStream;
                }
                else
                {
                    inStream = null;
                }
                _DoneRate += 1.0f / _ActualFilesCount;
                FileInfoEventArgs fiea = new FileInfoEventArgs(_Files[index], _Files[index].Name,PercentDoneEventArgs.ProducePercentDone(_DoneRate));
                OnFileCompression(fiea);
                if (fiea.Cancel)
                {
                    _Compressor.Cancelled = true;
                    return -1;
                }
            }
            else
            {
                if (_Streams == null)
                {
                    inStream = _FileStream;
                }
                else
                {
                    _FileStream = new InStreamWrapper(_Streams[index], true);
                    _FileStream.BytesRead += new EventHandler<IntEventArgs>(IntEventArgsHandler);
                    inStream = _FileStream;
                    _DoneRate += 1.0f / _ActualFilesCount;
                    FileInfoEventArgs fiea = new FileInfoEventArgs(null, _Entries[index], PercentDoneEventArgs.ProducePercentDone(_DoneRate));
                    OnFileCompression(fiea);
                    if (fiea.Cancel)
                    {
                        _Compressor.Cancelled = true;
                        return -1;
                    }
                }
            }
            return 0;
        }

        public long EnumProperties(IntPtr enumerator)
        {
            //Not implemented HRESULT
            return 0x80004001L;
        }

        public void SetOperationResult(OperationResult operationResult) { }

        #endregion

        #region ICryptoGetTextPassword2 Members

        public int CryptoGetTextPassword2(ref int passwordIsDefined, out string password)
        {
            passwordIsDefined = String.IsNullOrEmpty(Password) ? 0 : 1;
            password = Password;
            return 0;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_FileStream != null)
            {
                _FileStream.Dispose();
            }
        }

        #endregion
    }

    /// <summary>
    /// Callback to implement the ICodeProgress interface
    /// </summary>
    internal sealed class LzmaProgressCallback : ICodeProgress
    {
        private long _InSize;
        private float oldPercentDone;
        public event EventHandler<ProgressEventArgs> Working;

        /// <summary>
        /// Initializes a new instance of the LzmaProgressCallback class
        /// </summary>
        /// <param name="inSize">The input size</param>
        /// <param name="working">Progress event handler</param>
        public LzmaProgressCallback(long inSize, EventHandler<ProgressEventArgs> working)
        {
            _InSize = inSize;
            Working += working;
        }

        #region ICodeProgress Members
        /// <summary>
        /// Sets the progress
        /// </summary>
        /// <param name="inSize">The processed input size</param>
        /// <param name="outSize">The processed output size</param>
        public void SetProgress(long inSize, long outSize)
        {
            if (Working != null)
            {
                float newPercentDone = (inSize + 0.0f) / _InSize;
                float delta = newPercentDone - oldPercentDone;
                if (delta * 100 < 1.0)
                {
                    delta = 0;
                }
                else
                {
                    oldPercentDone = newPercentDone;
                }
                Working(this, new ProgressEventArgs(
                    PercentDoneEventArgs.ProducePercentDone(newPercentDone),
                    delta > 0 ? PercentDoneEventArgs.ProducePercentDone(delta) : (byte)0));
            }
        }

        #endregion
    }
}
