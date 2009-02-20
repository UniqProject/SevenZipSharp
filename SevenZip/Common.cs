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
using System.IO;
using System.Runtime.Serialization;

[assembly: CLSCompliant(true)]
namespace SevenZip
{
    #region Exceptions
    /// <summary>
    /// Exception class for LZMA operations
    /// </summary>
    [Serializable]
    public class LZMAException : Exception
    {
        const string DefaultMessage = "Specified byte array is not a valid LZMA compressed byte array!";
        public LZMAException() : base(DefaultMessage) { }
        public LZMAException(string message) : base(DefaultMessage + " Message: " + message) { }
        public LZMAException(string message, Exception inner) : base(DefaultMessage + " Message: " + message, inner) { }
        protected LZMAException( 
            SerializationInfo info, StreamingContext context ) : base( info, context ) { }
    }    
    /// <summary>
    /// Exception class for 7-zip archive open or read operations
    /// </summary>
    [Serializable]
    public class SevenZipArchiveException : Exception
    {
        const string DefaultMessage = "Invalid archive: open/read error! Is it encrypted and no password was provided?";
        public SevenZipArchiveException() : base(DefaultMessage) { }
        public SevenZipArchiveException(string message) : base(DefaultMessage + " Message: " + message) { }
        public SevenZipArchiveException(string message, Exception inner) : base(DefaultMessage + " Message: " + message, inner) { }
        protected SevenZipArchiveException(
            SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Exception class for empty common root if file name array in SevenZipCompressor
    /// </summary>
    [Serializable]
    public class SevenZipInvalidFileNamesException : Exception
    {
        const string DefaultMessage = "Invalid file names: ";
        public SevenZipInvalidFileNamesException() : base(DefaultMessage) { }
        public SevenZipInvalidFileNamesException(string message) : base(DefaultMessage + " Message: " + message) { }
        public SevenZipInvalidFileNamesException(string message, Exception inner) : base(DefaultMessage + " Message: " + message, inner) { }
        protected SevenZipInvalidFileNamesException(
            SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Exception class for fail to create an archive in SevenZipCompressor
    /// </summary>
    [Serializable]
    public class SevenZipCompressionFailedException : Exception
    {
        const string DefaultMessage = "The compression has failed for an unknown reason with code ";
        public SevenZipCompressionFailedException() : base(DefaultMessage) { }
        public SevenZipCompressionFailedException(string message) : base(DefaultMessage + " Message: " + message) { }
        public SevenZipCompressionFailedException(string message, Exception inner) : base(DefaultMessage + " Message: " + message, inner) { }
        protected SevenZipCompressionFailedException(
            SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Exception class for fail to extract an archive in SevenZipExtractor
    /// </summary>
    [Serializable]
    public class SevenZipExtractionFailedException : Exception
    {
        const string DefaultMessage = "The extraction has failed for an unknown reason with code ";
        public SevenZipExtractionFailedException() : base(DefaultMessage) { }
        public SevenZipExtractionFailedException(string message) : base(DefaultMessage + " Message: " + message) { }
        public SevenZipExtractionFailedException(string message, Exception inner) : base(DefaultMessage + " Message: " + message, inner) { }
        protected SevenZipExtractionFailedException(
            SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    #endregion

    /// <summary>
    /// Class that stores a password
    /// </summary>
    public class PasswordAware
    {
        private string _Password;
        /// <summary>
        /// Initializes a new instance of the PasswordAware class
        /// </summary>
        public PasswordAware() { }
        /// <summary>
        /// Initializes a new instance of the PasswordAware class
        /// </summary>
        /// <param name="password">Archive password</param>
        public PasswordAware(string password)
        {
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
                _Password = value;
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
        /// Gets or sets file write time
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
    }

    /// <summary>
    /// Interface for extracting and getting info from 7-zip archives
    /// </summary>
    [CLSCompliantAttribute(false)]
    public interface ISevenZipExtractor
    {
        long PackedSize
        { get; }
        long UnpackedSize
        { get; }
        string[] ArchiveFileNames
        { get; }
        List<ArchiveFileInfo> ArchiveFileTable
        { get; }
        event EventHandler<IndexEventArgs> FileExtractionStarted;
        event EventHandler FileExtractionFinished;
        event EventHandler ExtractionFinished;
        void ExtractArchive(string directory, bool reportErrors);
        void ExtractArchive(string directory);
        void ExtractFile(uint index, string directory);
        void ExtractFile(string fileName, string directory);
        void ExtractFiles(uint[] indexes, string directory);
        void ExtractFiles(string[] fileNames, string directory);
        void ExtractFile(uint index, string directory, bool reportErrors);
        void ExtractFile(string fileName, string directory, bool reportErrors);
        void ExtractFiles(uint[] indexes, string directory, bool reportErrors);
        void ExtractFiles(string[] fileNames, string directory, bool reportErrors);
        void Check();
    }

    /// <summary>
    /// Interface for packing files in 7-zip format
    /// </summary>
    public interface ISevenZipCompressor
    {        
        void CompressFiles(
            string[] fileFullNames, string archiveName, OutArchiveFormat format);
        void CompressFiles(
            string[] fileFullNames, string archiveName, OutArchiveFormat format, string password);
        void CompressFiles(
            string[] fileFullNames, string commonRoot, string archiveName, OutArchiveFormat format);
        void CompressFiles(
            string[] fileFullNames, string commonRoot, string archiveName, OutArchiveFormat format, string password);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format, string password);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format, bool recursion);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format, string searchPattern, bool recursion);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format,
            bool recursion, string password);
        void CompressDirectory(
            string path, string archiveName, OutArchiveFormat format,
            string password, string searchPattern, bool recursion);       
    }        
}
