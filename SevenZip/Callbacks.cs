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
        private FileInfo _fileInfo;
        private Dictionary<string, InStreamWrapper> _wrappers = 
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
                _fileInfo = new FileInfo(fileName);
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
                    value.Value = Marshal.StringToBSTR(_fileInfo.FullName);
                    break;
                case ItemPropId.IsDirectory:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = (byte) (_fileInfo.Attributes & FileAttributes.Directory);
                    break;
                case ItemPropId.Size:
                    value.VarType = VarEnum.VT_UI8;
                    value.UInt64Value = (UInt64) _fileInfo.Length;
                    break;
                case ItemPropId.Attributes:
                    value.VarType = VarEnum.VT_UI4;
                    value.UInt32Value = (uint) _fileInfo.Attributes;
                    break;
                case ItemPropId.CreationTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _fileInfo.CreationTime.ToFileTime();
                    break;
                case ItemPropId.LastAccessTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _fileInfo.LastAccessTime.ToFileTime();
                    break;
                case ItemPropId.LastWriteTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _fileInfo.LastWriteTime.ToFileTime();
                    break;
            }
            return 0;
        }

        public int GetStream(string name, out IInStream inStream)
        {
            if (!File.Exists(name))
            {
                name = Path.Combine(Path.GetDirectoryName(_fileInfo.FullName), name);
                if (!File.Exists(name))
                {
                    inStream = null;
                    AddException(new FileNotFoundException("The volume \"" + name + "\" was not found. Extraction is impossible."));
                    return 1;
                }
            }
            VolumeFileNames.Add(name);
            if (_wrappers.ContainsKey(name))
            {
                inStream = _wrappers[name];
            }
            else
            {
                try
                {
                    var wrapper = new InStreamWrapper(
                        new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), true);
                    _wrappers.Add(name, wrapper);
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
            if (_wrappers != null)
            {
                foreach (InStreamWrapper wrap in _wrappers.Values)
                {
                    wrap.Dispose();
                }
                _wrappers = null;
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
        private List<uint> _actualIndexes;
        private IInArchive _archive;

        /// <summary>
        /// For Compressing event.
        /// </summary>
        private long _bytesCount;

        private long _bytesWritten;
        private long _bytesWrittenOld;
        private string _directory;

        /// <summary>
        /// Rate of the done work from [0, 1].
        /// </summary>
        private float _doneRate;

        private SevenZipExtractor _extractor;
        private FakeOutStreamWrapper _fakeStream;
        private uint? _fileIndex;
        private int _filesCount;
        private OutStreamWrapper _fileStream;
        const int MEMORY_PRESSURE = 64 * 1024 * 1024; //64mb seems to be the maximum value

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
            _directory = directory;
            _actualIndexes = actualIndexes;
            if (!directory.EndsWith(new string(Path.DirectorySeparatorChar, 1), StringComparison.CurrentCulture))
            {
                _directory += Path.DirectorySeparatorChar;
            }
        }

        private void Init(IInArchive archive, Stream stream, int filesCount, uint fileIndex, SevenZipExtractor extractor)
        {
            CommonInit(archive, filesCount, extractor);
            _fileStream = new OutStreamWrapper(stream, false);
            _fileStream.BytesWritten += IntEventArgsHandler;
            _fileIndex = fileIndex;
        }

        private void CommonInit(IInArchive archive, int filesCount, SevenZipExtractor extractor)
        {
            _archive = archive;
            _filesCount = filesCount;
            _fakeStream = new FakeOutStreamWrapper();
            _fakeStream.BytesWritten += IntEventArgsHandler;
            _extractor = extractor;
            GC.AddMemoryPressure(MEMORY_PRESSURE);
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
                    _extractor.AddException(ex);
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
                    _extractor.AddException(ex);
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
                    _extractor.AddException(ex);
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
                    _extractor.AddException(ex);
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
                    _extractor.AddException(ex);
                }
            }
        }

        private void IntEventArgsHandler(object sender, IntEventArgs e)
        {
            var pold = (int) ((_bytesWrittenOld*100)/_bytesCount);
            _bytesWritten += e.Value;
            var pnow = (int) ((_bytesWritten*100)/_bytesCount);
            if (pnow > pold)
            {
                if (pnow > 100)
                {
                    pold = pnow = 0;
                }
                _bytesWrittenOld = _bytesWritten;
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
            _bytesCount = (long) total;
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
                var fileName = _directory;
                if (!_fileIndex.HasValue)
                {
                    #region Extraction to a file

                    if (_actualIndexes == null || _actualIndexes.Contains(index))
                    {
                        var data = new PropVariant();
                        _archive.GetProperty(index, ItemPropId.Path, ref data);
                        string entryName = NativeMethods.SafeCast(data, "");

                        #region Get entryName

                        if (String.IsNullOrEmpty(entryName))
                        {
                            if (_filesCount == 1)
                            {
                                var archName = Path.GetFileName(_extractor.FileName);
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

                        fileName = Path.Combine(_directory, entryName);
                        _archive.GetProperty(index, ItemPropId.IsDirectory, ref data);
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

                            _archive.GetProperty(index, ItemPropId.LastWriteTime, ref data);
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
                                    outStream = _fakeStream;
                                    goto FileExtractionStartedLabel;
                                }
                                fileName = fnea.FileName;
                            }
                            try
                            {
                                _fileStream = new OutStreamWrapper(File.Create(fileName), fileName, time, true);
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
                                outStream = _fakeStream;
                                goto FileExtractionStartedLabel;
                            }
                            _fileStream.BytesWritten += IntEventArgsHandler;
                            outStream = _fileStream;

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
                                outStream = _fakeStream;
                            }

                            #endregion
                        }
                    }
                    else
                    {
                        outStream = _fakeStream;
                    }

                    #endregion
                }
                else
                {
                    #region Extraction to a stream

                    if (index == _fileIndex)
                    {
                        outStream = _fileStream;
                        _fileIndex = null;
                    }
                    else
                    {
                        outStream = _fakeStream;
                    }

                    #endregion
                }

                FileExtractionStartedLabel:
                _doneRate += 1.0f/_filesCount;
                var iea = new FileInfoEventArgs(
                    _extractor.ArchiveFileData[(int) index], PercentDoneEventArgs.ProducePercentDone(_doneRate));
                OnFileExtractionStarted(iea);
                if (iea.Cancel)
                {
                    if (!String.IsNullOrEmpty(fileName))
                    {
                        _fileStream.Dispose();
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
                if (_fileStream != null && !_fileIndex.HasValue)
                {
                    #region Future plans

                    /*if (_FilesCount == 1 && _Extractor.ArchiveFileData[0].FileName == "[no name]")
                    {
                        if (FileChecker.CheckSignature(_fileStream.BaseStream) == InArchiveFormat.Tar)
                        {
                            
                        }
                    }*/

                    #endregion

                    try
                    {
                        _fileStream.BytesWritten -= IntEventArgsHandler;
                        _fileStream.Dispose();
                    }
                    catch (ObjectDisposedException) {}
                    _fileStream = null;
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
            GC.RemoveMemoryPressure(MEMORY_PRESSURE);
            if (_fileStream != null)
            {
                try
                {
                    _fileStream.Dispose();
                }
                catch (ObjectDisposedException) {}
                _fileStream = null;
            }
            if (_fakeStream != null)
            {
                try
                {
                    _fakeStream.Dispose();
                }
                catch (ObjectDisposedException) {}
                _fakeStream = null;
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
        #region Fields
        /// <summary>
        /// _files.Count if do not count directories
        /// </summary>
        private int _actualFilesCount;

        /// <summary>
        /// For Compressing event.
        /// </summary>
        private long _bytesCount;

        private long _bytesWritten;
        private long _bytesWrittenOld;
        private SevenZipCompressor _compressor;

        /// <summary>
        /// No directories.
        /// </summary>
        private bool _directoryStructure;

        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _doneRate;

        /// <summary>
        /// The names of the archive entries
        /// </summary>
        private string[] _entries;

        /// <summary>
        /// Array of files to pack
        /// </summary>
        private FileInfo[] _files;

        private InStreamWrapper _fileStream;

        private uint _indexInArchive;
        private uint _indexOffset;

        /// <summary>
        /// Common root of file names length.
        /// </summary>
        private int _rootLength;

        /// <summary>
        /// Input streams to be compressed.
        /// </summary>
        private Stream[] _streams;

        private UpdateData _updateData;
        private List<InStreamWrapper> _wrappersToDispose;

        /// <summary>
        /// Gets or sets the default item name used in MemoryStream compression.
        /// </summary>
        public string DefaultItemName { private get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether to compress as fast as possible, without calling events.
        /// </summary>
        public bool FastCompression { private get; set; }        
        private int _memoryPressure;
        #endregion

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
            _compressor = compressor;
            _indexInArchive = updateData.FilesCount;
            _indexOffset = updateData.Mode != InternalCompressionMode.Append ? 0 : _indexInArchive;
            if (_compressor.ArchiveFormat == OutArchiveFormat.Zip)
            {
                _wrappersToDispose = new List<InStreamWrapper>();
            }
            _updateData = updateData;
            _directoryStructure = directoryStructure;
            DefaultItemName = "default";            
        }

        private void Init(
            FileInfo[] files, int rootLength, SevenZipCompressor compressor,
            UpdateData updateData, bool directoryStructure)
        {
            _files = files;
            _rootLength = rootLength;
            if (files != null)
            {
                foreach (var fi in files)
                {
                    if (fi.Exists)
                    {
                        _bytesCount += fi.Length;
                        if ((fi.Attributes & FileAttributes.Directory) == 0)
                        {
                            _actualFilesCount++;
                        }
                    }
                }
            }
            CommonInit(compressor, updateData, directoryStructure);
        }

        private void Init(
            Stream stream, SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            _fileStream = new InStreamWrapper(stream, false);
            _fileStream.BytesRead += IntEventArgsHandler;
            _actualFilesCount = 1;
            try
            {
                _bytesCount = stream.Length;
            }
            catch (NotSupportedException)
            {
                _bytesCount = -1;
            }
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (NotSupportedException)
            {
                _bytesCount = -1;
            }
            CommonInit(compressor, updateData, directoryStructure);
        }

        private void Init(
            Dictionary<Stream, string> streamDict,
            SevenZipCompressor compressor, UpdateData updateData, bool directoryStructure)
        {
            _streams = new Stream[streamDict.Count];
            streamDict.Keys.CopyTo(_streams, 0);
            _entries = new string[streamDict.Count];
            streamDict.Values.CopyTo(_entries, 0);
            _actualFilesCount = streamDict.Count;
            foreach (Stream str in _streams)
            {
                _bytesCount += str.Length;
            }
            CommonInit(compressor, updateData, directoryStructure);
        }

        #endregion

        /// <summary>
        /// Gets or sets the dictionary size.
        /// </summary>
        public float DictionarySize
        {
            set
            {
                _memoryPressure = (int)(value * 1024 * 1024);
                GC.AddMemoryPressure(_memoryPressure);
            }
        }

        /// <summary>
        /// Raises events for the GetStream method.
        /// </summary>
        /// <param name="index">The current item index.</param>
        /// <returns>True if not cancelled; otherwise, false.</returns>
        private bool EventsForGetStream(uint index)
        {
            if (!FastCompression)
            {
                _fileStream.BytesRead += IntEventArgsHandler;
                _doneRate += 1.0f / _actualFilesCount;
                var fiea = new FileNameEventArgs(_files != null? _files[index].Name : _entries[index],
                                                 PercentDoneEventArgs.ProducePercentDone(_doneRate));
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
                    _compressor.AddException(ex);
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
                    _compressor.AddException(ex);
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
                    _compressor.AddException(ex);
                }
            }
        }

        #endregion

        #region IArchiveUpdateCallback Members

        public void SetTotal(ulong total) {}

        public void SetCompleted(ref ulong completeValue) {}

        public int GetUpdateItemInfo(uint index, ref int newData, ref int newProperties, ref uint indexInArchive)
        {
            switch (_updateData.Mode)
            {
                case InternalCompressionMode.Create:
                    newData = 1;
                    newProperties = 1;
                    indexInArchive = UInt32.MaxValue;
                    break;
                case InternalCompressionMode.Append:
                    if (index < _indexInArchive)
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
                    newProperties = Convert.ToInt32(_updateData.FileNamesToModify.ContainsKey((int) index)
                                                    && _updateData.FileNamesToModify[(int) index] != null);
                    if (_updateData.FileNamesToModify.ContainsKey((int) index)
                        && _updateData.FileNamesToModify[(int) index] == null)
                    {
                        indexInArchive = index != _updateData.ArchiveFileData.Count - 1
                                             ?
                                                 (uint) (_updateData.ArchiveFileData.Count - 1)
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
            index -= _indexOffset;
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
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            if (_files == null)
                            {
                                if (_entries != null)
                                {
                                    val = _entries[index];
                                }
                            }
                            else
                            {
                                if (_directoryStructure)
                                {
                                    if (_rootLength > 0)
                                    {
                                        val = _files[index].FullName.Substring(_rootLength);
                                    }
                                    else
                                    {
                                        val = _files[index].FullName[0] + _files[index].FullName.Substring(2);
                                    }
                                }
                                else
                                {
                                    val = _files[index].Name;
                                }
                            }
                        }
                        else
                        {
                            val = _updateData.FileNamesToModify[(int) index];
                        }
                        value.Value = Marshal.StringToBSTR(val);

                        #endregion

                        break;
                    case ItemPropId.IsDirectory:
                        value.VarType = VarEnum.VT_BOOL;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.UInt64Value = _files == null
                                                ? (ulong) 0
                                                : (byte) (_files[index].Attributes & FileAttributes.Directory);
                        }
                        else
                        {
                            value.UInt64Value = Convert.ToUInt64(_updateData.ArchiveFileData[(int) index].IsDirectory);
                        }
                        break;
                    case ItemPropId.Size:

                        #region Size

                        value.VarType = VarEnum.VT_UI8;
                        UInt64 size;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            if (_files == null)
                            {
                                if (_streams == null)
                                {
                                    size = _bytesCount > 0 ? (ulong) _bytesCount : 0;
                                }
                                else
                                {
                                    size = (ulong) _streams[index].Length;
                                }
                            }
                            else
                            {
                                size = (_files[index].Attributes & FileAttributes.Directory) == 0
                                           ?
                                               (ulong) _files[index].Length
                                           : 0;
                            }
                        }
                        else
                        {
                            size = _updateData.ArchiveFileData[(int) index].Size;
                        }
                        value.UInt64Value = size;

                        #endregion

                        break;
                    case ItemPropId.Attributes:
                        value.VarType = VarEnum.VT_UI4;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.UInt32Value = _files == null
                                                ? 32
                                                : (uint) _files[index].Attributes;
                        }
                        else
                        {
                            value.UInt32Value = _updateData.ArchiveFileData[(int) index].Attributes;
                        }
                        break;
                    case ItemPropId.CreationTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _files[index].CreationTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _updateData.ArchiveFileData[(int) index].CreationTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.LastAccessTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _files[index].LastAccessTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _updateData.ArchiveFileData[(int) index].LastAccessTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.LastWriteTime:
                        value.VarType = VarEnum.VT_FILETIME;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            value.Int64Value = _files == null
                                               ? DateTime.Now.ToFileTime()
                                               : _files[index].LastWriteTime.ToFileTime();
                        }
                        else
                        {
                            value.Int64Value = _updateData.ArchiveFileData[(int) index].LastWriteTime.ToFileTime();
                        }
                        break;
                    case ItemPropId.Extension:

                        #region Extension

                        value.VarType = VarEnum.VT_BSTR;
                        if (_updateData.Mode != InternalCompressionMode.Modify)
                        {
                            try
                            {
                                val = _files != null
                                      ? _files[index].Extension.Substring(1)
                                      : _entries == null
                                          ? ""
                                          : Path.GetExtension(_entries[index]);
                                value.Value = Marshal.StringToBSTR(val);
                            }
                            catch (ArgumentException)
                            {
                                value.Value = Marshal.StringToBSTR("");
                            }
                        }
                        else
                        {
                            val = Path.GetExtension(_updateData.ArchiveFileData[(int) index].FileName);
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
            index -= _indexOffset;
            if (_files != null)
            {
                _fileStream = null;
                try
                {
                    _fileStream = new InStreamWrapper(
                        new FileStream(_files[index].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                        true);
                }
                catch (Exception e)
                {
                    AddException(e);
                    inStream = null;
                    return -1;
                }
                inStream = _fileStream;
                if (!EventsForGetStream(index))
                {
                    return -1;
                }
            }
            else
            {
                if (_streams == null)
                {
                    inStream = _fileStream;
                }
                else
                {
                    _fileStream = new InStreamWrapper(_streams[index], true);
                    inStream = _fileStream;
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
            if (_fileStream != null)
            {
                
                    _fileStream.BytesRead -= IntEventArgsHandler;
                    //Specific Zip implementation - can not Dispose files for Zip.
                    if (_compressor.ArchiveFormat != OutArchiveFormat.Zip)
                    {
                        try
                        {
                            _fileStream.Dispose();                            
                        }
                        catch (ObjectDisposedException) {}
                    }
                    else
                    {
                        _wrappersToDispose.Add(_fileStream);
                    }                                
                _fileStream = null;
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
            GC.RemoveMemoryPressure(_memoryPressure);
            if (_fileStream != null)
            {
                try
                {
                    _fileStream.Dispose();
                }
                catch (ObjectDisposedException) {}
            }
            if (_wrappersToDispose != null)
            {
                foreach (var wrapper in _wrappersToDispose)
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
                var pold = (byte) ((_bytesWrittenOld*100)/_bytesCount);
                _bytesWritten += e.Value;
                byte pnow;
                if (_bytesCount < _bytesWritten) //Holy shit, this check for ZIP is golden
                {
                    pnow = 100;
                }
                else
                {
                    pnow = (byte)((_bytesWritten * 100) / _bytesCount);
                }
                if (pnow > pold)
                {
                    _bytesWrittenOld = _bytesWritten;
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
        private readonly long _inSize;
        private float _oldPercentDone;

        /// <summary>
        /// Initializes a new instance of the LzmaProgressCallback class
        /// </summary>
        /// <param name="inSize">The input size</param>
        /// <param name="working">Progress event handler</param>
        public LzmaProgressCallback(long inSize, EventHandler<ProgressEventArgs> working)
        {
            _inSize = inSize;
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
                float newPercentDone = (inSize + 0.0f)/_inSize;
                float delta = newPercentDone - _oldPercentDone;
                if (delta*100 < 1.0)
                {
                    delta = 0;
                }
                else
                {
                    _oldPercentDone = newPercentDone;
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