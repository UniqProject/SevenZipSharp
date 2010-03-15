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
using System.Globalization;

namespace SevenZip
{
#if UNMANAGED
    /// <summary>
    /// SevenZip Extractor/Compressor base class. Implements Password string, ReportErrors flag.
    /// </summary>
    public class SevenZipBase : MarshalByRefObject
    {
        private readonly string _password;
        private readonly bool _reportErrors;

        /// <summary>
        /// User exceptions thrown during the requested operations, for example, in events.
        /// </summary>
        private readonly List<Exception> _exceptions = new List<Exception>();

        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        protected SevenZipBase()
        {
            _password = "";
            _reportErrors = true;
        }

        /// <summary>
        /// Initializes a new instance of the SevenZipBase class
        /// </summary>
        /// <param name="password">The archive password</param>
        protected SevenZipBase(string password)
        {
            if (String.IsNullOrEmpty(password))
            {
                throw new SevenZipException("Empty password was specified.");
            }
            _password = password;
            _reportErrors = true;
        }

        /// <summary>
        /// Gets or sets the archive password
        /// </summary>
        protected string Password
        {
            get
            {
                return _password;
            }
            /*set
            {
                if (String.IsNullOrEmpty(value))
                {
                    throw new SevenZipException("Empty password was specified.");
                }
                _password = value;
            }*/
        }

        /// <summary>
        /// Gets or sets the value indicating whether the current procedure was cancelled.
        /// </summary>
        protected bool Canceled { get; set; }

        /// <summary>
        /// Gets or sets throw exceptions on archive errors flag
        /// </summary>
        protected bool ReportErrors
        {
            get
            {
                return _reportErrors;
            }
        }

        /// <summary>
        /// Gets the user exceptions thrown during the requested operations, for example, in events.
        /// </summary>
        private ReadOnlyCollection<Exception> Exceptions
        {
            get
            {
                return new ReadOnlyCollection<Exception>(_exceptions);
            }
        }

        internal void AddException(Exception e)
        {
            _exceptions.Add(e);
        }

        internal void ClearExceptions()
        {
            _exceptions.Clear();
        }

        private bool HasExceptions
        {
            get
            {
                return _exceptions.Count > 0;
            }
        }

        /// <summary>
        /// Throws the specified exception when is able to.
        /// </summary>
        /// <param name="e">The exception to throw.</param>
        /// <param name="handler">The handler responsible for the exception.</param>
        internal bool ThrowException(SevenZipBase handler, params Exception[] e)
        {
            if (_reportErrors && (handler == null || !handler.Canceled))
            {
                throw e[0];
            }
            return false;
        }

        /// <summary>
        /// Throws the first exception in the list if any exists.
        /// </summary>
        /// <returns>True means no exceptions.</returns>
        internal bool ThrowException()
        {
            if (HasExceptions && _reportErrors)
            {
                throw _exceptions[0];
            }
            return true;
        }

        internal void ThrowUserException()
        {
            if (HasExceptions)
            {
                throw new SevenZipException(SevenZipException.USER_EXCEPTION_MESSAGE);
            }
        }

        /// <summary>
        /// Throws exception if HRESULT != 0.
        /// </summary>
        /// <param name="hresult">Result code to check.</param>
        /// <param name="message">Exception message.</param>
        /// <param name="handler">The class responsible for the callback.</param>
        protected void CheckedExecute(int hresult, string message, SevenZipBase handler)
        {
            if (hresult != (int) OperationResult.Ok || handler.HasExceptions)
            {
                if (!handler.HasExceptions)
                {
                    if (hresult < -2000000000)
                    {
                        ThrowException(handler,
                                       new SevenZipException(
                                           "The execution has failed due to the bug in the SevenZipSharp.\n" +
                                           "Please report about it to http://sevenzipsharp.codeplex.com/WorkItem/List.aspx, post the release number and attach the archive."));
                    }
                    else
                    {
                        ThrowException(handler,
                                       new SevenZipException(message + hresult.ToString(CultureInfo.InvariantCulture) +
                                                             '.'));
                    }
                }
                else
                {
                    ThrowException(handler, handler.Exceptions[0]);
                }
            }
        }

#if !WINCE
        /// <summary>
        /// Changes the path to the 7-zip native library.
        /// </summary>
        /// <param name="libraryPath">The path to the 7-zip native library.</param>
        public static void SetLibraryPath(string libraryPath)
        {
            SevenZipLibraryManager.SetLibraryPath(libraryPath);
        }
#endif
        /// <summary>
        /// Gets the current library features.
        /// </summary>
        [CLSCompliant(false)]
        public static LibraryFeature CurrentLibraryFeatures
        {
            get
            {
                return SevenZipLibraryManager.CurrentLibraryFeatures;
            }
        }
    }    

