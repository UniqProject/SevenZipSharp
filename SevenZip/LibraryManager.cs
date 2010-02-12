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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;

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
        private static string _libraryFileName = ConfigurationManager.AppSettings["7zLocation"] ??
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "7z.dll");

        /// <summary>
        /// 7-zip library handle.
        /// </summary>
        private static IntPtr _modulePtr;

        /// <summary>
        /// 7-zip library features.
        /// </summary>
        private static LibraryFeature? _features;

        [ThreadStatic] private static Dictionary<object, Dictionary<InArchiveFormat, IInArchive>> _inArchives;
#if COMPRESS
        [ThreadStatic] private static Dictionary<object, Dictionary<OutArchiveFormat, IOutArchive>> _outArchives;
#endif

        //private static string _LibraryVersion;
        private static bool? _modifyCapabale;

        private static void InitUserInFormat(object user, InArchiveFormat format)
        {
            if (!_inArchives.ContainsKey(user))
            {
                _inArchives.Add(user, new Dictionary<InArchiveFormat, IInArchive>());
            }
            if (!_inArchives[user].ContainsKey(format))
            {
                _inArchives[user].Add(format, null);
            }
        }

#if COMPRESS
        private static void InitUserOutFormat(object user, OutArchiveFormat format)
        {
            if (!_outArchives.ContainsKey(user))
            {
                _outArchives.Add(user, new Dictionary<OutArchiveFormat, IOutArchive>());
            }
            if (!_outArchives[user].ContainsKey(format))
            {
                _outArchives[user].Add(format, null);
            }
        }
#endif

        private static void Init()
        {
            _inArchives = new Dictionary<object, Dictionary<InArchiveFormat, IInArchive>>();
#if COMPRESS
            _outArchives = new Dictionary<object, Dictionary<OutArchiveFormat, IOutArchive>>();
#endif
        }

        /// <summary>
        /// Loads the 7-zip library if necessary and adds user to the reference list
        /// </summary>
        /// <param name="user">Caller of the function</param>
        /// <param name="format">Archive format</param>
        public static void LoadLibrary(object user, Enum format)
        {
            if (_inArchives == null
#if COMPRESS
                || _outArchives == null
#endif
                )
            {
                Init();
            }
            if (_modulePtr == IntPtr.Zero)
            {
                if (!File.Exists(_libraryFileName))
                {
                    throw new SevenZipLibraryException("DLL file does not exist.");
                }
                if ((_modulePtr = NativeMethods.LoadLibrary(_libraryFileName))
                    == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException("failed to load library.");
                }
                if (NativeMethods.GetProcAddress(_modulePtr, "GetHandlerProperty") == IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_modulePtr);
                    throw new SevenZipLibraryException("library is invalid.");
                }
            }
            if (format is InArchiveFormat)
            {
                InitUserInFormat(user, (InArchiveFormat) format);
                return;
            }
#if COMPRESS
            if (format is OutArchiveFormat)
            {
                InitUserOutFormat(user, (OutArchiveFormat) format);
                return;
            }
