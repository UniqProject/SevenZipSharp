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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Reflection;
using SevenZip.ComRoutines;

namespace SevenZip
{
    /// <summary>
    /// Exception class for 7-zip library operations
    /// </summary>
    [Serializable]
    public class SevenZipLibraryException : SevenZipException
    {
        /// <summary>
        /// Exception dafault message which is displayed if no extra information is specified
        /// </summary>
        public static readonly string DefaultMessage = "Can not load 7-zip library or internal COM error!";
        /// <summary>
        /// Initializes a new instance of the SevenZipLibraryException class
        /// </summary>
        public SevenZipLibraryException() : base(DefaultMessage) { }
        /// <summary>
        /// Initializes a new instance of the SevenZipLibraryException class
        /// </summary>
        /// <param name="message">Additional detailed message</param>
        public SevenZipLibraryException(string message) : base(DefaultMessage, message) { }
        /// <summary>
        /// Initializes a new instance of the SevenZipLibraryException class
        /// </summary>
        /// <param name="message">Additional detailed message</param>
        /// <param name="inner">Inner exception occured</param>
        public SevenZipLibraryException(string message, Exception inner) : base(DefaultMessage, message, inner) { }
        /// <summary>
        /// Initializes a new instance of the SevenZipLibraryException class
        /// </summary>
        /// <param name="info">All data needed for serialization or deserialization</param>
        /// <param name="context">Serialized stream descriptor</param>
        protected SevenZipLibraryException(
            SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    internal sealed class UsersDictionary<T> : Dictionary<Enum, List<object>>
    {
        public bool HasUsers
        {
            get
            {
                bool result = false;
                int enumMax = Formats.GetMaxValue(typeof(T));
                //"typeof(T) is InArchiveFormat" does not work
                bool TisInArchiveFormat = typeof(T).FullName == typeof(InArchiveFormat).FullName;
                for (int i = 0; i <= enumMax; i++)
                {
                    if (TisInArchiveFormat)
                    {
                        result |= this[(InArchiveFormat)i] != null ?
                            this[(InArchiveFormat)i].Count != 0 : false;
                    }
                    else
                    {
                        result |= this[(OutArchiveFormat)i] != null ?
                            this[(OutArchiveFormat)i].Count != 0 : false;
                    }
                }
                return result;
            }
        }
    }

    /// <summary>
    /// 7-zip library low-level wrapper
    /// </summary>
    internal static class SevenZipLibraryManager
    {
        /// <summary>
        /// Path to the 7-zip dll
        /// </summary>
        /// <remarks>7zxa.dll supports only decoding from .7z archives.
        /// Features of 7za.dll: 
        ///     - Supporting 7z format;
        ///     - Built encoders: LZMA, PPMD, BCJ, BCJ2, COPY, AES-256 Encryption.
        ///     - Built decoders: LZMA, PPMD, BCJ, BCJ2, COPY, AES-256 Encryption, BZip2, Deflate.
        /// 7z.dll (from the 7-zip distribution) supports every InArchiveFormat for encoding and decoding.
        /// </remarks>
        private static string LibraryFileName = String.Concat(Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location), @"\\7z.dll");
        /// <summary>
        /// 7-zip library handle
        /// </summary>
        private static IntPtr _ModulePtr;
        private static int _UsersCount;
        [ThreadStatic]
        private static Dictionary<InArchiveFormat, IInArchive> _InArchives;
        [ThreadStatic]
        private static Dictionary<OutArchiveFormat, IOutArchive> _OutArchives;
        /// <summary>
        /// List of InArchiveFormat users, used to control COM resources
        /// </summary>
        [ThreadStatic]
        private static UsersDictionary<InArchiveFormat> _InUsers;
        /// <summary>
        /// List of OutArchiveFormat users, used to control COM resources
        /// </summary>
        [ThreadStatic]
        private static UsersDictionary<OutArchiveFormat> _OutUsers;      

        private static void Init()
        {            
            _InArchives = new Dictionary<InArchiveFormat, IInArchive>();
            _InUsers = new UsersDictionary<InArchiveFormat>();
            for (int i = 0; i <= Formats.GetMaxValue(typeof(InArchiveFormat)); i++)
            {
                _InArchives.Add((InArchiveFormat)i, null);
                _InUsers.Add((InArchiveFormat)i, new List<object>());
            }
            _OutArchives = new Dictionary<OutArchiveFormat, IOutArchive>();
            _OutUsers = new UsersDictionary<OutArchiveFormat>();
            for (int i = 0; i <= Formats.GetMaxValue(typeof(OutArchiveFormat)); i++)
            {
                 _OutArchives.Add((OutArchiveFormat)i, null);
                 _OutUsers.Add((OutArchiveFormat)i, new List<object>());
            }            
        }

        /// <summary>
        /// Loads the 7-zip library if necessary and adds user to the reference list
        /// </summary>
        /// <param name="user">Caller of the function</param>
        /// <param name="format">Archive format</param>
        public static void LoadLibrary(object user, Enum format)
        {
            if (_InArchives == null || _OutArchives == null)
            {
                Init();
            }
            if (_ModulePtr == IntPtr.Zero)
            {
                if (!File.Exists(LibraryFileName))
                {
                    throw new SevenZipLibraryException("DLL file does not exist.");
                }
                if ((_ModulePtr = NativeMethods.LoadLibrary(LibraryFileName))
                        == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException("failed to load library.");
                }
                if (NativeMethods.GetProcAddress(_ModulePtr, "GetHandlerProperty") == IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_ModulePtr);
                    throw new SevenZipLibraryException("library is invalid.");
                }
            }
            if (format is InArchiveFormat)
            {
                if (!_InUsers[format].Contains(user))
                {
                    _InUsers[format].Add(user);
                    _UsersCount++;
                }
                return;
            }
            if (format is OutArchiveFormat)
            {
                if (!_OutUsers[format].Contains(user))
                {
                    _OutUsers[format].Add(user);
                    _UsersCount++;
                }
                return;
            }
            throw new ArgumentException(
                "Enum " + format.ToString() + " is not a valid archive format attribute!");
        }

