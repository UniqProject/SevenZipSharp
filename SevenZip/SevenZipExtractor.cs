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
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SevenZip.ComRoutines;
using SevenZip.Sdk;
using SevenZip.Sdk.Compression.Lzma;

namespace SevenZip
{
    /// <summary>
    /// Class for extracting and getting information about 7-zip archives
    /// </summary>
    public sealed class SevenZipExtractor 
        #if UNMANAGED
        : SevenZipBase, ISevenZipExtractor, IDisposable 
        #endif
    {
        #if UNMANAGED
        private List<ArchiveFileInfo> _ArchiveFileData;
        private IInArchive _Archive;
        private IInStream _ArchiveStream;
        private ArchiveOpenCallback _OpenCallback;
        private string _FileName;
        private Stream _InStream;
        private long _PackedSize;
        private long? _UnpackedSize;
        private uint? _FilesCount;
        private bool? _IsSolid;
        private bool _Opened;
        private InArchiveFormat _Format;
        private ReadOnlyCollection<ArchiveFileInfo> _ArchiveFileInfoCollection;
        private ReadOnlyCollection<ArchiveProperty> _ArchiveProperties;
        internal bool Cancelled;

        /// <summary>
        /// Changes the path to the 7-zip native library
        /// </summary>
        /// <param name="libraryPath">The path to the 7-zip native library</param>
        public static void SetLibraryPath(string libraryPath)
        {
            SevenZipLibraryManager.SetLibraryPath(libraryPath);
        }

        #region Constructors
        /// <summary>
        /// General initialization function
        /// </summary>
        /// <param name="archiveFullName">The archive file name</param>
        private void Init(string archiveFullName)
        {
            SevenZipLibraryManager.LoadLibrary(this, FileChecker.CheckSignature(archiveFullName));
            try
            {
                _FileName = archiveFullName;
                _Format = FileChecker.CheckSignature(_FileName);
                _Archive = SevenZipLibraryManager.InArchive(_Format, this);
                _PackedSize = (new FileInfo(archiveFullName)).Length;
            }
            catch (SevenZipLibraryException)
            {
                SevenZipLibraryManager.FreeLibrary(this, _Format);
                throw;
            }
        }

        /// <summary>
        /// General initialization function
        /// </summary>
        /// <param name="stream">The stream to read the archive from.</param>
        private void Init(Stream stream)
        {
            ValidateStream(stream);
            _Format = FileChecker.CheckSignature(stream);
            SevenZipLibraryManager.LoadLibrary(this, _Format);
            try
            {
                _InStream = stream;                
                _Archive = SevenZipLibraryManager.InArchive(_Format, this);
                _PackedSize = stream.Length;
            }
            catch (SevenZipLibraryException)
            {
                SevenZipLibraryManager.FreeLibrary(this, _Format);
                throw;
            }
        }
        
        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">The archive full file name</param>
        public SevenZipExtractor(string archiveFullName)
            : base()
        {
            Init(archiveFullName);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveStream">The stream to read the archive from</param>
        /// <remarks>The archive format is guessed by the signature.</remarks>
        public SevenZipExtractor(Stream archiveStream)
            : base()
        {
            Init(archiveStream);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">The archive full file name</param>
        /// <param name="password">Password for an encrypted archive</param>
        public SevenZipExtractor(string archiveFullName, string password)
            : base(password)
        {
            Init(archiveFullName);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveStream">The stream to read the archive from</param>
        /// <param name="password">Password for an encrypted archive</param>
        /// <remarks>The archive format is guessed by the signature.</remarks>
        public SevenZipExtractor(
            Stream archiveStream, string password)
            : base(password)
        {
            Init(archiveStream);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">The archive full file name</param>
        /// <param name="password">Password for an encrypted archive</param>
        /// <param name="reportErrors">Indicates whether to throw exceptions on archive errors</param>
        public SevenZipExtractor(string archiveFullName, string password, bool reportErrors)
            : base(password, reportErrors)
        {
            Init(archiveFullName);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveStream">The stream to read the archive from</param>
        /// <param name="password">Password for an encrypted archive</param>
        /// <param name="reportErrors">Indicates whether to throw exceptions on archive errors</param>
        /// <remarks>The archive format is guessed by the signature.</remarks>
        public SevenZipExtractor(
            Stream archiveStream, string password, bool reportErrors)
            : base(password, reportErrors)
        {
            Init(archiveStream);
        }
        #endregion

        /// <summary>
        /// Frees the SevenZipExtractor class by calling Dispose method.
        /// </summary>
        ~SevenZipExtractor()
        {
            Dispose();
        }

        #region Properties
        /// <summary>
        /// Gets or sets archive full file name
        /// </summary>
        public string FileName
        {
            get
            {
                return _FileName;
            }
        }
        /// <summary>
        /// Gets the size of the archive file
        /// </summary>
        public long PackedSize
        {
            get
            {
                return _PackedSize;
            }
        }
        /// <summary>
        /// Gets the size of unpacked archive data
        /// </summary>
        public long UnpackedSize
        {
            get
            {
                if (!_UnpackedSize.HasValue)
                {
                    return -1;
                }
                else
                {
                    return _UnpackedSize.Value;
                }
            }
        }
        /// <summary>
        /// Gets a value indicating whether the archive is solid
        /// </summary>
        public bool IsSolid
        {
            get
            {
                if (!_IsSolid.HasValue)
                {
                    GetArchiveInfo();
                }
                return _IsSolid.Value;
            }
        }
        /// <summary>
        /// Gets the number of files in the archive
        /// </summary>
        [CLSCompliantAttribute(false)]
        public uint FilesCount
        {
            get
            {
                if (!_FilesCount.HasValue)
                {
                    GetArchiveInfo();
                }
                return _FilesCount.Value;
            }
        }

        /// <summary>
        /// Gets archive format
        /// </summary>
        public InArchiveFormat Format
        {
            get
            {
                return _Format;
            }
        }
        #endregion

        private ArchiveOpenCallback GetArchiveOpenCallback()
        {
            if (_OpenCallback == null)
            {
                _OpenCallback = String.IsNullOrEmpty(Password) ?
                    new ArchiveOpenCallback(_FileName) :
                    new ArchiveOpenCallback(_FileName, Password);
            }
            return _OpenCallback;
        }
        #endif

        /// <summary>
        /// Checks if the specified stream supports extraction.
        /// </summary>
        /// <param name="stream">The stream to check.</param>
        private static void ValidateStream(Stream stream)
        {
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new ArgumentException("The specified stream can not seek or read.", "stream");
            }
            if (stream.Length == 0)
            {
                throw new ArgumentException("The specified stream has zero length.", "stream");
            }
        }

        #if UNMANAGED
        #region IDisposable Members

        /// <summary>
        /// Releases the unmanaged resources used by SevenZipExtractor
        /// </summary>
        public void Dispose()
        {
            if (_Opened)
            {
                try
                {
                    _Archive.Close();
                }
                catch (System.Runtime.InteropServices.InvalidComObjectException) { }
            }
            if (_OpenCallback != null)
            {
                _OpenCallback.Dispose();
            }
            if (_ArchiveStream != null)
            {
                if (_ArchiveStream is IDisposable)
                {
                    (_ArchiveStream as IDisposable).Dispose();
                }
            }
            if (!String.IsNullOrEmpty(_FileName))
            {
                SevenZipLibraryManager.FreeLibrary(this, _Format);
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ISevenZipExtractor Members

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
        /// Occurs when the archive has been unpacked
        /// </summary>
        public event EventHandler ExtractionFinished;
        /// <summary>
        /// Occurs when data are being extracted
        /// </summary>
        /// <remarks>Use this event for accurate progress handling and various ProgressBar.StepBy(e.PercentDelta) routines</remarks>
        public event EventHandler<ProgressEventArgs> Extracting;
        /// <summary>
        /// Occurs during the extraction when a file already exists
        /// </summary>
        public event EventHandler<FileOverwriteEventArgs> FileExists;

        private void OnExtractionFinished(EventArgs e)
        {
            if (ExtractionFinished != null)
            {
                ExtractionFinished(this, e);
            }
        }
        #endregion

        /// <summary>
        /// Performs basic archive consistence test
        /// </summary>
        public void Check()
        {
            try
            {
                IInStream ArchiveStream = GetArchiveStream();
                ulong CheckPos = 1 << 15;
                if (!_Opened)
                {
                    if (_Archive.Open(ArchiveStream, ref CheckPos, GetArchiveOpenCallback()) != 0)
                    {
                        throw new SevenZipArchiveException();
                    }
                }                
            }
            finally
            {
                _Archive.Close();
                _Opened = false;
            }
        }

        /// <summary>
        /// Gets the archive input stream.
        /// </summary>
        /// <returns>The archive input wrapper stream.</returns>
        private IInStream GetArchiveStream()
        {
            if (_ArchiveStream != null)
            {
                return _ArchiveStream;
            }
            
            if (_InStream != null)
            {
                _InStream.Seek(0, SeekOrigin.Begin);
                _ArchiveStream = new InStreamWrapper(_InStream, false);
            }
            else
            {
                if (!_FileName.EndsWith(".001", StringComparison.OrdinalIgnoreCase))
                {
                    _ArchiveStream = new InStreamWrapper(File.OpenRead(_FileName), true);
                }
                else
                {
                    _ArchiveStream = new InMultiStreamWrapper(_FileName);
                    _PackedSize = (_ArchiveStream as InMultiStreamWrapper).Length;
                }
            }            
            return _ArchiveStream;
        }

        /// <summary>
        /// Retrieves all information about the archive.
        /// </summary>
        /// <exception cref="SevenZip.SevenZipArchiveException"/>
        private void GetArchiveInfo()
        {           
            if (_Archive == null)
            {
                throw new SevenZipArchiveException();
            }
            else
            {
                IInStream ArchiveStream = GetArchiveStream();                
                ulong CheckPos = 1 << 15;
                if (!_Opened)
                {
                    if (_Archive.Open(ArchiveStream, ref CheckPos, GetArchiveOpenCallback()) !=
                        (int)OperationResult.Ok)
                    {
                        throw new SevenZipArchiveException();
                    }
                }
                _Opened = true;
                try
                {
                    _FilesCount = _Archive.GetNumberOfItems();
                    if (_FilesCount == 0)
                    {
                        throw new SevenZipArchiveException();
                    }
                    PropVariant Data = new PropVariant();
                    _ArchiveFileData = new List<ArchiveFileInfo>((int)_FilesCount);
                    #region Getting archive items data
                    for (uint i = 0; i < _FilesCount; i++)
                    {
                        try
                        {
                            ArchiveFileInfo fileInfo = new ArchiveFileInfo();
                            fileInfo.Index = i;
                            _Archive.GetProperty(i, ItemPropId.Path, ref Data);
                            fileInfo.FileName = NativeMethods.SafeCast<string>(Data, "[no name]");
                            _Archive.GetProperty(i, ItemPropId.LastWriteTime, ref Data);
                            fileInfo.LastWriteTime = NativeMethods.SafeCast<DateTime>(Data, DateTime.Now);
                            _Archive.GetProperty(i, ItemPropId.CreationTime, ref Data);
                            fileInfo.CreationTime = NativeMethods.SafeCast<DateTime>(Data, DateTime.Now);
                            _Archive.GetProperty(i, ItemPropId.LastAccessTime, ref Data);
                            fileInfo.LastAccessTime = NativeMethods.SafeCast<DateTime>(Data, DateTime.Now);                            
                            _Archive.GetProperty(i, ItemPropId.Size, ref Data);
                            fileInfo.Size = NativeMethods.SafeCast<ulong>(Data, 0);
                            _Archive.GetProperty(i, ItemPropId.Attributes, ref Data);
                            fileInfo.Attributes = NativeMethods.SafeCast<uint>(Data, 0);
                            _Archive.GetProperty(i, ItemPropId.IsFolder, ref Data);
                            fileInfo.IsDirectory = NativeMethods.SafeCast<bool>(Data, false);
                            _Archive.GetProperty(i, ItemPropId.Encrypted, ref Data);
                            fileInfo.Encrypted = NativeMethods.SafeCast<bool>(Data, false);
                            _Archive.GetProperty(i, ItemPropId.Crc, ref Data);
                            fileInfo.Crc = NativeMethods.SafeCast<uint>(Data, 0);
                            _Archive.GetProperty(i, ItemPropId.Comment, ref Data);
                            fileInfo.Comment = NativeMethods.SafeCast<string>(Data, "");
                            _ArchiveFileData.Add(fileInfo);
                        }
                        catch (InvalidCastException)
                        {
                            _ArchiveFileData = null;
                            throw new SevenZipArchiveException("probably archive is corrupted.");
                        }
                    }
                    #endregion
                    #region Getting archive properties
                    uint numProps = _Archive.GetNumberOfArchiveProperties();
                    List<ArchiveProperty> archProps = new List<ArchiveProperty>((int)numProps);
                    for (uint i = 0; i < numProps; i++)
                    {
                        string propName;
                        ItemPropId propId;
                        ushort varType;
                        _Archive.GetArchivePropertyInfo(i, out propName, out propId, out varType);
                        _Archive.GetArchiveProperty(propId, ref Data);
                        if (propId == ItemPropId.Solid)
                        {
                            _IsSolid = NativeMethods.SafeCast<bool>(Data, true);
                        }
                        // TODO Add more archive properties
                        if (PropIdToName.PropIdNames.ContainsKey(propId))
                        {
                            archProps.Add(new ArchiveProperty(PropIdToName.PropIdNames[propId], Data.Object));
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "An unknown archive property encountered (code " +
                                ((int)propId).ToString(CultureInfo.InvariantCulture) + ')');
                        }
                    }
                    _ArchiveProperties = new ReadOnlyCollection<ArchiveProperty>(archProps);
                    if (!_IsSolid.HasValue && _Format == InArchiveFormat.Zip)
                    {
                        _IsSolid = false;
                    }
                    if (!_IsSolid.HasValue)
                    {
                        _IsSolid = true;
                    }
                    #endregion
                }
                finally
                {                                
                    //_Archive.Close();
                }
                _ArchiveFileInfoCollection = new ReadOnlyCollection<ArchiveFileInfo>(_ArchiveFileData);                
            }
        }
        /// <summary>
        /// Produces an array of indexes from 0 to the maximum value in the specified array
        /// </summary>
        /// <param name="indexes">The source array</param>
        /// <returns>The array of indexes from 0 to the maximum value in the specified array</returns>
        private static uint[] SolidIndexes(uint[] indexes)
        {
            int max = 0;
            foreach (uint i in indexes)
            {
                max = Math.Max(max, (int)i);
            }
            if (max > 0)
            {
                max++;
                uint[] res = new uint[max];
                for (int i = 0; i < max; i++)
                {
                    res[i] = (uint)i;
                }
                return res;
            }
            else
            {
                return indexes;
            }
        }

        /// <summary>
        /// Gets the IArchiveExtractCallback callback
        /// </summary>
        /// <param name="directory">The directory where extract the files</param>
        /// <param name="filesCount">The number of files to be extracted</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        /// <returns>The ArchiveExtractCallback callback</returns>
        private ArchiveExtractCallback GetArchiveExtractCallback(string directory, int filesCount, List<uint> actualIndexes)
        {
            ArchiveExtractCallback aec = String.IsNullOrEmpty(Password) ?
                new ArchiveExtractCallback(_Archive, directory, filesCount, actualIndexes, this) :
                new ArchiveExtractCallback(_Archive, directory, filesCount, actualIndexes, Password, this);
            aec.Open += new EventHandler<OpenEventArgs>((s, e) => { _UnpackedSize = (long)e.TotalSize; });
            aec.FileExtractionStarted += FileExtractionStarted;
            aec.FileExtractionFinished += FileExtractionFinished;
            aec.Extracting += Extracting;
            aec.FileExists += FileExists;
            return aec;
        }

        /// <summary>
        /// Gets the IArchiveExtractCallback callback
        /// </summary>
        /// <param name="stream">The stream where extract the file</param>
        /// <param name="index">The file index</param>
        /// <param name="filesCount">The number of files to be extracted</param>
        /// <returns>The ArchiveExtractCallback callback</returns>
        private ArchiveExtractCallback GetArchiveExtractCallback(Stream stream, uint index, int filesCount)
        {
            ArchiveExtractCallback aec = String.IsNullOrEmpty(Password) ?
                new ArchiveExtractCallback(_Archive, stream, filesCount, index, this) :
                new ArchiveExtractCallback(_Archive, stream, filesCount, index, Password, this);
            aec.Open += new EventHandler<OpenEventArgs>((s, e) => { _UnpackedSize = (long)e.TotalSize; });
            aec.FileExtractionStarted += FileExtractionStarted;
            aec.FileExtractionFinished += FileExtractionFinished;
            aec.Extracting += Extracting;
            aec.FileExists += FileExists;
            return aec;
        }

        private void FreeArchiveExtractCallback(ArchiveExtractCallback callback)
        {
            callback.Open -= new EventHandler<OpenEventArgs>((s, e) => { _UnpackedSize = (long)e.TotalSize; });
            callback.FileExtractionStarted -= FileExtractionStarted;
            callback.FileExtractionFinished -= FileExtractionFinished;
            callback.Extracting -= Extracting;
            callback.FileExists -= FileExists;
        }

        /// <summary>
        /// Gets the collection of ArchiveFileInfo with all information about files in the archive
        /// </summary>
        public ReadOnlyCollection<ArchiveFileInfo> ArchiveFileData
        {
            get
            {
                if (_ArchiveFileData == null)
                {
                    GetArchiveInfo();
                }
                return _ArchiveFileInfoCollection;
            }
        }
        /// <summary>
        /// Gets the properties for the current archive
        /// </summary>
        public ReadOnlyCollection<ArchiveProperty> ArchiveProperties
        {
            get
            {
                if (_ArchiveProperties == null)
                {
                    GetArchiveInfo();
                }
                return _ArchiveProperties;
            }
        }
        /// <summary>
        /// Gets the collection of all file names contained in the archive.
        /// </summary>
        /// <remarks>
        /// Each get recreates the collection
        /// </remarks>
        public ReadOnlyCollection<string> ArchiveFileNames
        {
            get
            {
                if (_ArchiveFileData == null)
                {
                    GetArchiveInfo();
                }
                List<string> fileNames = new List<string>(_ArchiveFileData.Count);
                foreach (ArchiveFileInfo afi in _ArchiveFileData)
                {
                    fileNames.Add(afi.FileName);
                }
                return new ReadOnlyCollection<string>(fileNames);
            }
        }
        
        /// <summary>
        /// Unpacks the file by its name to the specified stream
        /// </summary>
        /// <param name="fileName">The file full name in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(string fileName, Stream stream)
        {
            ExtractFile(fileName, stream, false);
        }
        /// <summary>
        /// Unpacks the file by its name to the specified stream
        /// </summary>
        /// <param name="fileName">The file full name in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(string fileName, Stream stream, bool reportErrors)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            int index = -1;
            foreach (ArchiveFileInfo afi in _ArchiveFileData)
            {
                if (afi.FileName == fileName && !afi.IsDirectory)
                {
                    index = (int)afi.Index;
                    break;
                }
            }
            if (index == -1)
            {
                if (reportErrors)
                {
                    throw new ArgumentOutOfRangeException("fileName", "The specified file name was not found in the archive file table.");
                }
                else
                {
                    return;
                }
            }
            else
            {
                ExtractFile((uint)index, stream, reportErrors);
            }
        }
        /// <summary>
        /// Unpacks the file by its index to the specified stream
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(uint index, Stream stream)
        {
            ExtractFile(index, stream, false);
        }
        /// <summary>
        /// Unpacks the file by its index to the specified stream
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(uint index, Stream stream, bool reportErrors)
        {
            if (!stream.CanWrite)
            {
                if (reportErrors)
                {
                    throw new ArgumentException("The specified stream can not be written.", "stream");
                }
                else
                {
                    return;
                }
            }
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            if (index > _FilesCount - 1)
            {
                if (reportErrors)
                {
                    throw new ArgumentOutOfRangeException("index", "The specified index is greater than the archive files count.");
                }
                else
                {
                    return;
                }
            }
            uint[] indexes = new uint[] {index};
            if (_IsSolid.Value)
            {
                indexes = SolidIndexes(indexes);
            }
            try
            {
                IInStream ArchiveStream = GetArchiveStream();
                ulong CheckPos = 1 << 15;               
                if (!_Opened)
                {
                    if (_Archive.Open(ArchiveStream, ref CheckPos, GetArchiveOpenCallback()) != 0
                        && reportErrors)
                    {
                        throw new SevenZipArchiveException();
                    }
                    _Opened = true;
                }
                using (ArchiveExtractCallback aec = GetArchiveExtractCallback(stream, index, indexes.Length))
                {
                    try
                    {

                        CheckedExecute(
                            _Archive.Extract(indexes, (uint)indexes.Length, 0, aec),
                            SevenZipExtractionFailedException.DefaultMessage);
                    }
                    catch (ExtractionFailedException)
                    {
                        if (reportErrors)
                        {
                            throw;
                        }
                    }
                    catch (SevenZipException e)
                    {
                        if (reportErrors && !Cancelled)
                        {
                            throw new ExtractionFailedException(e.Message);
                        }
                    }
                    finally
                    {
                        FreeArchiveExtractCallback(aec);
                        GC.Collect();
                    }
                }                
                OnExtractionFinished(EventArgs.Empty);
            }            
            catch (ExtractionFailedException)
            {
                if (reportErrors)
                {
                    throw;
                }
            }
            finally
            {
                //_Archive.Close();
            }
        }
        /// <summary>
        /// Unpacks the file by its index to the specified directory
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(uint index, string directory)
        {
            ExtractFiles(new uint[] { index }, directory, ReportErrors);
        }
        /// <summary>
        /// Unpacks the file by its index to the specified directory
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(uint index, string directory, bool reportErrors)
        {
            ExtractFiles(new uint[] { index }, directory, reportErrors);
        }
        /// <summary>
        /// Unpacks the file by its full name to the specified directory
        /// </summary>
        /// <param name="fileName">The file full name in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        public void ExtractFile(string fileName, string directory)
        {
            ExtractFiles(new string[] { fileName }, directory, ReportErrors);
        }
        /// <summary>
        /// Unpacks the file by its full name to the specified directory
        /// </summary>
        /// <param name="fileName">File full name in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        public void ExtractFile(string fileName, string directory, bool reportErrors)
        {
            ExtractFiles(new string[] { fileName }, directory, reportErrors);
        }
        /// <summary>
        /// Unpacks files by their indexes to the specified directory
        /// </summary>
        /// <param name="indexes">indexes of the files in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFiles(uint[] indexes, string directory)
        {
            ExtractFiles(indexes, directory, ReportErrors);
        }
        /// <summary>
        /// Unpacks files by their indexes to the specified directory
        /// </summary>
        /// <param name="indexes">indexes of the files in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFiles(uint[] indexes, string directory, bool reportErrors)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            #region indexes validation
            foreach (uint i in indexes)
            {
                if (i >= _FilesCount && reportErrors)
                {
                    throw new ArgumentOutOfRangeException(
                        "indexes",
                        "Index must be less than " + _FilesCount.Value.ToString(CultureInfo.InvariantCulture) + "!");
                }
            }
            #endregion
            List<uint> origIndexes = new List<uint>(indexes);
            origIndexes.Sort();
            indexes = origIndexes.ToArray();
            if (_IsSolid.Value)
            {
                indexes = SolidIndexes(indexes);
            }
            try
            {
                IInStream ArchiveStream = GetArchiveStream();                
                ulong CheckPos = 1 << 15;
                if (!_Opened)
                {
                    if (_Archive.Open(ArchiveStream, ref CheckPos, GetArchiveOpenCallback()) !=
                        (int)OperationResult.Ok
                        && reportErrors)
                    {
                        throw new SevenZipArchiveException();
                    }
                    _Opened = true;
                }
                using (ArchiveExtractCallback aec = GetArchiveExtractCallback(directory, indexes.Length, origIndexes))
                {
                    try
                    {

                        CheckedExecute(
                            _Archive.Extract(indexes, (uint)indexes.Length, 0, aec),
                            SevenZipExtractionFailedException.DefaultMessage);

                    }
                    catch (ExtractionFailedException)
                    {
                        if (reportErrors)
                        {
                            throw;
                        }
                    }
                    catch (SevenZipException e)
                    {
                        if (reportErrors && !Cancelled)
                        {
                            throw new ExtractionFailedException(e.Message);
                        }
                    }
                    finally
                    {
                        FreeArchiveExtractCallback(aec);
                        GC.Collect();
                    }
                }               
                OnExtractionFinished(EventArgs.Empty);
            }
            
            catch (ExtractionFailedException)
            {
                if (reportErrors)
                {
                    throw;
                }
            }
            finally
            {
                //_Archive.Close();
            }
        }
        /// <summary>
        /// Unpacks files by their full names to the specified directory
        /// </summary>
        /// <param name="fileNames">Full file names in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        public void ExtractFiles(string[] fileNames, string directory)
        {
            ExtractFiles(fileNames, directory, ReportErrors);
        }
        /// <summary>
        /// Unpacks files by their full names to the specified directory
        /// </summary>
        /// <param name="fileNames">Full file names in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        public void ExtractFiles(string[] fileNames, string directory, bool reportErrors)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            List<uint> indexes = new List<uint>(fileNames.Length);
            List<string> archiveFileNames = new List<string>(ArchiveFileNames);
            foreach (string fn in fileNames)
            {
                if (!archiveFileNames.Contains(fn) && reportErrors)
                {
                    throw new ArgumentOutOfRangeException("fileNames", "File \"" + fn + "\" was not found in the archive file table.");
                }
                else
                {
                    foreach (ArchiveFileInfo afi in _ArchiveFileData)
                    {
                        if (afi.FileName == fn && !afi.IsDirectory)
                        {
                            indexes.Add(afi.Index);
                            break;
                        }
                    }
                }
            }
            ExtractFiles(indexes.ToArray(), directory, reportErrors);
        }

        /// <summary>
        /// Extracts files for the archive, giving a callback the choice what
        /// to do with each file. The order of the files is given by the archive.
        /// </summary>
        /// <param name="extractFileCallback">The callback to call for each file in the archive.</param>
        public void ExtractFiles(ExtractFileCallback extractFileCallback)
        {
            if (IsSolid)
            {
                // solid strategy
            }
            else
            {
                foreach (ArchiveFileInfo archiveFileInfo in ArchiveFileData)
                {
                    ExtractFileCallbackArgs extractFileCallbackArgs = new ExtractFileCallbackArgs(archiveFileInfo);
                    extractFileCallback(extractFileCallbackArgs);
                    if (extractFileCallbackArgs.CancelExtraction) { break; }
                    if (extractFileCallbackArgs.ExtractToStream != null || extractFileCallbackArgs.ExtractToFile != null)
                    {
                        bool callDone = false;
                        try
                        {
                            if (extractFileCallbackArgs.ExtractToStream != null)
                            {
                                ExtractFile(archiveFileInfo.Index, extractFileCallbackArgs.ExtractToStream);
                            }
                            else
                            {
                                using (FileStream file = new FileStream(extractFileCallbackArgs.ExtractToFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan))
                                {
                                    ExtractFile(archiveFileInfo.Index, file);
                                }
                            }
                            callDone = true;
                        }
                        catch (Exception ex)
                        {
                            extractFileCallbackArgs.Exception = ex;
                            extractFileCallbackArgs.Reason = ExtractFileCallbackReason.Failure;
                            extractFileCallback(extractFileCallbackArgs);
                            if (extractFileCallbackArgs.Exception != null) { throw; }
                        }
                        if (callDone)
                        {
                            extractFileCallbackArgs.Reason = ExtractFileCallbackReason.Done;
                            extractFileCallback(extractFileCallbackArgs);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unpacks the whole archive to the specified directory
        /// </summary>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        public void ExtractArchive(string directory, bool reportErrors)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            try
            {
                IInStream ArchiveStream = GetArchiveStream();                
                ulong CheckPos = 1 << 15;
                if (!_Opened)
                {
                    if (_Archive.Open(ArchiveStream, ref CheckPos, GetArchiveOpenCallback()) !=
                        (int)OperationResult.Ok
                        && reportErrors)
                    {
                        throw new SevenZipArchiveException();
                    }
                }
                _Opened = true;
                using (ArchiveExtractCallback aec = GetArchiveExtractCallback(directory, (int)_FilesCount, null))
                {
                    try
                    {

                        CheckedExecute(
                            _Archive.Extract(null, UInt32.MaxValue, 0, aec),
                            SevenZipExtractionFailedException.DefaultMessage);
                        OnExtractionFinished(EventArgs.Empty);
                    }
                    catch (ExtractionFailedException)
                    {
                        if (reportErrors)
                        {
                            throw;
                        }
                    }
                    catch (SevenZipException e)
                    {
                        if (reportErrors && !Cancelled)
                        {
                            throw new ExtractionFailedException(e.Message);
                        }
                    }
                    finally
                    {
                        FreeArchiveExtractCallback(aec);
                    }
                }               
            }            
            catch (ExtractionFailedException)
            {
                if (reportErrors)
                {
                    throw;
                }
            }
            finally
            {
                _Archive.Close();
                _Opened = false;
            }
        }

        /// <summary>
        /// Unpacks the whole archive to the specified directory
        /// </summary>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        public void ExtractArchive(string directory)
        {
            ExtractArchive(directory, ReportErrors);
        }

        #endregion
        #endif

        #region LZMA SDK functions
        internal static byte[] GetLzmaProperties(Stream inStream, out long outSize)
        {
            byte[] LZMAproperties = new byte[5];
            if (inStream.Read(LZMAproperties, 0, 5) != 5)
            {
                throw new LzmaException();
            }
            outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int b = inStream.ReadByte();
                if (b < 0)
                {
                    throw new LzmaException();
                }
                outSize |= ((long)(byte)b) << (i << 3);
            }
            return LZMAproperties;
        }

        /// <summary>
        /// Decompress the specified stream (C# inside)
        /// </summary>
        /// <param name="inStream">The source compressed stream</param>
        /// <param name="outStream">The destination uncompressed stream</param>
        /// <param name="inLength">The length of compressed data (null for inStream.Length)</param>
        /// <param name="codeProgressEvent">The event for handling the code progress</param>
        public static void DecompressStream(Stream inStream, Stream outStream, int? inLength, EventHandler<ProgressEventArgs> codeProgressEvent)
        {
            if (!inStream.CanRead || !outStream.CanWrite)
            {
                throw new ArgumentException("The specified streams are invalid.");
            }
            Decoder decoder = new Decoder();
            long inSize, outSize;
            inSize = (inLength.HasValue ? inLength.Value : inStream.Length) - inStream.Position;
            decoder.SetDecoderProperties(GetLzmaProperties(inStream, out outSize));
            decoder.Code(
                inStream, outStream, inSize, outSize,
                new LzmaProgressCallback(inSize, codeProgressEvent));
        }

        /// <summary>
        /// Decompress byte array compressed with LZMA algorithm (C# inside)
        /// </summary>
        /// <param name="data">Byte array to decompress</param>
        /// <returns>Decompressed byte array</returns>
        public static byte[] ExtractBytes(byte[] data)
        {
            using (MemoryStream inStream = new MemoryStream(data))
            {
                Decoder decoder = new Decoder();
                inStream.Seek(0, 0);
                using (MemoryStream outStream = new MemoryStream())
                {
                    long outSize;
                    decoder.SetDecoderProperties(GetLzmaProperties(inStream, out outSize));
                    decoder.Code(inStream, outStream, inStream.Length - inStream.Position, outSize, null);
                    return outStream.ToArray();
                }
            }
        }
        #endregion
    }        

}
