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
    public sealed class SevenZipExtractor : SevenZipBase, ISevenZipExtractor, IDisposable
    {
        private List<ArchiveFileInfo> _ArchiveFileData;
        private IInArchive _Archive;
        private string _FileName;
        private long _PackedSize;
        private long? _UnpackedSize;
        private uint? _FilesCount;
        private bool? _IsSolid;
        private InArchiveFormat _Format;
        private ReadOnlyCollection<ArchiveFileInfo> _ArchiveFileInfoCollection;
        private ReadOnlyCollection<ArchiveProperty> _ArchiveProperties;

        /// <summary>
        /// General initialization function
        /// </summary>
        /// <param name="archiveFullName"></param>
        private void Init(string archiveFullName)
        {
            SevenZipLibraryManager.LoadLibrary(this, Formats.FormatByFileName(archiveFullName));
            try
            {
                _FileName = archiveFullName;
                _Format = Formats.FormatByFileName(_FileName);
                _Archive = SevenZipLibraryManager.InArchive(_Format);
                _PackedSize = (new FileInfo(archiveFullName)).Length;
            }
            catch (SevenZipLibraryException)
            {
                SevenZipLibraryManager.FreeLibrary(this, Formats.FormatByFileName(archiveFullName));
                throw;
            }
            _FilesCount = 0;
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">Archive full file name</param>
        public SevenZipExtractor(string archiveFullName)
            : base()
        {
            Init(archiveFullName);
        }

        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">Archive full file name</param>
        /// <param name="password">Password for encrypted archives</param>
        public SevenZipExtractor(string archiveFullName, string password)
            : base(password)
        {
            Init(archiveFullName);
        }
        /// <summary>
        /// Initializes a new instance of SevenZipExtractor class
        /// </summary>
        /// <param name="archiveFullName">Archive full file name</param>
        /// <param name="password">Password for encrypted archives</param>
        /// <param name="reportErrors">Throw exceptions on archive errors flag</param>
        public SevenZipExtractor(string archiveFullName, string password, bool reportErrors)
            : base(password, reportErrors)
        {
            Init(archiveFullName);
        }
        /// <summary>
        /// Frees the SevenZipExtractor class by calling Dispose method
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

            set
            {
                _FileName = value;
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
                    GetArchiveInfo();
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

        #region IDisposable Members

        /// <summary>
        /// Releases the unmanaged resources used by SevenZipExtractor
        /// </summary>
        public void Dispose()
        {
            if (!String.IsNullOrEmpty(_FileName))
            {
                SevenZipLibraryManager.FreeLibrary(this, Formats.FormatByFileName(_FileName));
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ISevenZipExtractor Members
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
        /// Occurs when the archive has been unpacked
        /// </summary>
        public event EventHandler ExtractionFinished;
        /// <summary>
        /// Occurs when data are being extracted
        /// </summary>
        /// <remarks>Use this event for accurate progress handling and various ProgressBar.StepBy(e.PercentDelta) routines</remarks>
        public event EventHandler<ProgressEventArgs> Extracting;

        private void OnExtractionFinished(EventArgs e)
        {
            if (ExtractionFinished != null)
            {
                ExtractionFinished(this, e);
            }
        }
        /// <summary>
        /// Performs basic archive consistence test
        /// </summary>
        public void Check()
        {
            try
            {
                using (InStreamWrapper ArchiveStream = new InStreamWrapper(File.OpenRead(_FileName)))
                {
                    ulong CheckPos = 1 << 15;
                    if (_Archive.Open(ArchiveStream, ref CheckPos, new ArchiveOpenCallback(Password)) != 0)
                    {
                        throw new SevenZipArchiveException();
                    }
                }
            }
            finally
            {
                _Archive.Close();
            }
        }

        /// <summary>
        /// Retrieves all information about the archive
        /// </summary>
        private void GetArchiveInfo()
        {
            if (_Archive == null)
            {
                throw new SevenZipArchiveException();
            }
            else
            {
                using (InStreamWrapper ArchiveStream = new InStreamWrapper(File.OpenRead(_FileName)))
                {
                    ulong CheckPos = 1 << 15;
                    if (_Archive.Open(ArchiveStream, ref CheckPos,
                        String.IsNullOrEmpty(Password) ? new ArchiveOpenCallback() : new ArchiveOpenCallback(Password)) != (int)OperationResult.Ok)
                    {
                        throw new SevenZipArchiveException();
                    }
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
                                fileInfo.FileName = (string)Data.Object;
                                _Archive.GetProperty(i, ItemPropId.LastWriteTime, ref Data);
                                fileInfo.LastWriteTime = NativeMethods.SafeCast<DateTime>(Data.Object, DateTime.Now);
                                _Archive.GetProperty(i, ItemPropId.Size, ref Data);
                                fileInfo.Size = NativeMethods.SafeCast<ulong>(Data.Object, 0);
                                _Archive.GetProperty(i, ItemPropId.Attributes, ref Data);
                                fileInfo.Attributes = NativeMethods.SafeCast<uint>(Data.Object, 0);
                                _Archive.GetProperty(i, ItemPropId.IsFolder, ref Data);
                                fileInfo.IsDirectory = NativeMethods.SafeCast<bool>(Data.Object, false);
                                _Archive.GetProperty(i, ItemPropId.Encrypted, ref Data);
                                fileInfo.Encrypted = NativeMethods.SafeCast<bool>(Data.Object, false);
                                _Archive.GetProperty(i, ItemPropId.Crc, ref Data);
                                fileInfo.Crc = NativeMethods.SafeCast<uint>(Data.Object, 0);
                                _Archive.GetProperty(i, ItemPropId.Comment, ref Data);
                                fileInfo.Comment = NativeMethods.SafeCast<string>(Data.Object, "");
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
                                _IsSolid = NativeMethods.SafeCast<bool>(Data.Object, true);
                            }
                            if (PropIdToName.PropIdNames.ContainsKey(propId))
                            {
                                archProps.Add(new ArchiveProperty(PropIdToName.PropIdNames[propId], Data.Object));
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
                        _Archive.Close();
                    }
                    _ArchiveFileInfoCollection = new ReadOnlyCollection<ArchiveFileInfo>(_ArchiveFileData);
                }
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
        private ArchiveExtractCallback GetArchiveExtractCallback(string directory)
        {
            ArchiveExtractCallback archiveExtractCallback = String.IsNullOrEmpty(Password) ?
                new ArchiveExtractCallback(_Archive, directory, (int)_FilesCount) :
                new ArchiveExtractCallback(_Archive, directory, (int)_FilesCount, Password);
            archiveExtractCallback.Open += new EventHandler<OpenEventArgs>((s, e) => { _UnpackedSize = (long)e.TotalSize; });
            archiveExtractCallback.FileExtractionStarted += FileExtractionStarted;
            archiveExtractCallback.FileExtractionFinished += FileExtractionFinished;
            archiveExtractCallback.Extracting += Extracting;
            return archiveExtractCallback;
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
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        [CLSCompliantAttribute(false)]
        public void ExtractFile(uint index, string directory, bool reportErrors)
        {
            ExtractFiles(new uint[] { index }, directory, reportErrors);
        }
        /// <summary>
        /// Unpacks the file by its full name to the specified directory
        /// </summary>
        /// <param name="fileName">File full name in the archive file table</param>
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
        /// <param name="reportErrors">Throw exception if extraction fails</param>
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
        /// <param name="reportErrors">Throw exception if extraction fails</param>
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
                using (InStreamWrapper ArchiveStream = new InStreamWrapper(File.OpenRead(_FileName)))
                {
                    ulong CheckPos = 1 << 15;
                    if (_Archive.Open(ArchiveStream, ref CheckPos,
                        String.IsNullOrEmpty(Password) ? new ArchiveOpenCallback() : new ArchiveOpenCallback(Password)) != 0
                        && reportErrors)
                    {
                        throw new SevenZipArchiveException();
                    }
                    try
                    {
                        CheckedExecute(
                            _Archive.Extract(indexes, 1, 0, GetArchiveExtractCallback(directory)),
                            SevenZipExtractionFailedException.DefaultMessage);
                    }
                    catch (ExtractionFailedException)
                    {
                        if (reportErrors)
                        {
                            throw;
                        }
                    }
                    if (_IsSolid.Value)
                    {
                        foreach (uint i in indexes)
                        {
                            if (!origIndexes.Contains(i))
                            {
                                File.Delete(directory + _ArchiveFileData[(int)i].FileName);
                            }
                        }
                    }
                    OnExtractionFinished(EventArgs.Empty);
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
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        public void ExtractFiles(string[] fileNames, string directory, bool reportErrors)
        {
            List<uint> indexes = new List<uint>(fileNames.Length);
            List<string> archiveFileNames = new List<string>(ArchiveFileNames);
            foreach (string fn in fileNames)
            {
                if (!archiveFileNames.Contains(fn) && reportErrors)
                {
                    throw new ArgumentOutOfRangeException("File " + fn + " is not in the archive!");
                }
                else
                {
                    foreach (ArchiveFileInfo afi in _ArchiveFileData)
                    {
                        if (afi.FileName == fn)
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
        /// Unpacks the whole archive to the specified directory
        /// </summary>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        public void ExtractArchive(string directory, bool reportErrors)
        {
            if (_ArchiveFileData == null)
            {
                GetArchiveInfo();
            }
            try
            {
                using (InStreamWrapper ArchiveStream = new InStreamWrapper(File.OpenRead(_FileName)))
                {
                    ulong CheckPos = 1 << 15;
                    if (_Archive.Open(ArchiveStream, ref CheckPos,
                        String.IsNullOrEmpty(Password) ? new ArchiveOpenCallback() : new ArchiveOpenCallback(Password)) != 0
                        && reportErrors)
                    {
                        throw new SevenZipArchiveException();
                    }
                    try
                    {
                        CheckedExecute(
                            _Archive.Extract(null, UInt32.MaxValue, 0, GetArchiveExtractCallback(directory)),
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
                    catch (SevenZipException)
                    {
                        if (reportErrors)
                        {
                            throw;
                        }
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

        /// <summary>
        /// Decompress byte array compressed with LZMA algorithm
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
                    byte[] LZMAproperties = new byte[5];
                    #region Read LZMA properties
                    if (inStream.Read(LZMAproperties, 0, 5) != 5)
                    {
                        throw new LzmaException();
                    }
                    long outSize = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        int b = inStream.ReadByte();
                        if (b < 0)
                        {
                            throw new LzmaException();
                        }
                        outSize |= ((long)(byte)b) << (i << 3);
                    }
                    #endregion
                    decoder.SetDecoderProperties(LZMAproperties);
                    decoder.Code(inStream, outStream, inStream.Length - inStream.Position, outSize, null);
                    return outStream.ToArray();
                }
            }
        }
    }
}
