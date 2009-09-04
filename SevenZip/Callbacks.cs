/*  This file is part of SevenZipSharp.

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
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SevenZip.Sdk;

namespace SevenZip
{
#if UNMANAGED
    /// <summary>
    /// Callback to handle the archive opening
    /// </summary>
    internal sealed class ArchiveOpenCallback : SevenZipBase, IArchiveOpenCallback, IArchiveOpenVolumeCallback,
                                                ICryptoGetTextPassword, IDisposable
    {
        private FileInfo _FileInfo;
        private Dictionary<string, InStreamWrapper> _Wrappers = 
            new Dictionary<string, InStreamWrapper>();
        public readonly List<string> VolumeFileNames = new List<string>();
       

        /// <summary>
        /// Performs the common initialization.
        /// </summary>
        /// <param name="fileName">Volume file name.</param>
        private void Init(string fileName)
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                _FileInfo = new FileInfo(fileName);
                VolumeFileNames.Add(fileName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class.
        /// </summary>
        /// <param name="fileName">The archive file name.</param>
        public ArchiveOpenCallback(string fileName)
        {
            Init(fileName);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class.
        /// </summary>
        /// <param name="fileName">The archive file name.</param>
        /// <param name="password">Password for the archive.</param>
        public ArchiveOpenCallback(string fileName, string password) : base(password)
        {
            Init(fileName);
        }

        #region IArchiveOpenCallback Members

        public void SetTotal(IntPtr files, IntPtr bytes) {}

        public void SetCompleted(IntPtr files, IntPtr bytes) {}

        #endregion

        #region IArchiveOpenVolumeCallback Members

        public int GetProperty(ItemPropId propId, ref PropVariant value)
        {
            switch (propId)
            {
                case ItemPropId.Name:
                    value.VarType = VarEnum.VT_BSTR;
                    value.Value = Marshal.StringToBSTR(_FileInfo.FullName);
                    break;
                case ItemPropId.IsDirectory:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = (byte) (_FileInfo.Attributes & FileAttributes.Directory);
                    break;
                case ItemPropId.Size:
                    value.VarType = VarEnum.VT_UI8;
                    value.UInt64Value = (UInt64) _FileInfo.Length;
                    break;
                case ItemPropId.Attributes:
                    value.VarType = VarEnum.VT_UI4;
                    value.UInt32Value = (uint) _FileInfo.Attributes;
                    break;
                case ItemPropId.CreationTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _FileInfo.CreationTime.ToFileTime();
                    break;
                case ItemPropId.LastAccessTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _FileInfo.LastAccessTime.ToFileTime();
                    break;
                case ItemPropId.LastWriteTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _FileInfo.LastWriteTime.ToFileTime();
                    break;
            }
            return 0;
        }

        public int GetStream(string name, out IInStream inStream)
        {
            if (!File.Exists(name))
            {
                name = Path.Combine(Path.GetDirectoryName(_FileInfo.FullName), name);
                if (!File.Exists(name))
                {
                    inStream = null;
                    AddException(new FileNotFoundException("The volume \"" + name + "\" was not found. Extraction is impossible."));
                    return 1;
                }
            }
            VolumeFileNames.Add(name);
            if (_Wrappers.ContainsKey(name))
            {
                inStream = _Wrappers[name];
            }
            else
            {
                try
                {
                    var wrapper = new InStreamWrapper(
                        new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), true);
                    _Wrappers.Add(name, wrapper);
                    inStream = wrapper;                    
                }
                catch (Exception)
                {
                    AddException(new FileNotFoundException("Failed to open the volume \"" + name + "\". Extraction is impossible."));
                    inStream = null;
                    return 1;
                }
            }
            return 0;
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
            if (_Wrappers != null)
            {
                foreach (InStreamWrapper wrap in _Wrappers.Values)
                {
                    wrap.Dispose();
                }
                _Wrappers = null;
            }
            GC.SuppressFinalize(this);
        }

        #endregion        
    }

    /// <summary>
    /// Archive extraction callback to handle the process of unpacking files
    /// </summary>
    internal sealed class ArchiveExtractCallback : SevenZipBase, IArchiveExtractCallback, ICryptoGetTextPassword,
                                                   IDisposable
    {
        private List<uint> _ActualIndexes;
        private IInArchive _Archive;

        /// <summary>
        /// For Compressing event.
        /// </summary>
        private long _BytesCount;

        private long _BytesWritten;
        private long _BytesWrittenOld;
        private string _Directory;

        /// <summary>
        /// Rate of the done work from [0, 1].
        /// </summary>
        private float _DoneRate;

        private SevenZipExtractor _Extractor;
        private FakeOutStreamWrapper _FakeStream;
        private uint? _FileIndex;
        private int _FilesCount;
        private OutStreamWrapper _FileStream;
        const int MemoryPressure = 64 * 1024 * 1024; //64mb seems to be the maximum value

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">The archive files count</param>'
        /// <param name="extractor">The owner of the callback</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes,
                                      SevenZipExtractor extractor)
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
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes,
                                      string password, SevenZipExtractor extractor)
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
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex,
                                      SevenZipExtractor extractor)
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
        public ArchiveExtractCallback(IInArchive archive, Stream stream, int filesCount, uint fileIndex, string password,
                                      SevenZipExtractor extractor)
            : base(password)
        {
            Init(archive, stream, filesCount, fileIndex, extractor);
        }

        private void Init(IInArchive archive, string directory, int filesCount, List<uint> actualIndexes,
                          SevenZipExtractor extractor)
        {
            CommonInit(archive, filesCount, extractor);
            _Directory = directory;
            _ActualIndexes = actualIndexes;
            if (!directory.EndsWith(new string(Path.DirectorySeparatorChar, 1), StringComparison.CurrentCulture))
            {
                _Directory += Path.DirectorySeparatorChar;
            }
        }

        private void Init(IInArchive archive, Stream stream, int filesCount, uint fileIndex, SevenZipExtractor extractor)
        {
            CommonInit(archive, filesCount, extractor);
            _FileStream = new OutStreamWrapper(stream, false);
            _FileStream.BytesWritten += IntEventArgsHandler;
            _FileIndex = fileIndex;
        }

        private void CommonInit(IInArchive archive, int filesCount, SevenZipExtractor extractor)
        {
            _Archive = archive;
            _FilesCount = filesCount;
            _FakeStream = new FakeOutStreamWrapper();
            _FakeStream.BytesWritten += IntEventArgsHandler;
            _Extractor = extractor;
            GC.AddMemoryPressure(MemoryPressure);
        }
        #endregion

        #region Events

        /// <summary>
        /// Occurs when a new file is going to be unpacked
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an output stream for a new file to unpack in</remarks>
        public event EventHandler<FileInfoEventArgs> FileExtractionStarted;

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
        public event EventHandler<FileOverwriteEventArgs> FileExists;

        private void OnFileExists(FileOverwriteEventArgs e)
        {
            if (FileExists != null)
            {
                try
                {
                    FileExists(this, e);
                }
                catch (Exception ex)
                {
                    _Extractor.AddException(ex);
                }
            }
        }

        private void OnOpen(OpenEventArgs e)
        {
            if (Open != null)
            {
                try
                {
                    Open(this, e);
                }
                catch (Exception ex)
                {
                    _Extractor.AddException(ex);
                }
            }
        }

        private void OnFileExtractionStarted(FileInfoEventArgs e)
        {
            if (FileExtractionStarted != null)
            {
                try
                {
                    FileExtractionStarted(this, e);
                }
                catch (Exception ex)
                {
                    _Extractor.AddException(ex);
                }
            }
        }

        private void OnFileExtractionFinished(EventArgs e)
        {
            if (FileExtractionFinished != null)
            {
                try
                {
                    FileExtractionFinished(this, e);
                }
                catch (Exception ex)
                {
                    _Extractor.AddException(ex);
                }
            }
        }

        private void OnExtracting(ProgressEventArgs e)
        {
            if (Extracting != null)
            {
                try
                {
                    Extracting(this, e);
                }
                catch (Exception ex)
                {
                    _Extractor.AddException(ex);
                }
            }
        }

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            var pold = (int) ((_BytesWrittenOld*100)/_BytesCount);
            _BytesWritten += e.Value;
            var pnow = (int) ((_BytesWritten*100)/_BytesCount);
            if (pnow > pold)
            {
                if (pnow > 100)
                {
                    pold = pnow = 0;
                }
                _BytesWrittenOld = _BytesWritten;
                OnExtracting(new ProgressEventArgs((byte) pnow, (byte) (pnow - pold)));
            }
        }

        #endregion

        #region IArchiveExtractCallback Members

        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        public void SetTotal(ulong total)
        {
            _BytesCount = (long) total;
            OnOpen(new OpenEventArgs(total));
        }

        public void SetCompleted(ref ulong completeValue) {}

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
                var fileName = _Directory;
                if (!_FileIndex.HasValue)
                {
                    #region Extraction to a file

                    if (_ActualIndexes == null || _ActualIndexes.Contains(index))
                    {
                        var data = new PropVariant();
                        _Archive.GetProperty(index, ItemPropId.Path, ref data);
                        string entryName = NativeMethods.SafeCast(data, "");

                        #region Get entryName

                        if (String.IsNullOrEmpty(entryName))
                        {
                            if (_FilesCount == 1)
                            {
                                var archName = Path.GetFileName(_Extractor.FileName);
                                archName = archName.Substring(0, archName.LastIndexOf('.'));
                                if (!archName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                                {
                                    archName += ".tar";
                                }
                                entryName = archName;
                            }
                            else
                            {
                                entryName = "[no name] " + index.ToString(CultureInfo.InvariantCulture);
                            }
                        }

                        #endregion

                        fileName = Path.Combine(_Directory, entryName);
                        _Archive.GetProperty(index, ItemPropId.IsDirectory, ref data);
                        try
                        {
                            fileName = ValidateFileName(fileName);
                        }
                        catch (Exception e)
                        {
                            AddException(e);
                            goto FileExtractionStartedLabel;
                        }
                        if (!NativeMethods.SafeCast(data, false))
                        {
                            #region Branch

                            _Archive.GetProperty(index, ItemPropId.LastWriteTime, ref data);
                            var time = NativeMethods.SafeCast(data, DateTime.MinValue);
                            if (File.Exists(fileName))
                            {
                                var fnea = new FileOverwriteEventArgs(fileName);
                                OnFileExists(fnea);
                                if (fnea.Cancel)
                                {
                                    Canceled = true;
                                    return -1;
                                }
                                if (String.IsNullOrEmpty(fnea.FileName))
                                {
                                    outStream = _FakeStream;
                                    goto FileExtractionStartedLabel;
                                }
                                fileName = fnea.FileName;
                            }
                            try
                            {
                                _FileStream = new OutStreamWrapper(File.Create(fileName), fileName, time, true);
                            }
                            catch (Exception e)
                            {
                                if (e is FileNotFoundException)
                                {
                                    AddException(
                                        new IOException("The file \"" + fileName +
                                                        "\" was not extracted due to the File.Create fail."));
                                }
                                else
                                {
                                    AddException(e);
                                }
                                outStream = _FakeStream;
                                goto FileExtractionStartedLabel;
                            }
                            _FileStream.BytesWritten += IntEventArgsHandler;
                            outStream = _FileStream;

                            #endregion
                        }
                        else
                        {
                            #region Branch

                            if (!Directory.Exists(fileName))
                            {
                                try
                                {
                                    Directory.CreateDirectory(fileName);
                                }
                                catch (Exception e)
                                {
                                    AddException(e);
                                }
                                outStream = _FakeStream;
                            }

                            #endregion
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
                    #region Extraction to a stream

                    if (index == _FileIndex)
                    {
                        outStream = _FileStream;
                        _FileIndex = null;
                    }
                    else
                    {
                        outStream = _FakeStream;
                    }

                    #endregion
                }

                FileExtractionStartedLabel:
                _DoneRate += 1.0f/_FilesCount;
                var iea = new FileInfoEventArgs(
                    _Extractor.ArchiveFileData[(int) index], PercentDoneEventArgs.ProducePercentDone(_DoneRate));
                OnFileExtractionStarted(iea);
                if (iea.Cancel)
                {
                    if (!String.IsNullOrEmpty(fileName))
                    {
                        _FileStream.Dispose();
                        if (File.Exists(fileName))
                        {
                            try
                            {
                                File.Delete(fileName);
                            }
                            catch (Exception e)
                            {
                                AddException(e);
                            }
                        }
                    }
                    Canceled = true;
                    return -1;
                }
            }
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode) {}

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
                        AddException(new ExtractionFailedException("File is corrupted. Crc check has failed."));
                        break;
                    case OperationResult.DataError:
                        AddException(new ExtractionFailedException("File is corrupted. Data error has occured."));
                        break;
                    case OperationResult.UnsupportedMethod:
                        AddException(new ExtractionFailedException("Unsupported method error has occured."));
                        break;
                }
            }
            else
            {
                if (_FileStream != null && !_FileIndex.HasValue)
                {
                    #region Future plans

                    /*if (_FilesCount == 1 && _Extractor.ArchiveFileData[0].FileName == "[no name]")
                    {
                        if (FileChecker.CheckSignature(_FileStream.BaseStream) == InArchiveFormat.Tar)
                        {
                            
                        }
                    }*/

                    #endregion

                    try
                    {
                        _FileStream.BytesWritten -= IntEventArgsHandler;
                        _FileStream.Dispose();
                    }
                    catch (ObjectDisposedException) {}
                    _FileStream = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
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
            GC.RemoveMemoryPressure(MemoryPressure);
            if (_FileStream != null)
            {
                try
                {
                    _FileStream.Dispose();
                }
                catch (ObjectDisposedException) {}
                _FileStream = null;
            }
            if (_FakeStream != null)
            {
                try
                {
                    _FakeStream.Dispose();
                }
                catch (ObjectDisposedException) {}
                _FakeStream = null;
            }
        }

        #endregion

        /// <summary>
        /// Validates the file name and ensures that the directory to the file name is valid and creates intermediate directories if necessary
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>The valid file name</returns>
        private static string ValidateFileName(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                throw new SevenZipArchiveException("some archive name is null or empty.");
            }
            var splittedFileName = new List<string>(fileName.Split(Path.DirectorySeparatorChar));
            foreach (char chr in Path.GetInvalidFileNameChars())
            {
                for (int i = 0; i < splittedFileName.Count; i++)
                {
                    if (chr == ':' && i == 0)
                    {
                        continue;
                    }
                    if (String.IsNullOrEmpty(splittedFileName[i]))
                    {
                        continue;
                    }
                    while (splittedFileName[i].IndexOf(chr) > -1)
                    {
                        splittedFileName[i] = splittedFileName[i].Replace(chr, '_');
                    }
                }
            }
            if (fileName.StartsWith(new string(Path.DirectorySeparatorChar, 2),
                                    StringComparison.CurrentCultureIgnoreCase))
            {
                splittedFileName.RemoveAt(0);
                splittedFileName.RemoveAt(0);
                splittedFileName[0] = new string(Path.DirectorySeparatorChar, 2) + splittedFileName[0];
            }
            if (splittedFileName.Count > 2)
            {
                string tfn = splittedFileName[0];
                for (int i = 1; i < splittedFileName.Count - 1; i++)
                {
                    tfn += Path.DirectorySeparatorChar + splittedFileName[i];
                    if (!Directory.Exists(tfn))
                    {
                        Directory.CreateDirectory(tfn);
                    }
                }
            }
            return String.Join(new string(Path.DirectorySeparatorChar, 1), splittedFileName.ToArray());
        }        
    }