#endif
            throw new ArgumentException(
                "Enum " + format + " is not a valid archive format attribute!");
        }

        /*/// <summary>
        /// Gets the native 7zip library version string.
        /// </summary>
        public static string LibraryVersion
        {
            get
            {
                if (String.IsNullOrEmpty(_LibraryVersion))
                {
                    FileVersionInfo dllVersionInfo = FileVersionInfo.GetVersionInfo(_libraryFileName);
                    _LibraryVersion = String.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        "{0}.{1}",
                        dllVersionInfo.FileMajorPart, dllVersionInfo.FileMinorPart);
                }
                return _LibraryVersion;
            }
        }*/

        /// <summary>
        /// Gets the value indicating whether the library supports modifying archives.
        /// </summary>
        public static bool ModifyCapable
        {
            get
            {
                if (!_modifyCapabale.HasValue)
                {
                    FileVersionInfo dllVersionInfo = FileVersionInfo.GetVersionInfo(_libraryFileName);
                    _modifyCapabale = dllVersionInfo.FileMajorPart >= 9;
                }
                return _modifyCapabale.Value;
            }
        }

        private static string GetResourceString(string str)
        {
            return "SevenZip.arch." + str;
        }

        private static bool ExtractionBenchmark(string archiveFileName, Stream outStream)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    GetResourceString(archiveFileName));
            try
            {
                using (var extr = new SevenZipExtractor(stream))
                {
                    extr.ExtractFile(0, outStream);
                }
            }
            catch(Exception)
            {
                return false;
            }
            return true;
        }

        private static bool CompressionBenchmark(Stream inStream, Stream outStream,
            OutArchiveFormat format, CompressionMethod method)
        {
            try
            {
                var compr = new SevenZipCompressor {ArchiveFormat = format, CompressionMethod = method};
                compr.CompressStream(inStream, outStream);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static LibraryFeature CurrentLibraryFeatures
        {
            get
            {
                if (_features != null && _features.HasValue)
                {
                    return _features.Value;
                }
                _features = LibraryFeature.None;
                #region Benchmark
                #region Extraction features
                using (var outStream = new MemoryStream())
                {
                    if (ExtractionBenchmark("Test.lzma.7z", outStream))
                    {
                        _features |= LibraryFeature.Extract7z;
                    }
                    if (ExtractionBenchmark("Test.lzma2.7z", outStream))
                    {
                        _features |= LibraryFeature.Extract7zLZMA2;
                    }
                    int i = 0;
                    if (ExtractionBenchmark("Test.bzip2.7z", outStream))
                    {
                        i++;
                    }
                    if (ExtractionBenchmark("Test.ppmd.7z", outStream))
                    {
                        i++;
                        if (i == 2 && (_features & LibraryFeature.Extract7z) != 0 &&
                            (_features & LibraryFeature.Extract7zLZMA2) != 0)
                        {
                            _features |= LibraryFeature.Extract7zAll;
                        }
                    }
                    if (ExtractionBenchmark("Test.rar", outStream))
                    {
                        _features |= LibraryFeature.ExtractRar;
                    }
                    if (ExtractionBenchmark("Test.tar", outStream))
                    {
                        _features |= LibraryFeature.ExtractTar;
                    }
                    if (ExtractionBenchmark("Test.txt.bz2", outStream))
                    {
                        _features |= LibraryFeature.ExtractBzip2;
                    }
                    if (ExtractionBenchmark("Test.txt.gz", outStream))
                    {
                        _features |= LibraryFeature.ExtractGzip;
                    }
                    if (ExtractionBenchmark("Test.txt.xz", outStream))
                    {
                        _features |= LibraryFeature.ExtractXz;
                    }
                    if (ExtractionBenchmark("Test.zip", outStream))
                    {
                        _features |= LibraryFeature.ExtractZip;
                    }
                }
                #endregion
                #region Compression features
                using (var inStream = new MemoryStream())
                {
                    inStream.Write(Encoding.UTF8.GetBytes("Test"), 0, 4);
                    using (var outStream = new MemoryStream())
                    {
                        if (CompressionBenchmark(inStream, outStream, 
                            OutArchiveFormat.SevenZip, CompressionMethod.Lzma))
                        {
                            _features |= LibraryFeature.Compress7z;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.SevenZip, CompressionMethod.Lzma2))
                        {
                            _features |= LibraryFeature.Compress7zLZMA2;
                        }
                        int i = 0;
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.SevenZip, CompressionMethod.BZip2))
                        {
                            i++;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.SevenZip, CompressionMethod.Ppmd))
                        {
                            i++;
                            if (i == 2 && (_features & LibraryFeature.Compress7z) != 0 &&
                            (_features & LibraryFeature.Compress7zLZMA2) != 0)
                            {
                                _features |= LibraryFeature.Compress7zAll;
                            }
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.Zip, CompressionMethod.Default))
                        {
                            _features |= LibraryFeature.CompressZip;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.BZip2, CompressionMethod.Default))
                        {
                            _features |= LibraryFeature.CompressBzip2;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.GZip, CompressionMethod.Default))
                        {
                            _features |= LibraryFeature.CompressGzip;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.Tar, CompressionMethod.Default))
                        {
                            _features |= LibraryFeature.CompressTar;
                        }
                        if (CompressionBenchmark(inStream, outStream,
                            OutArchiveFormat.XZ, CompressionMethod.Default))
                        {
                            _features |= LibraryFeature.CompressXz;
                        }                        
                    }
                }
                #endregion
                #endregion
                if (ModifyCapable && (_features.Value & LibraryFeature.Compress7z) != 0)
                {
                    _features |= LibraryFeature.Modify;
                }
                return _features.Value;
            }
        }

        /// <summary>
        /// Removes user from reference list and frees the 7-zip library if it becomes empty
        /// </summary>
        /// <param name="user">Caller of the function</param>
        /// <param name="format">Archive format</param>
        public static void FreeLibrary(object user, Enum format)
        {
            var sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
            sp.Demand();
            if (_modulePtr != IntPtr.Zero)
            {
                if (format is InArchiveFormat)
                {
                    if (_inArchives != null && _inArchives.ContainsKey(user) &&
                        _inArchives[user].ContainsKey((InArchiveFormat) format) &&
                        _inArchives[user][(InArchiveFormat) format] != null)
                    {
                        try
                        {
                            Marshal.ReleaseComObject(_inArchives[user][(InArchiveFormat) format]);
                        }
                        catch (InvalidComObjectException) {}
                        _inArchives[user].Remove((InArchiveFormat) format);
                        if (_inArchives[user].Count == 0)
                        {
                            _inArchives.Remove(user);
                        }
                    }
                }
#if COMPRESS
                if (format is OutArchiveFormat)
                {
                    if (_outArchives != null && _outArchives.ContainsKey(user) &&
                        _outArchives[user].ContainsKey((OutArchiveFormat) format) &&
                        _outArchives[user][(OutArchiveFormat) format] != null)
                    {
                        try
                        {
                            Marshal.ReleaseComObject(_outArchives[user][(OutArchiveFormat) format]);
                        }
                        catch (InvalidComObjectException) {}
                        _outArchives[user].Remove((OutArchiveFormat) format);
                        if (_outArchives[user].Count == 0)
                        {
                            _outArchives.Remove(user);
                        }
                    }
                }
#endif
                if ((_inArchives == null || _inArchives.Count == 0)
#if COMPRESS
                    && (_outArchives == null || _outArchives.Count == 0)
#endif
                    )
                {
                    _inArchives = null;
#if COMPRESS
                    _outArchives = null;
#endif
                    NativeMethods.FreeLibrary(_modulePtr);
                    _modulePtr = IntPtr.Zero;
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
            if (_inArchives[user][format] == null)
            {
                var sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
                if (_modulePtr == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException();
                }
                var createObject =
                    (NativeMethods.CreateObjectDelegate) Marshal.GetDelegateForFunctionPointer(
                                                             NativeMethods.GetProcAddress(_modulePtr, "CreateObject"),
                                                             typeof (NativeMethods.CreateObjectDelegate));
                if (createObject == null)
                {
                    throw new SevenZipLibraryException();
                }
                object result;
                Guid interfaceId = typeof (IInArchive).GUID;
                Guid classID = Formats.InFormatGuids[format];
                try
                {
                    createObject(ref classID, ref interfaceId, out result);
                }
                catch (Exception)
                {
                    result = null;
                }
                if (result == null)
                {
                    throw new SevenZipLibraryException("Your 7-zip library does not support this archive type.");
                }
                InitUserInFormat(user, format);
                _inArchives[user][format] = result as IInArchive;
            }
            return _inArchives[user][format];
        }

#if COMPRESS
        /// <summary>
        /// Gets IOutArchive interface for 7-zip archive packing
        /// </summary>
        /// <param name="format">Archive format.</param>  
        /// <param name="user">Archive format user.</param>
        public static IOutArchive OutArchive(OutArchiveFormat format, object user)
        {
            if (_outArchives[user][format] == null)
            {
                var sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
                if (_modulePtr == IntPtr.Zero)
                {
                    throw new SevenZipLibraryException();
                }
                var createObject =
                    (NativeMethods.CreateObjectDelegate) Marshal.GetDelegateForFunctionPointer(
                                                             NativeMethods.GetProcAddress(_modulePtr, "CreateObject"),
                                                             typeof (NativeMethods.CreateObjectDelegate));
                if (createObject == null)
                {
                    throw new SevenZipLibraryException();
                }
                object result;
                Guid interfaceId = typeof (IOutArchive).GUID;
                Guid classID = Formats.OutFormatGuids[format];
                try
                {
                    createObject(ref classID, ref interfaceId, out result);
                }
                catch (Exception)
                {
                    result = null;
                }
                if (result == null)
                {
                    throw new SevenZipLibraryException("Your 7-zip library does not support this archive type.");
                }
                InitUserOutFormat(user, format);
                _outArchives[user][format] = result as IOutArchive;
            }
            return _outArchives[user][format];
        }
#endif

        public static void SetLibraryPath(string libraryPath)
        {
            if (_modulePtr != IntPtr.Zero && !Path.GetFullPath(libraryPath).Equals( 
                Path.GetFullPath(_libraryFileName), StringComparison.OrdinalIgnoreCase))
            {
                throw new SevenZipLibraryException(
                    "can not change the library path while the library \"" + _libraryFileName + "\" is being used.");
            }
            if (!File.Exists(libraryPath))
            {
                throw new SevenZipLibraryException(
                    "can not change the library path because the file \"" + libraryPath + "\" does not exist.");
            }
            _libraryFileName = libraryPath;
            _features = null;
        }
    }
#endif
}