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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SevenZip.Sdk.Compression.Lzma;

namespace SevenZip
{
    /// <summary>
    /// Class for extracting and getting information about 7-zip archives
    /// </summary>
    public sealed class SevenZipExtractor
#if UNMANAGED
        : SevenZipBase, IDisposable
#endif
    {
#if UNMANAGED
        private List<ArchiveFileInfo> _ArchiveFileData;
        private IInArchive _Archive;
        private IInStream _ArchiveStream;
        private ArchiveOpenCallback _OpenCallback;
        private string _FileName;
        private Stream _InStream;
        private long? _PackedSize;
        private long? _UnpackedSize;
        private uint? _FilesCount;
        private bool? _IsSolid;
        private bool _Opened;
        private bool _Disposed;
        private InArchiveFormat _Format;
        private ReadOnlyCollection<ArchiveFileInfo> _ArchiveFileInfoCollection;
        private ReadOnlyCollection<ArchiveProperty> _ArchiveProperties;

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
            _FileName = archiveFullName;
            _Format = FileChecker.CheckSignature(archiveFullName);
            SevenZipLibraryManager.LoadLibrary(this, _Format);
            try
            {
                _Archive = SevenZipLibraryManager.InArchive(_Format, this);
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
        /// <param name="archiveStream">The stream to read the archive from</param>
        /// <remarks>The archive format is guessed by the signature.</remarks>
        public SevenZipExtractor(Stream archiveStream)
        {
            Init(archiveStream);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">The archive full file name</param>
        public SevenZipExtractor(string archiveFullName)
        {
            Init(archiveFullName);
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

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets archive full file name
        /// </summary>
        public string FileName
        {
            get
            {
                DisposedCheck();
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
                DisposedCheck();
                return _PackedSize.HasValue
                           ?
                               _PackedSize.Value
                           :
                               _FileName != null
                                   ?
                                       (new FileInfo(_FileName)).Length
                                   :
                                       -1;
            }
        }

        /// <summary>
        /// Gets the size of unpacked archive data
        /// </summary>
        public long UnpackedSize
        {
            get
            {
                DisposedCheck();
                if (!_UnpackedSize.HasValue)
                {
                    return -1;
                }
                return _UnpackedSize.Value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the archive is solid
        /// </summary>
        public bool IsSolid
        {
            get
            {
                DisposedCheck();
                if (!_IsSolid.HasValue)
                {
                    GetArchiveInfo(true);
                }
                return _IsSolid.Value;
            }
        }

        /// <summary>
        /// Gets the number of files in the archive
        /// </summary>
        [CLSCompliant(false)]
        public uint FilesCount
        {
            get
            {
                DisposedCheck();
                if (!_FilesCount.HasValue)
                {
                    GetArchiveInfo(true);
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
                DisposedCheck();
                return _Format;
            }
        }

        #endregion

        private ArchiveOpenCallback GetArchiveOpenCallback()
        {
            if (_OpenCallback == null)
            {
                _OpenCallback = String.IsNullOrEmpty(Password)
                                    ?
                                        new ArchiveOpenCallback(_FileName)
                                    :
                                        new ArchiveOpenCallback(_FileName, Password);
            }
            return _OpenCallback;
        }

        /// <summary>
        /// Checked whether the class was disposed.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException" />
        private void DisposedCheck()
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("SevenZipExtractor");
            }
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
        /// Releases the unmanaged resources used by SevenZipExtractor.
        /// </summary>
        public void Dispose()
        {
            if (!_Disposed)
            {
                if (_Opened)
                {
                    try
                    {
                        _Archive.Close();
                        _Archive = null;
                        _ArchiveFileData = null;
                        _ArchiveProperties = null;
                        _ArchiveFileInfoCollection = null;
                        _InStream = null;
                    }
                    catch (InvalidComObjectException)
                    {
                    }
                }
                if (_OpenCallback != null)
                {
                    try
                    {
                        _OpenCallback.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    _OpenCallback = null;
                }
                if (_ArchiveStream != null)
                {
                    if (_ArchiveStream is IDisposable)
                    {
                        try
                        {
                            (_ArchiveStream as IDisposable).Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        _ArchiveStream = null;
                    }
                }
                if (!String.IsNullOrEmpty(_FileName))
                {
                    SevenZipLibraryManager.FreeLibrary(this, _Format);
                }
            }
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Core public Members

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
        /// Performs archive integrity test.
        /// </summary>
        public void Check()
        {
            DisposedCheck();
            try
            {
                InitArchiveFileData(false);
                IInStream archiveStream = GetArchiveStream(false);
                ArchiveOpenCallback openCallback = GetArchiveOpenCallback();

                ulong checkPos = 1 << 15;
                if (!_Opened)
                {
                    if (_Archive.Open(archiveStream, ref checkPos, openCallback) != 0)
                    {
                        if (!ThrowException(null, new SevenZipArchiveException()))
                        {
                            return;
                        }
                    }
                }
                _Opened = true;
                using (ArchiveExtractCallback aec = GetArchiveExtractCallback("", (int) _FilesCount, null))
                {
                    try
                    {
                        CheckedExecute(
                            _Archive.Extract(null, UInt32.MaxValue, 1, aec),
                            SevenZipExtractionFailedException.DefaultMessage, aec);
                    }
                    finally
                    {
                        FreeArchiveExtractCallback(aec);
                    }
                }
            }
            finally
            {
                _ArchiveStream = null;
                _Archive.Close();
                _Opened = false;
            }
        }

        /// <summary>
        /// Gets the archive input stream.
        /// </summary>
        /// <returns>The archive input wrapper stream.</returns>
        private IInStream GetArchiveStream(bool dispose)
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
                    _ArchiveStream = new InStreamWrapper(
                        new FileStream(_FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                        dispose);
                }
                else
                {
                    _ArchiveStream = new InMultiStreamWrapper(_FileName, dispose);
                    _PackedSize = (_ArchiveStream as InMultiStreamWrapper).Length;
                }
            }
            return _ArchiveStream;
        }

        /// <summary>
        /// Retrieves all information about the archive.
        /// </summary>
        /// <exception cref="SevenZip.SevenZipArchiveException"/>
        private void GetArchiveInfo(bool disposeStream)
        {
            if (_Archive == null)
            {
                if (!ThrowException(null, new SevenZipArchiveException()))
                {
                    return;
                }
            }
            else
            {
                IInStream archiveStream;
                using ((archiveStream = GetArchiveStream(disposeStream)) as IDisposable)
                {
                    ArchiveOpenCallback openCallback = GetArchiveOpenCallback();
                    ulong checkPos = 1 << 15;
                    if (!_Opened)
                    {
                        if (_Archive.Open(archiveStream, ref checkPos, openCallback) !=
                            (int) OperationResult.Ok)
                        {
                            if (!ThrowException(null, new SevenZipArchiveException()))
                            {
                                return;
                            }
                        }
                        _Opened = !disposeStream;
                    }
                    _FilesCount = _Archive.GetNumberOfItems();
                    if (_FilesCount == 0)
                    {
                        if (!ThrowException(null, new SevenZipArchiveException()))
                        {
                            return;
                        }
                    }
                    var data = new PropVariant();
                    _ArchiveFileData = new List<ArchiveFileInfo>((int) _FilesCount);

                    #region Getting archive items data

                    for (uint i = 0; i < _FilesCount; i++)
                    {
                        try
                        {
                            var fileInfo = new ArchiveFileInfo {Index = (int) i};
                            _Archive.GetProperty(i, ItemPropId.Path, ref data);
                            fileInfo.FileName = NativeMethods.SafeCast(data, "[no name]");
                            _Archive.GetProperty(i, ItemPropId.LastWriteTime, ref data);
                            fileInfo.LastWriteTime = NativeMethods.SafeCast(data, DateTime.Now);
                            _Archive.GetProperty(i, ItemPropId.CreationTime, ref data);
                            fileInfo.CreationTime = NativeMethods.SafeCast(data, DateTime.Now);
                            _Archive.GetProperty(i, ItemPropId.LastAccessTime, ref data);
                            fileInfo.LastAccessTime = NativeMethods.SafeCast(data, DateTime.Now);
                            _Archive.GetProperty(i, ItemPropId.Size, ref data);
                            fileInfo.Size = NativeMethods.SafeCast<ulong>(data, 0);
                            _Archive.GetProperty(i, ItemPropId.Attributes, ref data);
                            fileInfo.Attributes = NativeMethods.SafeCast<uint>(data, 0);
                            _Archive.GetProperty(i, ItemPropId.IsDirectory, ref data);
                            fileInfo.IsDirectory = NativeMethods.SafeCast(data, false);
                            _Archive.GetProperty(i, ItemPropId.Encrypted, ref data);
                            fileInfo.Encrypted = NativeMethods.SafeCast(data, false);
                            _Archive.GetProperty(i, ItemPropId.Crc, ref data);
                            fileInfo.Crc = NativeMethods.SafeCast<uint>(data, 0);
                            _Archive.GetProperty(i, ItemPropId.Comment, ref data);
                            fileInfo.Comment = NativeMethods.SafeCast(data, "");
                            _ArchiveFileData.Add(fileInfo);
                        }
                        catch (InvalidCastException)
                        {
                            ThrowException(null, new SevenZipArchiveException("probably archive is corrupted."));
                        }
                    }

                    #endregion

                    #region Getting archive properties

                    uint numProps = _Archive.GetNumberOfArchiveProperties();
                    var archProps = new List<ArchiveProperty>((int) numProps);
                    for (uint i = 0; i < numProps; i++)
                    {
                        string propName;
                        ItemPropId propId;
                        ushort varType;
                        _Archive.GetArchivePropertyInfo(i, out propName, out propId, out varType);
                        _Archive.GetArchiveProperty(propId, ref data);
                        if (propId == ItemPropId.Solid)
                        {
                            _IsSolid = NativeMethods.SafeCast(data, true);
                        }
                        // TODO Add more archive properties
                        if (PropIdToName.PropIdNames.ContainsKey(propId))
                        {
                            archProps.Add(new ArchiveProperty
                                              {
                                                  Name = PropIdToName.PropIdNames[propId],
                                                  Value = data.Object
                                              });
                        }
                        else
                        {
                            Debug.WriteLine(
                                "An unknown archive property encountered (code " +
                                ((int) propId).ToString(CultureInfo.InvariantCulture) + ')');
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
                if (disposeStream)
                {
                    _Archive.Close();
                    _ArchiveStream = null;
                }
                _ArchiveFileInfoCollection = new ReadOnlyCollection<ArchiveFileInfo>(_ArchiveFileData);
            }
        }

        /// <summary>
        /// Ensure that _ArchiveFileData is loaded.
        /// </summary>
        /// <param name="disposeStream">Dispose the archive stream after this operation.</param>
        private void InitArchiveFileData(bool disposeStream)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo(disposeStream);
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
                max = Math.Max(max, (int) i);
            }
            if (max > 0)
            {
                max++;
                var res = new uint[max];
                for (int i = 0; i < max; i++)
                {
                    res[i] = (uint) i;
                }
                return res;
            }
            return indexes;
        }

        /// <summary>
        /// Checkes whether all the indexes are valid.
        /// </summary>
        /// <param name="indexes">The indexes to check.</param>
        /// <returns>True is valid; otherwise, false.</returns>
        private static bool CheckIndexes(params int[] indexes)
        {
            bool res = true;
            foreach (int i in indexes)
            {
                if (i < 0)
                {
                    res = false;
                    break;
                }
            }
            return res;
        }

        /// <summary>
        /// Gets the IArchiveExtractCallback callback
        /// </summary>
        /// <param name="directory">The directory where extract the files</param>
        /// <param name="filesCount">The number of files to be extracted</param>
        /// <param name="actualIndexes">The list of actual indexes (solid archives support)</param>
        /// <returns>The ArchiveExtractCallback callback</returns>
        private ArchiveExtractCallback GetArchiveExtractCallback(string directory, int filesCount,
                                                                 List<uint> actualIndexes)
        {
            ArchiveExtractCallback aec = String.IsNullOrEmpty(Password)
                                             ?
                                                 new ArchiveExtractCallback(_Archive, directory, filesCount,
                                                                            actualIndexes, this)
                                             :
                                                 new ArchiveExtractCallback(_Archive, directory, filesCount,
                                                                            actualIndexes, Password, this);
            aec.Open += ((s, e) => { _UnpackedSize = (long) e.TotalSize; });
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
            ArchiveExtractCallback aec = String.IsNullOrEmpty(Password)
                                             ?
                                                 new ArchiveExtractCallback(_Archive, stream, filesCount, index, this)
                                             :
                                                 new ArchiveExtractCallback(_Archive, stream, filesCount, index,
                                                                            Password, this);
            aec.Open += ((s, e) => { _UnpackedSize = (long) e.TotalSize; });
            aec.FileExtractionStarted += FileExtractionStarted;
            aec.FileExtractionFinished += FileExtractionFinished;
            aec.Extracting += Extracting;
            aec.FileExists += FileExists;
            return aec;
        }

        private void FreeArchiveExtractCallback(ArchiveExtractCallback callback)
        {
            callback.Open -= ((s, e) => { _UnpackedSize = (long) e.TotalSize; });
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
                DisposedCheck();
                InitArchiveFileData(true);
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
                DisposedCheck();
                if (_ArchiveProperties == null)
                {
                    GetArchiveInfo(true);
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
                DisposedCheck();
                InitArchiveFileData(true);
                var fileNames = new List<string>(_ArchiveFileData.Count);
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
        /// <param name="fileName">The file full name in the archive file table.</param>
        /// <param name="stream">The stream where the file is to be unpacked.</param>
        public void ExtractFile(string fileName, Stream stream)
        {
            DisposedCheck();
            InitArchiveFileData(false);
            int index = -1;
            foreach (ArchiveFileInfo afi in _ArchiveFileData)
            {
                if (afi.FileName == fileName && !afi.IsDirectory)
                {
                    index = afi.Index;
                    break;
                }
            }
            if (index == -1)
            {
                if (!ThrowException(null, new ArgumentOutOfRangeException(
                                              "fileName",
                                              "The specified file name was not found in the archive file table.")))
                {
                    return;
                }
            }
            else
            {
                ExtractFile(index, stream);
            }
        }

        /// <summary>
        /// Unpacks the file by its index to the specified stream
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        public void ExtractFile(int index, Stream stream)
        {
            DisposedCheck();
            ClearExceptions();
            if (!CheckIndexes(index))
            {
                if (!ThrowException(null, new ArgumentException("The index must be more or equal to zero.", "index")))
                {
                    return;
                }
            }
            if (!stream.CanWrite)
            {
                if (!ThrowException(null, new ArgumentException("The specified stream can not be written.", "stream")))
                {
                    return;
                }
            }
            InitArchiveFileData(false);
            if (index > _FilesCount - 1)
            {
                if (!ThrowException(null, new ArgumentOutOfRangeException(
                                              "index", "The specified index is greater than the archive files count.")))
                {
                    return;
                }
            }
            var indexes = new[] {(uint) index};
            if (_IsSolid.Value)
            {
                indexes = SolidIndexes(indexes);
            }
            IInStream archiveStream = GetArchiveStream(false);
            ArchiveOpenCallback openCallback = GetArchiveOpenCallback();

            ulong checkPos = 1 << 15;
            if (!_Opened)
            {
                if (_Archive.Open(archiveStream, ref checkPos, openCallback) != 0)
                {
                    ThrowException(null, new SevenZipArchiveException());
                }
                _Opened = true;
            }
            using (ArchiveExtractCallback aec = GetArchiveExtractCallback(stream, (uint) index, indexes.Length))
            {
                try
                {
                    CheckedExecute(
                        _Archive.Extract(indexes, (uint) indexes.Length, 0, aec),
                        SevenZipExtractionFailedException.DefaultMessage, aec);
                }
                finally
                {
                    FreeArchiveExtractCallback(aec);
                }
            }
            OnExtractionFinished(EventArgs.Empty);
            ThrowUserException();
        }

        /// <summary>
        /// Unpacks files by their indexes to the specified directory
        /// </summary>
        /// <param name="indexes">indexes of the files in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        public void ExtractFiles(string directory, params int[] indexes)
        {
            DisposedCheck();
            ClearExceptions();
            if (!CheckIndexes(indexes))
            {
                if (
                    !ThrowException(null, new ArgumentException("The indexes must be more or equal to zero.", "indexes")))
                {
                    return;
                }
            }
            InitArchiveFileData(false);

            #region Indexes stuff

            var uindexes = new uint[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                uindexes[i] = (uint) indexes[i];
            }
            foreach (uint i in uindexes)
            {
                if (i >= _FilesCount)
                {
                    if (!ThrowException(
                             null, new ArgumentOutOfRangeException("indexes",
                                                                   "Index must be less than " +
                                                                   _FilesCount.Value.ToString(
                                                                       CultureInfo.InvariantCulture) + "!")))
                    {
                        return;
                    }
                }
            }
            var origIndexes = new List<uint>(uindexes);
            origIndexes.Sort();
            uindexes = origIndexes.ToArray();
            if (_IsSolid.Value)
            {
                uindexes = SolidIndexes(uindexes);
            }

            #endregion

            try
            {
                IInStream archiveStream;
                using ((archiveStream = GetArchiveStream(origIndexes.Count != 1)) as IDisposable)
                {
                    ArchiveOpenCallback openCallback = GetArchiveOpenCallback();

                    ulong checkPos = 1 << 15;
                    if (!_Opened)
                    {
                        if (_Archive.Open(archiveStream, ref checkPos, openCallback) != (int) OperationResult.Ok)
                        {
                            if (!ThrowException(null, new SevenZipArchiveException()))
                            {
                                return;
                            }
                        }
                        _Opened = true;
                    }
                    using (
                        ArchiveExtractCallback aec = GetArchiveExtractCallback(directory, (int) _FilesCount, origIndexes)
                        )
                    {
                        try
                        {
                            CheckedExecute(
                                _Archive.Extract(uindexes, (uint) uindexes.Length, 0, aec),
                                SevenZipExtractionFailedException.DefaultMessage, aec);
                        }
                        finally
                        {
                            FreeArchiveExtractCallback(aec);
                        }
                    }
                }
                OnExtractionFinished(EventArgs.Empty);
            }
            finally
            {
                if (origIndexes.Count > 1)
                {
                    _Archive.Close();
                    _ArchiveStream = null;
                    _Opened = false;
                }
            }
            ThrowUserException();
        }

        /// <summary>
        /// Unpacks files by their full names to the specified directory
        /// </summary>
        /// <param name="fileNames">Full file names in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        public void ExtractFiles(string directory, params string[] fileNames)
        {
            DisposedCheck();
            InitArchiveFileData(false);
            var indexes = new List<int>(fileNames.Length);
            var archiveFileNames = new List<string>(ArchiveFileNames);
            foreach (string fn in fileNames)
            {
                if (!archiveFileNames.Contains(fn))
                {
                    if (
                        !ThrowException(null,
                                        new ArgumentOutOfRangeException("fileNames",
                                                                        "File \"" + fn +
                                                                        "\" was not found in the archive file table.")))
                    {
                        return;
                    }
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
            ExtractFiles(directory, indexes.ToArray());
        }

        /// <summary>
        /// Extracts files for the archive, giving a callback the choice what
        /// to do with each file. The order of the files is given by the archive.
        /// </summary>
        /// <param name="extractFileCallback">The callback to call for each file in the archive.</param>
        public void ExtractFiles(ExtractFileCallback extractFileCallback)
        {
            DisposedCheck();
            InitArchiveFileData(false);
            if (IsSolid)
            {
                // solid strategy
            }
            else
            {
                foreach (ArchiveFileInfo archiveFileInfo in ArchiveFileData)
                {
                    var extractFileCallbackArgs = new ExtractFileCallbackArgs(archiveFileInfo);
                    extractFileCallback(extractFileCallbackArgs);
                    if (extractFileCallbackArgs.CancelExtraction)
                    {
                        break;
                    }
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
                                using (
                                    var file = new FileStream(extractFileCallbackArgs.ExtractToFile, FileMode.CreateNew,
                                                              FileAccess.Write, FileShare.None, 8192,
                                                              FileOptions.SequentialScan))
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
                            if (!ThrowException(null, ex))
                            {
                                return;
                            }
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
        public void ExtractArchive(string directory)
        {
            DisposedCheck();
            ClearExceptions();
            InitArchiveFileData(false);
            try
            {
                IInStream archiveStream;
                using ((archiveStream = GetArchiveStream(true)) as IDisposable)
                {
                    ArchiveOpenCallback openCallback = GetArchiveOpenCallback();
                    ulong checkPos = 1 << 15;
                    if (!_Opened)
                    {
                        if (_Archive.Open(archiveStream, ref checkPos, openCallback) != (int) OperationResult.Ok)
                        {
                            if (!ThrowException(null, new SevenZipArchiveException()))
                            {
                                return;
                            }
                        }
                    }
                    _Opened = true;
                    using (ArchiveExtractCallback aec = GetArchiveExtractCallback(directory, (int) _FilesCount, null))
                    {
                        try
                        {
                            CheckedExecute(
                                _Archive.Extract(null, UInt32.MaxValue, 0, aec),
                                SevenZipExtractionFailedException.DefaultMessage, aec);
                            OnExtractionFinished(EventArgs.Empty);
                        }
                        finally
                        {
                            FreeArchiveExtractCallback(aec);
                        }
                    }
                }
            }
            finally
            {
                _Archive.Close();
                _ArchiveStream = null;
                _Opened = false;
            }
            ThrowUserException();
        }

        #endregion

#endif

        #region LZMA SDK functions

        internal static byte[] GetLzmaProperties(Stream inStream, out long outSize)
        {
            var lzmAproperties = new byte[5];
            if (inStream.Read(lzmAproperties, 0, 5) != 5)
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
                outSize |= ((long) (byte) b) << (i << 3);
            }
            return lzmAproperties;
        }

        /// <summary>
        /// Decompress the specified stream (C# inside)
        /// </summary>
        /// <param name="inStream">The source compressed stream</param>
        /// <param name="outStream">The destination uncompressed stream</param>
        /// <param name="inLength">The length of compressed data (null for inStream.Length)</param>
        /// <param name="codeProgressEvent">The event for handling the code progress</param>
        public static void DecompressStream(Stream inStream, Stream outStream, int? inLength,
                                            EventHandler<ProgressEventArgs> codeProgressEvent)
        {
            if (!inStream.CanRead || !outStream.CanWrite)
            {
                throw new ArgumentException("The specified streams are invalid.");
            }
            var decoder = new Decoder();
            long outSize, inSize = (inLength.HasValue ? inLength.Value : inStream.Length) - inStream.Position;
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
            using (var inStream = new MemoryStream(data))
            {
                var decoder = new Decoder();
                inStream.Seek(0, 0);
                using (var outStream = new MemoryStream())
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