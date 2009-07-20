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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Reflection;
using SevenZip.ComRoutines;

namespace SevenZip
{     
    #if UNMANAGED
    /// <summary>
    /// 7-zip library low-level wrapper.
    /// </summary>
    internal static class SevenZipLibraryManager
    {
        /// <summary>
        /// Path to the 7-zip dll.
        /// </summary>
        /// <remarks>7zxa.dll supports only decoding from .7z archives.
        /// Features of 7za.dll: 
        ///     - Supporting 7z format;
        ///     - Built encoders: LZMA, PPMD, BCJ, BCJ2, COPY, AES-256 Encryption.
        ///     - Built decoders: LZMA, PPMD, BCJ, BCJ2, COPY, AES-256 Encryption, BZip2, Deflate.
        /// 7z.dll (from the 7-zip distribution) supports every InArchiveFormat for encoding and decoding.
        /// </remarks>
        private static string _LibraryFileName = ConfigurationManager.AppSettings["7zLocation"]?? Path.Combine(Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location),  "7z.dll");
        /// <summary>
        /// 7-zip library handle
        /// </summary>
        private static IntPtr _ModulePtr;
        [ThreadStatic]
        private static Dictionary<object, Dictionary<InArchiveFormat, IInArchive>> _InArchives;
        #if COMPRESS
        [ThreadStatic]
        private static Dictionary<object, Dictionary<OutArchiveFormat, IOutArchive>> _OutArchives;
        #endif

        private static string _LibraryVersion;
        private static bool? _ModifyCapabale;

        private static void InitUserInFormat(object user, InArchiveFormat format)
        {
            if (!_InArchives.ContainsKey(user))
            {
                _InArchives.Add(user, new Dictionary<InArchiveFormat, IInArchive>());
            }
            if (!_InArchives[user].ContainsKey(format))
            {
                _InArchives[user].Add(format, null);
            }
        }

        #if COMPRESS
        private static void InitUserOutFormat(object user, OutArchiveFormat format)
        {
            if (!_OutArchives.ContainsKey(user))
            {
                _OutArchives.Add(user, new Dictionary<OutArchiveFormat, IOutArchive>());
            }
            if (!_OutArchives[user].ContainsKey(format))
            {
                _OutArchives[user].Add(format, null);
            }
        }
        #endif

        private static void Init()
        {            
            _InArchives = new Dictionary<object, Dictionary<InArchiveFormat, IInArchive>>();
            #if COMPRESS
            _OutArchives = new Dictionary<object, Dictionary<OutArchiveFormat, IOutArchive>>();
            #endif
        }

