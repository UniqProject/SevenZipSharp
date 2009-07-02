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
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using SevenZip.ComRoutines;

namespace SevenZip
{    
    #if UNMANAGED
    /// <summary>
    /// SevenZip Extractor/Compressor base class. Implements Password string, ReportErrors flag.
    /// </summary>
    public class SevenZipBase : MarshalByRefObject
    {
        private string _Password;
        private bool _ReportErrors;
        /// <summary>
        /// User exceptions thrown during the requested operations, for example, in events.
        /// </summary>
        private List<Exception> _UserExceptions = new List<Exception>();

        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        public SevenZipBase()
        {
            _Password = "";
            _ReportErrors = true;
        }
        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        /// <param name="password">The archive password</param>
        public SevenZipBase(string password)
        {
            if (String.IsNullOrEmpty(password))
            {
                throw new SevenZipException("Empty password was specified.");
            }
            _Password = password;
            _ReportErrors = true;
        }
        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        /// <param name="reportErrors">Throw exceptions on archive errors flag</param>
        public SevenZipBase(bool reportErrors)
        {
            _Password = "";
            _ReportErrors = reportErrors;
        }
        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        /// <param name="password">The archive password</param>
        /// <param name="reportErrors">Throw exceptions on archive errors flag</param>
        public SevenZipBase(string password, bool reportErrors)
        {
            _ReportErrors = reportErrors;
            if (String.IsNullOrEmpty(password))
            {
                throw new SevenZipException("Empty password was specified.");
            }
            _Password = password;
        }
        /// <summary>
        /// Gets or sets the archive password
        /// </summary>
        protected string Password
        {
            get
            {
                return _Password;
            }

            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new SevenZipException("Empty password was specified.");
                }
                _Password = value;
            }
        }
        /// <summary>
        /// Gets or sets throw exceptions on archive errors flag
        /// </summary>
        protected bool ReportErrors
        {
            get
            {
                return _ReportErrors;
            }

            set
            {
                _ReportErrors = value;
            }
        }

        /// <summary>
        /// Gets the user exceptions thrown during the requested operations, for example, in events.
        /// </summary>
        public ReadOnlyCollection<Exception> Exceptions
        {
            get
            {
                return new ReadOnlyCollection<Exception>(_UserExceptions);
            }
        }

        internal void AddException(Exception e)
        {
            _UserExceptions.Add(e);
        }

        internal void ClearExceptions()
        {
            _UserExceptions.Clear();
        }

        internal bool HasExceptions()
        {
            return _UserExceptions.Count > 0;
        }

        /// <summary>
        /// Throws exception if HRESULT != 0.
        /// </summary>
        /// <param name="hresult">Result code to check.</param>
        /// <param name="message">Exception message.</param>
        /// <param name="handler">The class responsible for the callback.</param>
        public static void CheckedExecute(int hresult, string message, SevenZipBase handler)
        {
            if (hresult != (int)SevenZip.ComRoutines.OperationResult.Ok)
            {
                if (!handler.HasExceptions())
                {
                    if (hresult < -2000000000)
                    {
                        throw new SevenZipException("The execution has failed due to the bug in the SevenZipSharp.\n" +
                            "Please report about it to http://sevenzipsharp.codeplex.com/WorkItem/List.aspx, post the release number and attach the archive.");
                    }
                    else
                    {
                        throw new SevenZipException(message + hresult.ToString(CultureInfo.InvariantCulture) + '.');
                    }
                }
                else
                {
                    throw handler.Exceptions[0];
                }
            }
        }
    }    

    /// <summary>
    /// Struct for storing information about files in the 7-zip archive
    /// </summary>
    public struct ArchiveFileInfo
    {
        private uint _Index;
        private string _FileName;
        private DateTime _LastWriteTime;
        private DateTime _CreationTime;
        private DateTime _LastAccessTime;
        private ulong _Size;
        private uint _CRC;
        private uint _Attributes;
        private bool _IsDirectory;
        private bool _Encrypted;
        private string _Comment;

        /// <summary>
        /// Gets or sets index of the file in the archive file table
        /// </summary>
        [CLSCompliantAttribute(false)]
        public uint Index
        {
            get
            {
                return _Index;
            }

            set
            {
                _Index = value;
            }
        }

        /// <summary>
        /// Gets or sets file name
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
        /// Gets or sets the file last write time.
        /// </summary>
        public DateTime LastWriteTime
        {
            get
            {
                return _LastWriteTime;
            }

            set
            {
                _LastWriteTime = value;
            }
        }

        /// <summary>
        /// Gets or sets the file creation time.
        /// </summary>
        public DateTime CreationTime
        {
            get
            {
                return _CreationTime;
            }

            set
            {
                _CreationTime = value;
            }
        }
        
        /// <summary>
        /// Gets or sets the file creation time.
        /// </summary>
        public DateTime LastAccessTime
        {
            get
            {
                return _LastAccessTime;
            }

            set
            {
                _LastAccessTime = value;
            }
        }

        /// <summary>
        /// Gets or sets size of the file (unpacked)
        /// </summary>
        [CLSCompliantAttribute(false)]
        public ulong Size
        {
            get
            {
                return _Size;
            }

            set
            {
                _Size = value;
            }
        }

        /// <summary>
        /// Gets or sets CRC checksum of the file
        /// </summary>
        [CLSCompliantAttribute(false)]
        public uint Crc
        {
            get
            {
                return _CRC;
            }

            set
            {
                _CRC = value;
            }
        }

        /// <summary>
        /// Gets or sets file attributes
        /// </summary>
        [CLSCompliantAttribute(false)]
        public uint Attributes
        {
            get
            {
                return _Attributes;
            }

            set
            {
                _Attributes = value;
            }
        }

        /// <summary>
        /// Gets or sets being a directory
        /// </summary>
        public bool IsDirectory
        {
            get
            {
                return _IsDirectory;
            }

            set
            {
                _IsDirectory = value;
            }
        }

        /// <summary>
        /// Gets or sets being encrypted
        /// </summary>
        public bool Encrypted
        {
            get
            {
                return _Encrypted;
            }

            set
            {
                _Encrypted = value;
            }
        }

        /// <summary>
        /// Gets or sets comment for the file
        /// </summary>
        public string Comment
        {
            get
            {
                return _Comment;
            }

            set
            {
                _Comment = value;
            }
        }
        /// <summary>
        /// Determines whether the specified System.Object is equal to the current ArchiveFileInfo.
        /// </summary>
        /// <param name="obj">The System.Object to compare with the current ArchiveFileInfo.</param>
        /// <returns>true if the specified System.Object is equal to the current ArchiveFileInfo; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return (obj is ArchiveFileInfo) ? Equals((ArchiveFileInfo)obj) : false;
        }
        /// <summary>
        /// Determines whether the specified ArchiveFileInfo is equal to the current ArchiveFileInfo.
        /// </summary>
        /// <param name="afi">The ArchiveFileInfo to compare with the current ArchiveFileInfo.</param>
        /// <returns>true if the specified ArchiveFileInfo is equal to the current ArchiveFileInfo; otherwise, false.</returns>
        public bool Equals(ArchiveFileInfo afi)
        {
            return afi.Index == _Index && afi.FileName == _FileName;
        }
        /// <summary>
        ///  Serves as a hash function for a particular type.
        /// </summary>
        /// <returns> A hash code for the current ArchiveFileInfo.</returns>
        public override int GetHashCode()
        {
            return _FileName.GetHashCode() ^ (int)_Index;
        }
        /// <summary>
        /// Returns a System.String that represents the current ArchiveFileInfo.
        /// </summary>
        /// <returns>A System.String that represents the current ArchiveFileInfo.</returns>
        public override string ToString()
        {
            return "[" + _Index.ToString(CultureInfo.CurrentCulture) + "] " + _FileName;
        }
        /// <summary>
        /// Determines whether the specified ArchiveFileInfo instances are considered equal.
        /// </summary>
        /// <param name="afi1">The first ArchiveFileInfo to compare.</param>
        /// <param name="afi2">The second ArchiveFileInfo to compare.</param>
        /// <returns>true if the specified ArchiveFileInfo instances are considered equal; otherwise, false.</returns>
        public static bool operator ==(ArchiveFileInfo afi1, ArchiveFileInfo afi2)
        {
            return afi1.Equals(afi2);
        }
        /// <summary>
        /// Determines whether the specified ArchiveFileInfo instances are not considered equal.
        /// </summary>
        /// <param name="afi1">The first ArchiveFileInfo to compare.</param>
        /// <param name="afi2">The second ArchiveFileInfo to compare.</param>
        /// <returns>true if the specified ArchiveFileInfo instances are not considered equal; otherwise, false.</returns>
        public static bool operator !=(ArchiveFileInfo afi1, ArchiveFileInfo afi2)
        {
            return !afi1.Equals(afi2);
        }
    }
    
    /// <summary>
    /// Archive property struct
    /// </summary>
    public struct ArchiveProperty
    {
        private string _Name;
        private object _Value;

        /// <summary>
        /// Gets the name of the archive property
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
        }
        /// <summary>
        /// Gets the value of the archive property
        /// </summary>
        public object Value
        {
            get
            {
                return _Value;
            }
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveProperty struct
        /// </summary>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        public ArchiveProperty(string name, object value)
        {
            _Name = name;
            _Value = value;
        }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current ArchiveProperty.
        /// </summary>
        /// <param name="obj">The System.Object to compare with the current ArchiveProperty.</param>
        /// <returns>true if the specified System.Object is equal to the current ArchiveProperty; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return (obj is ArchiveProperty) ? Equals((ArchiveProperty)obj) : false;
        }
        /// <summary>
        /// Determines whether the specified ArchiveProperty is equal to the current ArchiveProperty.
        /// </summary>
        /// <param name="afi">The ArchiveProperty to compare with the current ArchiveProperty.</param>
        /// <returns>true if the specified ArchiveProperty is equal to the current ArchiveProperty; otherwise, false.</returns>
        public bool Equals(ArchiveProperty afi)
        {
            return afi.Name == _Name && afi.Value == _Value;
        }
        /// <summary>
        ///  Serves as a hash function for a particular type.
        /// </summary>
        /// <returns> A hash code for the current ArchiveProperty.</returns>
        public override int GetHashCode()
        {
            return _Name.GetHashCode() ^ _Value.GetHashCode();
        }
        /// <summary>
        /// Returns a System.String that represents the current ArchiveProperty.
        /// </summary>
        /// <returns>A System.String that represents the current ArchiveProperty.</returns>
        public override string ToString()
        {
            return _Name + " = " + _Value.ToString();
        }
        /// <summary>
        /// Determines whether the specified ArchiveProperty instances are considered equal.
        /// </summary>
        /// <param name="afi1">The first ArchiveProperty to compare.</param>
        /// <param name="afi2">The second ArchiveProperty to compare.</param>
        /// <returns>true if the specified ArchiveProperty instances are considered equal; otherwise, false.</returns>
        public static bool operator ==(ArchiveProperty afi1, ArchiveProperty afi2)
        {
            return afi1.Equals(afi2);
        }
        /// <summary>
        /// Determines whether the specified ArchiveProperty instances are not considered equal.
        /// </summary>
        /// <param name="afi1">The first ArchiveProperty to compare.</param>
        /// <param name="afi2">The second ArchiveProperty to compare.</param>
        /// <returns>true if the specified ArchiveProperty instances are not considered equal; otherwise, false.</returns>
        public static bool operator !=(ArchiveProperty afi1, ArchiveProperty afi2)
        {
            return !afi1.Equals(afi2);
        }
    }

    /// <summary>
    /// Interface for extracting and getting info from 7-zip archives
    /// </summary>
    [CLSCompliantAttribute(false)]
    public interface ISevenZipExtractor
    {
        /// <summary>
        /// Gets size of the archive file
        /// </summary>
        long PackedSize
        { get; }
        /// <summary>
        /// Gets size of unpacked archive data
        /// </summary>
        long UnpackedSize
        { get; }
        /// <summary>
        /// Gets a value indicating whether the archive is solid
        /// </summary>
        bool IsSolid
        { get; }
        /// <summary>
        /// Gets the collection of all file names contained in the archive.
        /// </summary>
        /// <remarks>
        /// Each get recreates the collection
        /// </remarks>
        ReadOnlyCollection<string> ArchiveFileNames
        { get; }
        /// <summary>
        /// Gets the properties for the current archive
        /// </summary>
        ReadOnlyCollection<ArchiveProperty> ArchiveProperties
        { get; }
        /// <summary>
        /// Gets the collection of ArchiveFileInfo with all information about files in the archive
        /// </summary>
        ReadOnlyCollection<ArchiveFileInfo> ArchiveFileData
        { get; }
        /// <summary>
        /// Occurs when a new file is going to be unpacked
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an output stream for a new file to unpack in</remarks>
        event EventHandler<FileInfoEventArgs> FileExtractionStarted;
        /// <summary>
        /// Occurs when a file has been successfully unpacked
        /// </summary>
        event EventHandler FileExtractionFinished;
        /// <summary>
        /// Occurs when the archive has been unpacked
        /// </summary>
        event EventHandler ExtractionFinished;
        /// <summary>
        /// Occurs when data are being extracted
        /// </summary>
        /// <remarks>Use this event for accurate progress handling and various ProgressBar.StepBy(e.PercentDelta) routines</remarks>
        event EventHandler<ProgressEventArgs> Extracting;
        /// <summary>
        /// Occurs during the extraction when a file already exists
        /// </summary>
        event EventHandler<FileOverwriteEventArgs> FileExists;
        /// <summary>
        /// Unpacks the whole archive to the specified directory
        /// </summary>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        void ExtractArchive(string directory, bool reportErrors);
        /// <summary>
        /// Unpacks the whole archive to the specified directory
        /// </summary>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        void ExtractArchive(string directory);
        /// <summary>
        /// Unpacks the file by its name to the specified stream
        /// </summary>
        /// <param name="fileName">The file full name in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        void ExtractFile(string fileName, Stream stream);
        /// <summary>
        /// Unpacks the file by its name to the specified stream
        /// </summary>
        /// <param name="fileName">The file full name in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        void ExtractFile(string fileName, Stream stream, bool reportErrors);
        /// <summary>
        /// Unpacks the file by its index to the specified stream
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        void ExtractFile(uint index, Stream stream);
        /// <summary>
        /// Unpacks the file by its index to the specified stream
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="stream">The stream where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw an exception if extraction fails</param>
        void ExtractFile(uint index, Stream stream, bool reportErrors);
        /// <summary>
        /// Unpacks the file by its index to the specified directory
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        void ExtractFile(uint index, string directory);
        /// <summary>
        /// Unpacks the file by its full name to the specified directory
        /// </summary>
        /// <param name="fileName">File full name in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        void ExtractFile(string fileName, string directory);
        /// <summary>
        /// Unpacks files by their indexes to the specified directory
        /// </summary>
        /// <param name="indexes">indexes of the files in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        void ExtractFiles(uint[] indexes, string directory);
        /// <summary>
        /// Unpacks files by their full names to the specified directory
        /// </summary>
        /// <param name="fileNames">Full file names in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        void ExtractFiles(string[] fileNames, string directory);
        /// <summary>
        /// Unpacks the file by its index to the specified directory
        /// </summary>
        /// <param name="index">Index in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        void ExtractFile(uint index, string directory, bool reportErrors);
        /// <summary>
        /// Unpacks the file by its full name to the specified directory
        /// </summary>
        /// <param name="fileName">File full name in the archive file table</param>
        /// <param name="directory">Directory where the file is to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        void ExtractFile(string fileName, string directory, bool reportErrors);
        /// <summary>
        /// Unpacks files by their indexes to the specified directory
        /// </summary>
        /// <param name="indexes">indexes of the files in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        void ExtractFiles(uint[] indexes, string directory, bool reportErrors);
        /// <summary>
        /// Unpacks files by their full names to the specified directory
        /// </summary>
        /// <param name="fileNames">Full file names in the archive file table</param>
        /// <param name="directory">Directory where the files are to be unpacked</param>
        /// <param name="reportErrors">Throw exception if extraction fails</param>
        void ExtractFiles(string[] fileNames, string directory, bool reportErrors);
        /// <summary>
        /// Performs basic archive consistence test
        /// </summary>
        void Check();
    }

    #if COMPRESS

    /// <summary>
    /// Archive compression mode.
    /// </summary>
    public enum CompressionMode
    {
        /// <summary>
        /// Create a new archive; overwrite the existing one.
        /// </summary>
        Create,
        /// <summary>
        /// Add data to the archive.
        /// </summary>
        Append,
    }

    internal enum InternalCompressionMode
    {
        /// <summary>
        /// Create a new archive; overwrite the existing one.
        /// </summary>
        Create,
        /// <summary>
        /// Add data to the archive.
        /// </summary>
        Append,
        /// <summary>
        /// Modify archive data.
        /// </summary>
        Modify
    }
    /// <summary>
    /// Archive update data for UpdateCallback.
    /// </summary>
    internal struct UpdateData
    {
        public InternalCompressionMode Mode;
        public uint FilesCount;
        private Dictionary<int, string> _FileNamesToModify;
        private List<ArchiveFileInfo> _ArchiveFileData;

        public Dictionary<int, string> FileNamesToModify
        {
            get
            {
                return _FileNamesToModify;
            }

            set
            {
                _FileNamesToModify = value;
            }
        }

        public List<ArchiveFileInfo> ArchiveFileData
        {
            get
            {
                return _ArchiveFileData;
            }

            set
            {
                _ArchiveFileData = value;
            }
        }
    }

    /// <summary>
    /// Interface for packing files in 7-zip format.
    /// </summary>
    public interface ISevenZipCompressor
    {
        /// <summary>
        /// Gets or sets the compression level
        /// </summary>
        CompressionLevel CompressionLevel { get; set; }
        /// <summary>
        /// Gets or sets the archive format
        /// </summary>
        OutArchiveFormat ArchiveFormat { get; set; }
        /// <summary>
        /// Gets or sets the compression method
        /// </summary>
        CompressionMethod CompressionMethod { get; set; }
        /// <summary>
        /// Gets or sets the custom compression parameters - for advanced users only
        /// </summary>
        Dictionary<string, string> CustomParameters { get; }
        /// <summary>
        /// Gets or sets the size of the archive volume (0 for no volumes)
        /// </summary>
        int VolumeSize { get; set; }
        /// <summary>
        /// Occurs when the next file is going to be packed.
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an input stream for the next file to pack it.</remarks>
        event EventHandler<FileNameEventArgs> FileCompressionStarted;

        /// <summary>
        /// Occurs when data are being compressed
        /// </summary>
        /// <remarks>Use this event for accurate progress handling and various ProgressBar.StepBy(e.PercentDelta) routines.</remarks>
        event EventHandler<ProgressEventArgs> Compressing;

        /// <summary>
        /// Occurs when all files information was determined and SevenZipCompressor is about to start to compress them.
        /// </summary>
        event EventHandler<IntEventArgs> FilesFound;

        /// <summary>
        /// Occurs when the compression procedure is finished
        /// </summary>
        event EventHandler CompressionFinished;
        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveName">The archive file name</param>
        void CompressFiles(
            string[] fileFullNames, string archiveName);
        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>        
        void CompressFiles(
            string archiveName, string[] fileFullNames, string password);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRoot">Common root of the file names</param>
        /// <param name="archiveName">The archive file name</param>
        void CompressFiles(
            string[] fileFullNames, string commonRoot, string archiveName);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRoot">Common root of the file names</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        void CompressFiles(
            string[] fileFullNames, string commonRoot, string archiveName, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        void CompressDirectory(
            string directory, string archiveName);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        void CompressDirectory(
            string directory, string archiveName, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, string archiveName, bool recursion);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, string archiveName, string searchPattern, bool recursion);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>        
        /// <param name="recursion">Search for files recursively</param>
        /// <param name="password">The archive password</param>
        void CompressDirectory(
            string directory, string archiveName,
            bool recursion, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, string archiveName,
            string password, string searchPattern, bool recursion);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveStream">The archive output stream</param>
        void CompressFiles(
            string[] fileFullNames, Stream archiveStream);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="password">The archive password</param>        
        void CompressFiles(
            string[] fileFullNames, Stream archiveStream, string password);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRoot">Common root of the file names</param>
        /// <param name="archiveStream">The archive output stream</param>
        void CompressFiles(
            string[] fileFullNames, string commonRoot, Stream archiveStream);

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRoot">Common root of the file names</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="password">The archive password</param>
        void CompressFiles(
            string[] fileFullNames, string commonRoot, Stream archiveStream, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>
        void CompressDirectory(
            string directory, Stream archiveStream);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="password">The archive password</param>
        void CompressDirectory(
            string directory, Stream archiveStream, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, Stream archiveStream, bool recursion);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, Stream archiveStream, string searchPattern, bool recursion);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>        
        /// <param name="recursion">Search for files recursively</param>
        /// <param name="password">The archive password</param>
        void CompressDirectory(
            string directory, Stream archiveStream,
            bool recursion, string password);

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream</param>
        /// <param name="password">The archive password</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        void CompressDirectory(
            string directory, Stream archiveStream,
            string password, string searchPattern, bool recursion);

        /// <summary>
        /// Compresses the specified stream
        /// </summary>
        /// <param name="inStream">The source uncompressed stream</param>
        /// <param name="outStream">The destination compressed stream</param>
        void CompressStream(Stream inStream, Stream outStream);

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, string archiveName);

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, string archiveName, string password);

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, Stream archiveStream);

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, Stream archiveStream, string password);

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, string archiveName);

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, string archiveName, string password);

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, Stream archiveStream);

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, Stream archiveStream, string password);
    }
    #endif
    #endif
}