#if COMPRESS
    /// <summary>
    /// Archive update callback to handle the process of packing files
    /// </summary>
    internal sealed class ArchiveUpdateCallback : SevenZipBase, IArchiveUpdateCallback, ICryptoGetTextPassword2,
                                                  IDisposable
    {
        /// <summary>
        /// _Files.Count if do not count directories
        /// </summary>
        private int _ActualFilesCount;

        /// <summary>
        /// For Compressing event.
        /// </summary>
        private long _BytesCount;

        private long _BytesWritten;
        private long _BytesWrittenOld;
        private SevenZipCompressor _Compressor;

        /// <summary>
        /// No directories.
        /// </summary>
        private bool _DirectoryStructure;

        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _DoneRate;

        /// <summary>
        /// The names of the archive entries
        /// </summary>
        private string[] _Entries;

        /// <summary>
        /// Array of files to pack
        /// </summary>
        private FileInfo[] _Files;

        private InStreamWrapper _FileStream;

        private uint _IndexInArchive;
        private uint _IndexOffset;

        /// <summary>
        /// Common file names root length
        /// </summary>
        private int _RootLength;

        /// <summary>
        /// Input streams
        /// </summary>
        private Stream[] _Streams;

        private UpdateData _UpdateData;
        private List<InStreamWrapper> _WrappersToDispose;

        /// <summary>
        /// Gets or sets the default item name used in MemoryStream compression.
        /// </summary>
        public string DefaultItemName { private get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether to compress as fast as possible, without calling events.
        /// </summary>
        public bool FastCompression { private get; set; }
        public float DictionarySize { private get; set; }
        private int _MemoryPressure;

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            FileInfo[] files, int rootLength,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            Init(files, rootLength, compressor, updateData, directoryStructure);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            FileInfo[] files, int rootLength, string password,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
            : base(password)
        {
            Init(files, rootLength, compressor, updateData, directoryStructure);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            Stream stream, SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            Init(stream, compressor, updateData, directoryStructure);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="stream">The input stream</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            Stream stream, string password, SevenZipCompressor compressor, UpdateData updateData,
            bool directoryStructure)
            : base(password)
        {
            Init(stream, compressor, updateData, directoryStructure);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="streamDict">Dictionary&lt;file stream, name of the archive entry&gt;</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            Dictionary<Stream, string> streamDict,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            Init(streamDict, compressor, updateData, directoryStructure);
        }

        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="streamDict">Dictionary&lt;file stream, name of the archive entry&gt;</param>
        /// <param name="password">The archive password</param>
        /// <param name="compressor">The owner of the callback</param>
        /// <param name="updateData">The compression parameters.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        public ArchiveUpdateCallback(
            Dictionary<Stream, string> streamDict, string password,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
            : base(password)
        {
            Init(streamDict, compressor, updateData, directoryStructure);
        }

        private void CommonInit(SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            _Compressor = compressor;
            _IndexInArchive = updateData.FilesCount;
            _IndexOffset = updateData.Mode != InternalCompressionMode.Append ? 0 : _IndexInArchive;
            if (_Compressor.ArchiveFormat == OutArchiveFormat.Zip)
            {
                _WrappersToDispose = new List<InStreamWrapper>();
            }
            _UpdateData = updateData;
            _DirectoryStructure = directoryStructure;
            DefaultItemName = "default";
            _MemoryPressure = (int)(DictionarySize * 1024 * 1024);
            GC.AddMemoryPressure(_MemoryPressure);
        }

        private void Init(
            FileInfo[] files, int rootLength, SevenZipCompressor compressor,
            UpdateData updateData, bool directoryStructure)
        {
            _Files = files;
            _RootLength = rootLength;
            if (files != null)
            {
                foreach (var fi in files)
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
            }
            CommonInit(compressor, updateData, directoryStructure);
        }

        private void Init(
            Stream stream, SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            _FileStream = new InStreamWrapper(stream, false);
            _FileStream.BytesRead += IntEventArgsHandler;
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
            CommonInit(compressor, updateData, directoryStructure);
        }

        private void Init(
            Dictionary<Stream, string> streamDict,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
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
            CommonInit(compressor, updateData, directoryStructure);
        }

        #endregion

        private bool EventsForGetStream(uint index)
        {
            if (!FastCompression)
            {
                _FileStream.BytesRead += IntEventArgsHandler;
                _DoneRate += 1.0f / _ActualFilesCount;
                var fiea = new FileNameEventArgs(_Files[index].Name,
                                                 PercentDoneEventArgs.ProducePercentDone(_DoneRate));
                OnFileCompression(fiea);
                if (fiea.Cancel)
                {
                    Canceled = true;
                    return false;
                }
            }
            return true;
        }

        #region Events

        /// <summary>
        /// Occurs when the next file is going to be packed.
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an input stream for the next file to pack it</remarks>
        public event EventHandler<FileNameEventArgs> FileCompressionStarted;

        /// <summary>
        /// Occurs when data are being compressed.
        /// </summary>
        public event EventHandler<ProgressEventArgs> Compressing;

        /// <summary>
        /// Occurs when the current file was compressed.
        /// </summary>
        public event EventHandler FileCompressionFinished;

        private void OnFileCompression(FileNameEventArgs e)
        {
            if (FileCompressionStarted != null)
            {
                try
                {
                    FileCompressionStarted(this, e);
                }
                catch (Exception ex)
                {
                    _Compressor.AddException(ex);
                }
            }
        }

        private void OnCompressing(ProgressEventArgs e)
        {
            if (Compressing != null)
            {
                try
                {
                    Compressing(this, e);
                }
                catch (Exception ex)
                {
                    _Compressor.AddException(ex);
                }
            }
        }

        private void OnFileCompressionFinished(EventArgs e)
        {
            if (FileCompressionFinished != null)
            {
                try
                {
                    FileCompressionFinished(this, e);
                }
                catch (Exception ex)
                {
                    _Compressor.AddException(ex);
                }
            }
        }

        #endregion

        #region IArchiveUpdateCallback Members

        public void SetTotal(ulong total) {}

        public void SetCompleted(ref ulong completeValue) {}

        public int GetUpdateItemInfo(uint index, ref int newData, ref int newProperties, ref uint indexInArchive)
        {
            switch (_UpdateData.Mode)
            {
                case InternalCompressionMode.Create:
                    newData = 1;
                    newProperties = 1;
                    indexInArchive = UInt32.MaxValue;
                    break;
                case InternalCompressionMode.Append:
                    if (index < _IndexInArchive)
                    {
                        newData = 0;
                        newProperties = 0;
                        indexInArchive = index;
                    }
                    else
                    {
                        newData = 1;
                        newProperties = 1;
                        indexInArchive = UInt32.MaxValue;
                    }
                    break;
                case InternalCompressionMode.Modify:
                    newData = 0;
                    newProperties = Convert.ToInt32(_UpdateData.FileNamesToModify.ContainsKey((int) index)
                                                    && _UpdateData.FileNamesToModify[(int) index] != null);
                    if (_UpdateData.FileNamesToModify.ContainsKey((int) index)
                        && _UpdateData.FileNamesToModify[(int) index] == null)
                    {
                        indexInArchive = index != _UpdateData.ArchiveFileData.Count - 1
                                             ?
                                                 (uint) (_UpdateData.ArchiveFileData.Count - 1)
                                             : 0;
                    }
                    else
                    {
                        indexInArchive = index;
                    }
                    break;
            }
            return 0;
        }

        public int GetProperty(uint index, ItemPropId propID, ref PropVariant value)
        {
            index -= _IndexOffset;
            try
            {
                switch (propID)
                {
                    case ItemPropId.IsAnti:
                        value.VarType = VarEnum.VT_BOOL;
                        value.UInt64Value = 0;
                        break;
                    case ItemPropId.Path:

                        #region Path

                        value.VarType = VarEnum.VT_BSTR;
                        string val = DefaultItemName;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            if (_Files == null)
                            {
                                if (_Entries != null)
                                {
                                    val = _Entries[index];
                                }
                            }
                            else
                            {
                                if (_DirectoryStructure)
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
                                else
                                {
                                    val = _Files[index].Name;
                                }
                            }
                        }
                        else
                        {
                            val = _UpdateData.FileNamesToModify[(int) index];
                        }
                        value.Value = Marshal.StringToBSTR(val);

                        #endregion

                        break;
                    case ItemPropId.IsDirectory:
                        value.VarType = VarEnum.VT_BOOL;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.UInt64Value = _Files == null
                                                ? (ulong) 0
                                                : (byte) (_Files[index].Attributes & FileAttributes.Directory);
                        }
                        else
                        {
                            value.UInt64Value = Convert.ToUInt64(_UpdateData.ArchiveFileData[(int) index].IsDirectory);
                        }
                        break;
                    case ItemPropId.Size:

                        #region Size

                        value.VarType = VarEnum.VT_UI8;
                        UInt64 size;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            if (_Files == null)
                            {
                                if (_Streams == null)
                                {
                                    size = _BytesCount > 0 ? (ulong) _BytesCount : 0;
                                }
                                else
                                {
                                    size = (ulong) _Streams[index].Length;
                                }
                            }
                            else
                            {
                                size = (_Files[index].Attributes & FileAttributes.Directory) == 0
                                           ?
                                               (ulong) _Files[index].Length
                                           : 0;
                            }
                        }
                        else
                        {
                            size = _UpdateData.ArchiveFileData[(int) index].Size;
                        }
                        value.UInt64Value = size;

                        #endregion

                        break;
                    case ItemPropId.Attributes:
                        value.VarType = VarEnum.VT_UI4;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.UInt32Value = _Files == null
                                                ? 32
                                                : (uint) _Files[index].Attributes;
                        }
                        else
                        {
                            value.UInt32Value = _UpdateData.ArchiveFileData[(int) index].Attributes;
                        }
                        break;
                    case ItemPropId.CreationTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _Files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _Files[index].CreationTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _UpdateData.ArchiveFileData[(int) index].CreationTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.LastAccessTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _Files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _Files[index].LastAccessTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _UpdateData.ArchiveFileData[(int) index].LastAccessTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.LastWriteTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _Files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _Files[index].LastWriteTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _UpdateData.ArchiveFileData[(int) index].LastWriteTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.Extension:

                        #region Extension

                        value.VarType = VarEnum.VT_BSTR;
                        if (_UpdateData.Mode != InternalCompressionMode.Modify)
                        {
                            try
                            {
                                val = _Files != null
                                      ? _Files[index].Extension.Substring(1)
                                      : _Entries == null
                                          ? ""
                                          : Path.GetExtension(_Entries[index]);
                                value.Value = Marshal.StringToBSTR(val);
                            }
                            catch (ArgumentException)
                            {
                                value.Value = Marshal.StringToBSTR("");
                            }
                        }
                        else
                        {
                            val = Path.GetExtension(_UpdateData.ArchiveFileData[(int) index].FileName);
                            value.Value = Marshal.StringToBSTR(val);
                        }

                        #endregion

                        break;
                }
            }
            catch (Exception e)
            {
                AddException(e);
            }
            return 0;
        }

        /// <summary>
        /// Gets the stream for 7-zip library.
        /// </summary>
        /// <param name="index">File index</param>
        /// <param name="inStream">Input file stream</param>
        /// <returns>Zero if Ok</returns>
        public int GetStream(uint index, out ISequentialInStream inStream)
        {
            index -= _IndexOffset;
            if (_Files != null)
            {
                _FileStream = null;
                try
                {
                    _FileStream = new InStreamWrapper(
                        new FileStream(_Files[index].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                        true);
                }
                catch (Exception e)
                {
                    AddException(e);
                    inStream = null;
                    return -1;
                }
                inStream = _FileStream;
                if (!EventsForGetStream(index))
                {
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
                    inStream = _FileStream;
                    if (!EventsForGetStream(index))
                    {
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

        public void SetOperationResult(OperationResult operationResult)
        {
            if (operationResult != OperationResult.Ok && ReportErrors)
            {
                switch (operationResult)
                {
                    case OperationResult.CrcError:
                        AddException(new ExtractionFailedException("File is corrupted. Crc check has failed."));
                        break;
                    case OperationResult.DataError:
                        AddException(new ExtractionFailedException("File is corrupted. Data error has occured."));
                        break;
                    case OperationResult.UnsupportedMethod:
                        AddException(new ExtractionFailedException("Unsupported method error has occured."));
                        break;
                }
            }
            if (_FileStream != null)
            {
                
                    _FileStream.BytesRead -= IntEventArgsHandler;
                    //Specific Zip implementation - can not Dispose files for Zip.
                    if (_Compressor.ArchiveFormat != OutArchiveFormat.Zip)
                    {
                        try
                        {
                            _FileStream.Dispose();                            
                        }
                        catch (ObjectDisposedException) {}
                    }
                    else
                    {
                        _WrappersToDispose.Add(_FileStream);
                    }                                
                _FileStream = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            OnFileCompressionFinished(EventArgs.Empty);
        }

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
            GC.RemoveMemoryPressure(_MemoryPressure);
            if (_FileStream != null)
            {
                try
                {
                    _FileStream.Dispose();
                }
                catch (ObjectDisposedException) {}
            }
            if (_WrappersToDispose != null)
            {
                foreach (var wrapper in _WrappersToDispose)
                {
                    try
                    {
                        wrapper.Dispose();
                    }
                    catch (ObjectDisposedException) {}
                }
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            lock (this)
            {
                var pold = (byte) ((_BytesWrittenOld*100)/_BytesCount);
                _BytesWritten += e.Value;
                var pnow = (byte) ((_BytesWritten*100)/_BytesCount);
                if (pnow > pold)
                {
                    _BytesWrittenOld = _BytesWritten;
                    OnCompressing(new ProgressEventArgs(pnow, (byte) (pnow - pold)));
                }
            }
        }
    }
#endif
#endif

    /// <summary>
    /// Callback to implement the ICodeProgress interface
    /// </summary>
    internal sealed class LzmaProgressCallback : ICodeProgress
    {
        private readonly long _InSize;
        private float _OldPercentDone;

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
                float newPercentDone = (inSize + 0.0f)/_InSize;
                float delta = newPercentDone - _OldPercentDone;
                if (delta*100 < 1.0)
                {
                    delta = 0;
                }
                else
                {
                    _OldPercentDone = newPercentDone;
                }
                Working(this, new ProgressEventArgs(
                                  PercentDoneEventArgs.ProducePercentDone(newPercentDone),
                                  delta > 0 ? PercentDoneEventArgs.ProducePercentDone(delta) : (byte) 0));
            }
        }

        #endregion

        public event EventHandler<ProgressEventArgs> Working;
    }
}