        /// <summary>
        /// Loads the 7-zip library if necessary and adds user to the reference list
        /// </summary>
        /// <param name="user">Caller of the function</param>
        /// <param name="format">Archive format</param>
        public static void LoadLibrary(object user, Enum format)
        {
            if (_InArchives == null 
                #if COMPRESS
                || _OutArchives == null
                #endif
                )
            {
                Init();
            }
            if (_ModulePtr == IntPtr.Zero)
            {
                if (!File.Exists(_LibraryFileName))
                {
                    throw new SevenZipLibraryException("DLL file does not exist.");
                }
                if ((_ModulePtr = NativeMethods.LoadLibrary(_LibraryFileName))
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
                InitUserInFormat(user, (InArchiveFormat)format);
                return;
            }
            #if COMPRESS
            if (format is OutArchiveFormat)
            {
                InitUserOutFormat(user, (OutArchiveFormat)format);
                return;
            }
            #endif
            throw new ArgumentException(
                "Enum " + format.ToString() + " is not a valid archive format attribute!");
        }

        /// <summary>
        /// Gets the native 7zip library version string.
        /// </summary>
        public static string LibraryVersion
        {
            get
            {
                if (String.IsNullOrEmpty(_LibraryVersion))
                {
                    FileVersionInfo dllVersionInfo = FileVersionInfo.GetVersionInfo(_LibraryFileName);
                    _LibraryVersion = String.Format("{0}.{1}", dllVersionInfo.FileMajorPart, dllVersionInfo.FileMinorPart);
                }
                return _LibraryVersion;
            }
        }

        /// <summary>
        /// Gets the value indicating whether the library supports modifying archives.
        /// </summary>
        public static bool ModifyCapable
        {
            get
            {
                if (!_ModifyCapabale.HasValue)
                {
                    FileVersionInfo dllVersionInfo = FileVersionInfo.GetVersionInfo(_LibraryFileName);
                    _ModifyCapabale = dllVersionInfo.FileMajorPart >= 9;
                }
                return _ModifyCapabale.Value;
            }
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
                if (format is InArchiveFormat)
                {
                    if (_InArchives != null && _InArchives.ContainsKey(user) &&
                        _InArchives[user].ContainsKey((InArchiveFormat)format) &&
                        _InArchives[user][(InArchiveFormat)format] != null)
                    {
                        try
                        {
                            Marshal.ReleaseComObject(_InArchives[user][(InArchiveFormat)format]);
                        }
                        catch (InvalidComObjectException) { }
                        _InArchives[user].Remove((InArchiveFormat)format);
                        if (_InArchives[user].Count == 0)
                        {
                            _InArchives.Remove(user);
                        }
                    }                   
                }
                #if COMPRESS
                if (format is OutArchiveFormat)
                {
                    if (_OutArchives != null && _OutArchives.ContainsKey(user) &&
                        _OutArchives[user].ContainsKey((OutArchiveFormat)format) &&
                        _OutArchives[user][(OutArchiveFormat)format] != null)
                    {
                        try
                        {
                            Marshal.ReleaseComObject(_OutArchives[user][(OutArchiveFormat)format]);
                        }
                        catch (InvalidComObjectException) { }
                        _OutArchives[user].Remove((OutArchiveFormat)format);
                        if (_OutArchives[user].Count == 0)
                        {
                            _OutArchives.Remove(user);
                        }
                    }
                }
                #endif
                if ((_InArchives == null || _InArchives.Count == 0)
                    #if COMPRESS
                     && (_OutArchives == null || _OutArchives.Count == 0)
                    #endif
                    )
                {
                    _InArchives = null;
                    #if COMPRESS
                    _OutArchives = null;
                    #endif
                    NativeMethods.FreeLibrary(_ModulePtr);
                    _ModulePtr = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets IInArchive interface for 7-zip archive handling
        /// </summary>
        /// <param name="format">Archive format.</param>
        /// <param name="user">Archive format user.</param>
        public static IInArchive InArchive(InArchiveFormat format, object user)
        {
            if (_InArchives[user][format] == null)
            {
                SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
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
                if (Result == null)
                {
                    throw new SevenZipLibraryException("Your 7-zip library does not support this archive type.");
                }
                InitUserInFormat(user, format);
                _InArchives[user][format] = Result as IInArchive;
            }
            return _InArchives[user][format];
        }

        #if COMPRESS
        /// <summary>
        /// Gets IOutArchive interface for 7-zip archive packing
        /// </summary>
        /// <param name="format">Archive format.</param>  
        /// <param name="user">Archive format user.</param>
        public static IOutArchive OutArchive(OutArchiveFormat format, object user)
        {            
            if (_OutArchives[user][format] == null)
            {
                SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
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
                if (Result == null)
                {
                    throw new SevenZipLibraryException("Your 7-zip library does not support this archive type.");
                }
                InitUserOutFormat(user, format);
                _OutArchives[user][format] = Result as IOutArchive;
            }
            return _OutArchives[user][format];
        }
        #endif

        public static void SetLibraryPath(string libraryPath)
        {
            if (_ModulePtr != IntPtr.Zero)
            {
                throw new SevenZipLibraryException(
                    "can not change the library path while the library\"" + _LibraryFileName + "\"is being used.");
            }
            else
            {
                if (!File.Exists(libraryPath))
                {
                    throw new SevenZipLibraryException(
                    "can not change the library path because the file\"" + libraryPath + "\"does not exist.");
                }
                _LibraryFileName = libraryPath;
            }
        }
    }
    #endif
}