    /// <summary>
    /// Struct for storing information about files in the 7-zip archive.
    /// </summary>
    public struct ArchiveFileInfo
    {
        /// <summary>
        /// Gets or sets index of the file in the archive file table.
        /// </summary>
        [CLSCompliant(false)]
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the file last write time.
        /// </summary>
        public DateTime LastWriteTime { get; set; }

        /// <summary>
        /// Gets or sets the file creation time.
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the file creation time.
        /// </summary>
        public DateTime LastAccessTime { get; set; }

        /// <summary>
        /// Gets or sets size of the file (unpacked).
        /// </summary>
        [CLSCompliant(false)]
        public ulong Size { get; set; }

        /// <summary>
        /// Gets or sets CRC checksum of the file.
        /// </summary>
        [CLSCompliant(false)]
        public uint Crc { get; set; }

        /// <summary>
        /// Gets or sets file attributes.
        /// </summary>
        [CLSCompliant(false)]
        public uint Attributes { get; set; }

        /// <summary>
        /// Gets or sets being a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets being encrypted.
        /// </summary>
        public bool Encrypted { get; set; }

        /// <summary>
        /// Gets or sets comment for the file.
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current ArchiveFileInfo.
        /// </summary>
        /// <param name="obj">The System.Object to compare with the current ArchiveFileInfo.</param>
        /// <returns>true if the specified System.Object is equal to the current ArchiveFileInfo; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return (obj is ArchiveFileInfo) ? Equals((ArchiveFileInfo) obj) : false;
        }

        /// <summary>
        /// Determines whether the specified ArchiveFileInfo is equal to the current ArchiveFileInfo.
        /// </summary>
        /// <param name="afi">The ArchiveFileInfo to compare with the current ArchiveFileInfo.</param>
        /// <returns>true if the specified ArchiveFileInfo is equal to the current ArchiveFileInfo; otherwise, false.</returns>
        public bool Equals(ArchiveFileInfo afi)
        {
            return afi.Index == Index && afi.FileName == FileName;
        }

        /// <summary>
        ///  Serves as a hash function for a particular type.
        /// </summary>
        /// <returns> A hash code for the current ArchiveFileInfo.</returns>
        public override int GetHashCode()
        {
            return FileName.GetHashCode() ^ Index;
        }

        /// <summary>
        /// Returns a System.String that represents the current ArchiveFileInfo.
        /// </summary>
        /// <returns>A System.String that represents the current ArchiveFileInfo.</returns>
        public override string ToString()
        {
            return "[" + Index.ToString(CultureInfo.CurrentCulture) + "] " + FileName;
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
    /// Archive property struct.
    /// </summary>
    public struct ArchiveProperty
    {
        /// <summary>
        /// Gets the name of the archive property.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the value of the archive property.
        /// </summary>
        public object Value { get; internal set; }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current ArchiveProperty.
        /// </summary>
        /// <param name="obj">The System.Object to compare with the current ArchiveProperty.</param>
        /// <returns>true if the specified System.Object is equal to the current ArchiveProperty; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return (obj is ArchiveProperty) ? Equals((ArchiveProperty) obj) : false;
        }

        /// <summary>
        /// Determines whether the specified ArchiveProperty is equal to the current ArchiveProperty.
        /// </summary>
        /// <param name="afi">The ArchiveProperty to compare with the current ArchiveProperty.</param>
        /// <returns>true if the specified ArchiveProperty is equal to the current ArchiveProperty; otherwise, false.</returns>
        public bool Equals(ArchiveProperty afi)
        {
            return afi.Name == Name && afi.Value == Value;
        }

        /// <summary>
        ///  Serves as a hash function for a particular type.
        /// </summary>
        /// <returns> A hash code for the current ArchiveProperty.</returns>
        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        /// <summary>
        /// Returns a System.String that represents the current ArchiveProperty.
        /// </summary>
        /// <returns>A System.String that represents the current ArchiveProperty.</returns>
        public override string ToString()
        {
            return Name + " = " + Value;
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
    /// Zip encryption method enum.
    /// </summary>
    public enum ZipEncryptionMethod
    {
        /// <summary>
        /// ZipCrypto encryption method.
        /// </summary>
        ZipCrypto,
        /// <summary>
        /// AES 128 bit encryption method.
        /// </summary>
        Aes128,
        /// <summary>
        /// AES 192 bit encryption method.
        /// </summary>
        Aes192,
        /// <summary>
        /// AES 256 bit encryption method.
        /// </summary>
        Aes256
    }

    /// <summary>
    /// Archive update data for UpdateCallback.
    /// </summary>
    internal struct UpdateData
    {
        public uint FilesCount;
        public InternalCompressionMode Mode;

        public Dictionary<int, string> FileNamesToModify { get; set; }

        public List<ArchiveFileInfo> ArchiveFileData { get; set; }
    }
#endif
#endif
}