        /// <summary>
        /// Removes user from reference list and frees the 7-zip library if it becomes empty
        /// </summary>
        /// <param name="user">Caller of the function</param>
        /// <param name="format">Archive format</param>
        public static void FreeLibrary(object user, Enum format)
        {
            SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
            sp.Demand();
            if (_ModulePtr != IntPtr.Zero)
            {
                if (format is InArchiveFormat && _InUsers != null)
                {
                    _InUsers[format].Remove(user);
                    if (_InUsers[format].Count == 0)
                    {                            
                        if (_InArchives != null && _InArchives[(InArchiveFormat)format] != null)
                        {
                            // Suggested by gregshutdown
                            //Marshal.ReleaseComObject(_InArchives[(InArchiveFormat)format]);
                            _UsersCount--;
                        }
                    }
                }
                if (format is OutArchiveFormat && _OutUsers != null)
                {
                    _OutUsers[format].Remove(user);
                    if (_OutUsers[format].Count == 0)
                    {
                        if (_OutArchives != null && _OutArchives[(OutArchiveFormat)format] != null)
                        {
                            // Suggested by gregshutdown
                            //Marshal.ReleaseComObject(_OutArchives[(OutArchiveFormat)format]);
                            _UsersCount--;
                        }
                    }
                }
                if (_UsersCount == 0)
                {
                    NativeMethods.FreeLibrary(_ModulePtr);
                    _ModulePtr = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets IInArchive interface for 7-zip archive handling
        /// </summary>
        /// <param name="format">Archive format</param>
        public static IInArchive InArchive(InArchiveFormat format)
        {
            SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
            sp.Demand();
            if (_InArchives[format] == null)
            {
                if (_ModulePtr == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException();
                }
                NativeMethods.CreateObjectDelegate CreateObject =
                    (NativeMethods.CreateObjectDelegate)Marshal.GetDelegateForFunctionPointer(
                    NativeMethods.GetProcAddress(_ModulePtr, "CreateObject"),
                    typeof(NativeMethods.CreateObjectDelegate));
                if (CreateObject == null)
                {
                    throw new SevenZipLibraryException();
                }
                object Result;
                Guid interfaceId = typeof(IInArchive).GUID;
                Guid classID = Formats.InFormatGuids[format];
                CreateObject(ref classID, ref interfaceId, out Result);
                _InArchives[format] = Result as IInArchive;
            }
            return _InArchives[format];
        }
        /// <summary>
        /// Gets IOutArchive interface for 7-zip archive packing
        /// </summary>
        /// <param name="format">Archive format</param>  
        public static IOutArchive OutArchive(OutArchiveFormat format)
        {
            SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
            sp.Demand();
            if (_OutArchives[format] == null)
            {
                if (_ModulePtr == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException();
                }
                NativeMethods.CreateObjectDelegate CreateObject =
                    (NativeMethods.CreateObjectDelegate)Marshal.GetDelegateForFunctionPointer(
                    NativeMethods.GetProcAddress(_ModulePtr, "CreateObject"),
                    typeof(NativeMethods.CreateObjectDelegate));
                if (CreateObject == null)
                {
                    throw new SevenZipLibraryException();
                }
                object Result;
                Guid interfaceId = typeof(IOutArchive).GUID;
                Guid classID = Formats.OutFormatGuids[format];
                CreateObject(ref classID, ref interfaceId, out Result);
                _OutArchives[format] = Result as IOutArchive;
            }
            return _OutArchives[format];
        }

        public static void SetLibraryPath(string libraryPath)
        {
            if (_ModulePtr != IntPtr.Zero)
            {
                throw new SevenZipLibraryException(
                    "can not change the library path while the library\"" + LibraryFileName + "\"is being used.");
            }
            else
            {
                if (!File.Exists(libraryPath))
                {
                    throw new SevenZipLibraryException(
                    "can not change the library path because the file\"" + libraryPath + "\"does not exist.");
                }
                LibraryFileName = libraryPath;
            }
        }
    }
}
