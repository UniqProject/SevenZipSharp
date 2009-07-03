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
using SevenZip.ComRoutines;
using SevenZip.Sdk;
using SevenZip.Sdk.Compression.Lzma;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace SevenZip
{
    #if COMPRESS
    /// <summary>
    /// Class for packing files into 7-zip archives
    /// </summary>
    public sealed class SevenZipCompressor 
        #if UNMANAGED
        : SevenZipBase, ISevenZipCompressor
        #endif
    {
        #if UNMANAGED
        private bool _CompressingFilesOnDisk;
        private CompressionLevel _CompressionLevel = CompressionLevel.Normal;
        private OutArchiveFormat _ArchiveFormat = OutArchiveFormat.SevenZip;
        private CompressionMethod _CompressionMethod = CompressionMethod.Default;
        private Dictionary<string, string> _CustomParameters = new Dictionary<string,string>();
        private int _VolumeSize;
        private string _ArchiveName;
        private bool _IncludeEmptyDirectories;
        private bool _PreserveDirectoryRoot;
        private bool _DirectoryStructure;
        private bool _DirectoryCompress;
        private CompressionMode _Mode;
        private uint _OldFilesCount;
        private static readonly string TempFolderPath = 
            Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User) + "\\";
        #endif
        private static int _LzmaDictionarySize = 1 << 22;

        #if UNMANAGED
        /// <summary>
        /// Changes the path to the 7-zip native library
        /// </summary>
        /// <param name="libraryPath">The path to the 7-zip native library</param>
        public static void SetLibraryPath(string libraryPath)
        {
            SevenZipLibraryManager.SetLibraryPath(libraryPath);
        }

        /// <summary>
        /// Initializes a new instance of the SevenZipCompressor class 
        /// </summary>
        public SevenZipCompressor() : base() 
        {
            _DirectoryStructure = true;
        }              
        #endif

        /// <summary>
        /// Checks if the specified stream supports compression.
        /// </summary>
        /// <param name="stream">The stream to check.</param>
        private static void ValidateStream(Stream stream)
        {
            if (!stream.CanWrite || !stream.CanSeek)
            {
                throw new ArgumentException("The specified stream can not seek or is not writable.", "stream");            
            }
        }

        #if UNMANAGED
        #region Private utilities.

        private IOutArchive MakeOutArchive(IInStream inArchiveStream)
        {
            IInArchive inArchive = SevenZipLibraryManager.InArchive(
                                Formats.InForOutFormats[_ArchiveFormat], this);
            using (ArchiveOpenCallback OpenCallback = GetArchiveOpenCallback())
            {
                ulong CheckPos = 1 << 15;
                if (inArchive.Open(inArchiveStream, ref CheckPos, OpenCallback) != (int)OperationResult.Ok)
                {
                    if (!ThrowException(null, new SevenZipArchiveException("Can not update the archive: Open() failed.")))
                    {
                        return null;
                    }
                }
                _OldFilesCount = inArchive.GetNumberOfItems();
            }
            return (IOutArchive)inArchive;
        }

        /// <summary>
        /// Guaranties the correct work of the SetCompressionProperties function
        /// </summary>
        /// <param name="method">The compression method to check</param>
        /// <returns>The value indicating whether the specified method is valid for the current ArchiveFormat</returns>
        private bool MethodIsValid(CompressionMethod method)
        {
            if (method == CompressionMethod.Default)
            {
                return true;
            }
            switch (_ArchiveFormat)
            {
                case OutArchiveFormat.Zip:
                    return method != CompressionMethod.Ppmd;
                case OutArchiveFormat.GZip:
                    return method == CompressionMethod.Deflate;
                case OutArchiveFormat.BZip2:
                    return method == CompressionMethod.BZip2;
                case OutArchiveFormat.SevenZip:
                    return method != CompressionMethod.Deflate && method != CompressionMethod.Deflate64;
                case OutArchiveFormat.Tar:
                    return method == CompressionMethod.Copy;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Sets the compression properties
        /// </summary>
        private void SetCompressionProperties()
        {            
            switch (_ArchiveFormat)
            {
                case OutArchiveFormat.Tar:
                    break;
                default:
                    ISetProperties setter = _Mode == CompressionMode.Create ?
                        (ISetProperties)SevenZipLibraryManager.OutArchive(_ArchiveFormat, this) :
                        (ISetProperties)SevenZipLibraryManager.InArchive(
                        Formats.InForOutFormats[_ArchiveFormat], this);
                    if (setter == null)
                    {
                        if (!ThrowException(null, new CompressionFailedException("The specified archive format is unsupported.")))
                        {
                            return;
                        }
                    }
                    if (_CustomParameters.ContainsKey("x") || _CustomParameters.ContainsKey("m"))
                    {
                        if (!ThrowException(null, new CompressionFailedException("The specified compression parameters are invalid.")))
                        {
                            return;
                        }
                    }
                    IntPtr[] names;
                    PropVariant[] values;
                    SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                    sp.Demand();
                    #region Initialize compression properties
                    if (_CompressionMethod == CompressionMethod.Default)
                    {
                        names = new IntPtr[1 + _CustomParameters.Count];
                        names[0] = Marshal.StringToBSTR("x");
                        values = new PropVariant[1 + _CustomParameters.Count];
                        int i = 1;
                        foreach (string key in _CustomParameters.Keys)
                        {
                            names[i] = Marshal.StringToBSTR(key);
                            if (key == "fb" || key == "pass" || key == "d")
                            {
                                values[i].VarType = VarEnum.VT_UI4;
                                values[i++].UInt32Value = Convert.ToUInt32(_CustomParameters[key], System.Globalization.CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                values[i].VarType = VarEnum.VT_BSTR;
                                values[i++].Value = Marshal.StringToBSTR(_CustomParameters[key]);
                            }
                        }
                    }
                    else
                    {
                        names = new IntPtr[2 + _CustomParameters.Count];
                        names[0] = Marshal.StringToBSTR("x");
                        names[1] = _ArchiveFormat == OutArchiveFormat.Zip ? Marshal.StringToBSTR("m") : Marshal.StringToBSTR("0");
                        values = new PropVariant[2 + _CustomParameters.Count];
                        values[1].VarType = VarEnum.VT_BSTR;
                        values[1].Value = Marshal.StringToBSTR(Formats.MethodNames[_CompressionMethod]);
                        int i = 2;
                        foreach (string key in _CustomParameters.Keys)
                        {
                            names[i] = Marshal.StringToBSTR(key);
                            if (key == "fb" || key == "pass" || key == "d")
                            {
                                values[i].VarType = VarEnum.VT_UI4;
                                values[i++].UInt32Value = Convert.ToUInt32(_CustomParameters[key], System.Globalization.CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                values[i].VarType = VarEnum.VT_BSTR;
                                values[i++].Value = Marshal.StringToBSTR(_CustomParameters[key]);
                            }
                        }
                    }
                    #endregion
                    #region Set compression level
                    values[0].VarType = VarEnum.VT_UI4;
                    switch (_CompressionLevel)
                    {
                        case CompressionLevel.None:
                            values[0].UInt32Value = 0;
                            break;
                        case CompressionLevel.Fast:
                            values[0].UInt32Value = 1;
                            break;
                        case CompressionLevel.Low:
                            values[0].UInt32Value = 3;
                            break;
                        case CompressionLevel.Normal:
                            values[0].UInt32Value = 5;
                            break;
                        case CompressionLevel.High:
                            values[0].UInt32Value = 7;
                            break;
                        case CompressionLevel.Ultra:
                            values[0].UInt32Value = 9;
                            break;
                    }
                    #endregion
                    GCHandle NamesHandle = GCHandle.Alloc(names, GCHandleType.Pinned);
                    GCHandle ValuesHandle = GCHandle.Alloc(values, GCHandleType.Pinned);
                    try
                    {
                        setter.SetProperties(NamesHandle.AddrOfPinnedObject(), ValuesHandle.AddrOfPinnedObject(), names.Length);
                    }
                    finally
                    {                        
                        NamesHandle.Free();
                        ValuesHandle.Free();
                    }
                    break;
            }
            
        }

        /// <summary>
        /// Finds the common root of file names
        /// </summary>
        /// <param name="files">Array of file names</param>
        /// <returns>Common root</returns>
        private static int CommonRoot(string[] files)
        {
            List<string[]> splittedFileNames = new List<string[]>(files.Length);
            foreach (string fn in files)
            {
                splittedFileNames.Add(fn.Split(Path.DirectorySeparatorChar));
            }
            int minSplitLength = splittedFileNames[0].Length - 1;
            if (files.Length > 1)
            {
                for (int i = 1; i < files.Length; i++)
                {
                    if (minSplitLength > splittedFileNames[i].Length)
                    {
                        minSplitLength = splittedFileNames[i].Length;
                    }
                }
            }
            string res = "";
            for (int i = 0; i < minSplitLength; i++)
            {
                bool common = true;
                for (int j = 1; j < files.Length; j++)
                {
                    if (!(common &= splittedFileNames[j - 1][i] == splittedFileNames[j][i]))
                    {
                        break;
                    }
                }
                if (common)
                {
                    res += splittedFileNames[0][i] + Path.DirectorySeparatorChar;
                }
                else
                {
                    break;
                }
            }
            return res.Length;
        }

        /// <summary>
        /// Validates the common root
        /// </summary>
        /// <param name="commonRootLength">The length of the common root of the file names.</param>
        /// <param name="files">Array of file names</param>
        private static void CheckCommonRoot(string[] files, ref int commonRootLength)
        {
            string commonRoot = "";
            try
            {
                commonRoot = files[0].Substring(0, commonRootLength);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new SevenZipInvalidFileNamesException("invalid common root.");
            }
            if (commonRoot.EndsWith(new string(Path.DirectorySeparatorChar, 1), StringComparison.CurrentCulture))
            {
                commonRoot = commonRoot.Substring(0, commonRootLength - 1);
                commonRootLength--;
            }
            foreach (string fn in files)
            {
                if (!fn.StartsWith(commonRoot, StringComparison.CurrentCulture))
                {
                    throw new SevenZipInvalidFileNamesException("invalid common root.");
                }
            }
        }

        /// <summary>
        /// Ensures that directory directory is not empty
        /// </summary>
        /// <param name="directory">Directory name</param>
        /// <returns>False if is not empty</returns>
        private bool RecursiveDirectoryEmptyCheck(string directory)
        {
            DirectoryInfo di = new DirectoryInfo(directory);
            if (di.GetFiles().Length > 0)
            {
                return false;
            }
            bool empty = true;
            foreach (DirectoryInfo cdi in di.GetDirectories())
            {
                empty &= RecursiveDirectoryEmptyCheck(cdi.FullName);
                if (!empty)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Makes special FileInfo array for the archive file table.
        /// </summary>
        /// <param name="files">Array of files to pack.</param>
        /// <param name="commonRootLength">The length of the common root of file names</param>
        /// <param name="directoryCompress">The value indicating whether to produce the array for files in a particular directory or just for an array of files.</param>
        /// <param name="directoryStructure">Preserve directory structure.</param>
        /// <returns>Special FileInfo array for the archive file table.</returns>
        private static FileInfo[] ProduceFileInfoArray(
            string[] files, int commonRootLength, 
            bool directoryCompress, bool directoryStructure)
        {
            List<FileInfo> fis = new List<FileInfo>(files.Length);
            string commonRoot = files[0].Substring(0, commonRootLength);
            if (directoryCompress)
            {
                foreach (string fn in files)
                {
                    fis.Add(new FileInfo(fn));
                }
            }
            else
            {
                if (!directoryStructure)
                {
                    foreach (string fn in files)
                    {
                        if (!Directory.Exists(fn))
                        {
                            fis.Add(new FileInfo(fn));
                        }
                    }
                }
                else
                {
                    List<string> fns = new List<string>(files.Length);
                    CheckCommonRoot(files, ref commonRootLength);
                    if (commonRootLength > 0)
                    {
                        commonRootLength++;
                        foreach (string f in files)
                        {
                            string[] splittedAfn = f.Substring(commonRootLength).Split(Path.DirectorySeparatorChar);
                            string cfn = commonRoot;
                            for (int i = 0; i < splittedAfn.Length; i++)
                            {
                                cfn += Path.DirectorySeparatorChar + splittedAfn[i];
                                if (!fns.Contains(cfn))
                                {
                                    fis.Add(new FileInfo(cfn));
                                    fns.Add(cfn);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (string f in files)
                        {
                            string[] splittedAfn = f.Substring(commonRootLength).Split(Path.DirectorySeparatorChar);
                            string cfn = splittedAfn[0];
                            for (int i = 1; i < splittedAfn.Length; i++)
                            {
                                cfn += Path.DirectorySeparatorChar + splittedAfn[i];
                                if (!fns.Contains(cfn))
                                {
                                    fis.Add(new FileInfo(cfn));
                                    fns.Add(cfn);
                                }
                            }
                        }
                    }
                }
            }
            return fis.ToArray();
        }

        /// <summary>
        /// Recursive function for adding files in directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="files">List of files</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        private void AddFilesFromDirectory(string directory, List<string> files, string searchPattern)
        {
            DirectoryInfo di = new DirectoryInfo(directory);
            foreach (FileInfo fi in di.GetFiles(searchPattern))
            {
                files.Add(fi.FullName);
            }            
            foreach (DirectoryInfo cdi in di.GetDirectories())
            {
                if (_IncludeEmptyDirectories)
                {
                    files.Add(cdi.FullName);
                }
                AddFilesFromDirectory(cdi.FullName, files, searchPattern);
            }
        }
        #endregion        

        #region GetArchiveUpdateCallback overloads
        /// <summary>
        /// Produces  a new instance of ArchiveUpdateCallback class.
        /// </summary>
        /// <param name="files">Array of FileInfo - files to pack</param>
        /// <param name="rootLength">Length of the common root of file names</param>
        /// <param name="password">The archive password</param>
        /// <returns></returns>
        private ArchiveUpdateCallback GetArchiveUpdateCallback(
            FileInfo[] files, int rootLength, string password)
        {
            SetCompressionProperties();
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password)) ?
                new ArchiveUpdateCallback(files, rootLength, this, GetUpdateData(), _DirectoryStructure) :
                new ArchiveUpdateCallback(files, rootLength, password, this, GetUpdateData(), _DirectoryStructure);
            auc.FileCompressionStarted += FileCompressionStarted;
            auc.Compressing += Compressing;
            auc.FileCompressionFinished += FileCompressionFinished;
            return auc;
        }

        /// <summary>
        /// Produces  a new instance of ArchiveUpdateCallback class.
        /// </summary>
        /// <param name="inStream">The archive input stream.</param>
        /// <param name="password">The archive password.</param>
        /// <returns></returns>
        private ArchiveUpdateCallback GetArchiveUpdateCallback(Stream inStream, string password)
        {
            SetCompressionProperties();
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password)) ?
                new ArchiveUpdateCallback(inStream, this, GetUpdateData(), _DirectoryStructure) :
                new ArchiveUpdateCallback(inStream, password, this, GetUpdateData(), _DirectoryStructure);
            auc.FileCompressionStarted += FileCompressionStarted;
            auc.Compressing += Compressing;
            auc.FileCompressionFinished += FileCompressionFinished;
            return auc;
        }

        /// <summary>
        /// Produces  a new instance of ArchiveUpdateCallback class.
        /// </summary>
        /// <param name="streamDict">Dictionary&lt;file stream, name of the archive entry&gt;</param>
        /// <param name="password">The archive password</param>
        /// <returns></returns>
        private ArchiveUpdateCallback GetArchiveUpdateCallback(
            Dictionary<Stream, string> streamDict, string password)
        {
            SetCompressionProperties();
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password)) ?
                new ArchiveUpdateCallback(streamDict, this, GetUpdateData(), _DirectoryStructure) :
                new ArchiveUpdateCallback(streamDict, password, this, GetUpdateData(), _DirectoryStructure);
            auc.FileCompressionStarted += FileCompressionStarted;
            auc.Compressing += Compressing;
            auc.FileCompressionFinished += FileCompressionFinished;
            return auc;
        }
        #endregion        

        #region Service "Get" functions

        private void FreeCompressionCallback(ArchiveUpdateCallback callback)
        {
            callback.FileCompressionStarted -= FileCompressionStarted;
            callback.Compressing -= Compressing;
            callback.FileCompressionFinished -= FileCompressionFinished;
        }

        private static string GetTempArchiveFileName(string archiveName)
        {
            return TempFolderPath + Path.GetFileName(archiveName) + ".~";
        }

        private FileStream GetArchiveFileStream(string archiveName)
        {
            if (_Mode != CompressionMode.Create && !File.Exists(archiveName))
            {
                if (!ThrowException(null, new CompressionFailedException("file \"" + archiveName + "\" does not exist.")))
                {
                    return null;
                }
            }
            return _VolumeSize == 0 ? _Mode == CompressionMode.Create ?
                File.Create(archiveName) : File.Create(GetTempArchiveFileName(archiveName)) : null;
        }

        private void FinalizeUpdate()
        {
            if (_VolumeSize == 0 && _Mode != CompressionMode.Create)
            {
                File.Move(GetTempArchiveFileName(_ArchiveName), _ArchiveName);
            }
        }

        private UpdateData GetUpdateData()
        {
            UpdateData updateData = new UpdateData();
            updateData.Mode = (InternalCompressionMode)((int)_Mode);
            switch (_Mode)
            {
                case CompressionMode.Create:
                    updateData.FilesCount = UInt32.MaxValue;
                    break;
                case CompressionMode.Append:
                    updateData.FilesCount = _OldFilesCount;                   
                    break;
            }
            return updateData;
        }

        private ISequentialOutStream GetOutStream(Stream outStream)
        {
            if (!_CompressingFilesOnDisk)
            {
                return new OutStreamWrapper(outStream, false);
            }
            if (_VolumeSize == 0)
            {
                return new OutStreamWrapper(outStream, false);
            }
            else
            {
                return new OutMultiStreamWrapper(_ArchiveName, _VolumeSize);
            }
        }

        private IInStream GetInStream()
        {
            return File.Exists(_ArchiveName) && _Mode != CompressionMode.Create && _CompressingFilesOnDisk ?
                new InStreamWrapper(File.OpenRead(_ArchiveName), true) : null;
        }

        private ArchiveOpenCallback GetArchiveOpenCallback()
        {
            return String.IsNullOrEmpty(Password) ?
                new ArchiveOpenCallback(_ArchiveName) :
                new ArchiveOpenCallback(_ArchiveName, Password);
        }

        #endregion

        #region ISevenZipCompressor Members

        #region Events
        /// <summary>
        /// Occurs when the next file is going to be packed.
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an input stream for the next file to pack it</remarks>
        public event EventHandler<FileNameEventArgs> FileCompressionStarted;
        /// <summary>
        /// Occurs when the current file was compressed.
        /// </summary>
        public event EventHandler FileCompressionFinished;
        /// <summary>
        /// Occurs when data are being compressed
        /// </summary>
        /// <remarks>Use this event for accurate progress handling and various ProgressBar.StepBy(e.PercentDelta) routines</remarks>
        public event EventHandler<ProgressEventArgs> Compressing;
        /// <summary>
        /// Occurs when all files information was determined and SevenZipCompressor is about to start to compress them.
        /// </summary>
        /// <remarks>The incoming int value indicates the number of scanned files.</remarks>
        public event EventHandler<IntEventArgs> FilesFound;
        /// <summary>
        /// Occurs when the compression procedure is finished
        /// </summary>
        public event EventHandler CompressionFinished;

        private void OnCompressionFinished(EventArgs e)
        {
            if (CompressionFinished != null)
            {
                try
                {
                    CompressionFinished(this, e);
                }
                catch (Exception ex)
                {
                    base.AddException(ex);
                }
            }
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the compression level
        /// </summary>
        public CompressionLevel CompressionLevel
        {
            get
            {
                return _CompressionLevel;
            }

            set
            {
                _CompressionLevel = value;
            }
        }

        /// <summary>
        /// Gets or sets the archive format
        /// </summary>
        public OutArchiveFormat ArchiveFormat
        {
            get
            {
                return _ArchiveFormat;
            }

            set
            {
                _ArchiveFormat = value;
                if (!MethodIsValid(_CompressionMethod))
                {
                    _CompressionMethod = CompressionMethod.Default;
                }
            }
        }

        /// <summary>
        /// Gets or sets the compression method
        /// </summary>
        public CompressionMethod CompressionMethod
        {
            get
            {
                return _CompressionMethod;
            }

            set
            {
                if (!MethodIsValid(value))
                {
                    _CompressionMethod = CompressionMethod.Default;
                }
                else
                {                    
                    _CompressionMethod = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the compression mode.
        /// </summary>
        public CompressionMode CompressionMode
        {
            get
            {
                return _Mode;
            }

            set
            {
                _Mode = value;
            }
        }

        /// <summary>
        /// Gets or sets the size in bytes of an archive volume (0 for no volumes).
        /// </summary>
        public int VolumeSize
        {
            get
            {
                return _VolumeSize;
            }

            set
            {
                _VolumeSize = value > 0? value : 0;
            }
        }
        
        /// <summary>
        /// Gets the custom compression parameters - for advanced users only
        /// </summary>
        public Dictionary<string, string> CustomParameters
        {
            get
            {
                return _CustomParameters;
            }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to include empty directories to archives.
        /// </summary>
        public bool IncludeEmptyDirectories
        {
            get
            {
                return _IncludeEmptyDirectories;
            }
            set
            {
                _IncludeEmptyDirectories = value;
            }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to preserve the directory root for CompressDirectory.
        /// </summary>
        public bool PreserveDirectoryRoot
        {
            get 
            { 
                return _PreserveDirectoryRoot; 
            }
            set 
            { 
                _PreserveDirectoryRoot = value; 
            }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to preserve directory structure.
        /// </summary>
        public bool DirectoryStructure
        {
            get
            {
                return _DirectoryStructure;
            }
            set
            {
                _DirectoryStructure = value;
            }
        }
        #endregion

        #region CompressFiles function overloads

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveName">The archive file name</param>
        public void CompressFiles(
            string archiveName, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveName, "", fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive.
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveStream">The archive output stream. 
        /// Use CompressFiles(string archiveName ... ) overloads for archiving to disk.</param>       
        public void CompressFiles(
            Stream archiveStream, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveStream, "", fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRootLength">The length of the common root of the file names.</param>
        /// <param name="archiveName">The archive file name</param>
        public void CompressFiles(
             string archiveName, int commonRootLength, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveName, commonRootLength, "", fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRootLength">The length of the common root of the file names.</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressFiles(string archiveName, ... ) overloads for archiving to disk.</param>
        public void CompressFiles(
            Stream archiveStream, int commonRootLength, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveStream, commonRootLength, "", fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        public void CompressFilesEncrypted(
            string archiveName, string password, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveName, CommonRoot(fileFullNames), password, fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressFiles( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        public void CompressFilesEncrypted(
            Stream archiveStream, string password, params string[] fileFullNames)
        {
            CompressFilesEncrypted(archiveStream, CommonRoot(fileFullNames), password, fileFullNames);
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRootLength">The length of the common root of the file names.</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        public void CompressFilesEncrypted(
            string archiveName, int commonRootLength, string password, params string[] fileFullNames)
        {
            _CompressingFilesOnDisk = true;
            _ArchiveName = archiveName;
            using (FileStream fs = GetArchiveFileStream(archiveName))
            {
                if (fs == null)
                {
                    return;
                }
                CompressFilesEncrypted(fs, commonRootLength, password, fileFullNames);
            }
            FinalizeUpdate();
        }

        /// <summary>
        /// Packs files into the archive
        /// </summary>
        /// <param name="fileFullNames">Array of file names to pack</param>
        /// <param name="commonRootLength">The length of the common root of the file names.</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressFiles( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        public void CompressFilesEncrypted(
            Stream archiveStream, int commonRootLength, string password, params string[] fileFullNames)
        {
            base.ClearExceptions();
            if (fileFullNames.Length > 1 && (_ArchiveFormat == OutArchiveFormat.BZip2 || _ArchiveFormat == OutArchiveFormat.GZip))
            {
                if (!ThrowException(null, new CompressionFailedException("Can not compress more than one file in this format.")))
                {
                    return;
                }
            }
            if (_VolumeSize == 0 || !_CompressingFilesOnDisk)
            {
                ValidateStream(archiveStream);
            }
            FileInfo[] files = null;
            try
            {
                files = ProduceFileInfoArray(fileFullNames, commonRootLength, _DirectoryCompress, _DirectoryStructure);
            }
            catch (Exception e)
            {
                if (!ThrowException(null, e))
                {
                    return;
                }
            }
            _DirectoryCompress = false;
            if (FilesFound != null)
            {
                FilesFound(this, new IntEventArgs(fileFullNames.Length));
            }
            try
            {                       
                ISequentialOutStream ArchiveStream;
                using ((ArchiveStream = GetOutStream(archiveStream)) as IDisposable)
                {
                    IInStream InArchiveStream;
                    using ((InArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;                    
                        if (_Mode == CompressionMode.Create || !_CompressingFilesOnDisk)
                        {
                            SevenZipLibraryManager.LoadLibrary(this, _ArchiveFormat);
                            outArchive = SevenZipLibraryManager.OutArchive(_ArchiveFormat, this);
                        }
                        else
                        {
                            // Create IInArchive, read it and convert to IOutArchive
                            SevenZipLibraryManager.LoadLibrary(
                                this, Formats.InForOutFormats[_ArchiveFormat]);
                            if ((outArchive = MakeOutArchive(InArchiveStream)) == null)
                            {
                                return;
                            }
                        }
                        using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(
                            files, commonRootLength, password))
                        {
                            try
                            {
                                CheckedExecute(
                                    outArchive.UpdateItems(
                                    ArchiveStream, (uint)files.Length + _OldFilesCount, auc),
                                    SevenZipCompressionFailedException.DefaultMessage, auc);                            
                            }
                            finally
                            {
                                FreeCompressionCallback(auc);
                            }                    
                        }  
                    }
                }                    
            }            
            finally
            {
                if (_Mode == CompressionMode.Create || !_CompressingFilesOnDisk)
                {
                    SevenZipLibraryManager.FreeLibrary(this, _ArchiveFormat);
                }
                else
                {
                    SevenZipLibraryManager.FreeLibrary(this, Formats.InForOutFormats[_ArchiveFormat]);
                    File.Delete(_ArchiveName);                    
                }
                _CompressingFilesOnDisk = false;
                OnCompressionFinished(EventArgs.Empty);                
            }
            ThrowUserException();
        }
        #endregion

        #region CompressDirectory function overloads

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        public void CompressDirectory(
            string directory, string archiveName)
        {
            CompressDirectory(directory, archiveName, "", "*.*", true);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>

        public void CompressDirectory(
            string directory, Stream archiveStream)
        {
            CompressDirectory(directory, archiveStream, "", "*.*", true);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        public void CompressDirectory(
            string directory, string archiveName, string password)
        {
            CompressDirectory(directory, archiveName, password, "*.*", true);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        public void CompressDirectory(
            string directory, Stream archiveStream, string password)
        {
            CompressDirectory(directory, archiveStream, password, "*.*", true);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, string archiveName, bool recursion)
        {
            CompressDirectory(directory, archiveName, "", "*.*", recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, Stream archiveStream, bool recursion)
        {
            CompressDirectory(directory, archiveStream, "", "*.*", recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, string archiveName,
            string searchPattern, bool recursion)
        {
            CompressDirectory(directory, archiveName, "", searchPattern, recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, Stream archiveStream,
            string searchPattern, bool recursion)
        {
            CompressDirectory(directory, archiveStream, "", searchPattern, recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>        
        /// <param name="recursion">Search for files recursively</param>
        /// <param name="password">The archive password</param>
        public void CompressDirectory(
            string directory, string archiveName,
            bool recursion, string password)
        {
            CompressDirectory(directory, archiveName, password, "*.*", recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>        
        /// <param name="recursion">Search for files recursively</param>
        /// <param name="password">The archive password</param>
        public void CompressDirectory(
            string directory, Stream archiveStream,
            bool recursion, string password)
        {
            CompressDirectory(directory, archiveStream, password, "*.*", recursion);
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, string archiveName,
            string password, string searchPattern, bool recursion)
        {
            _CompressingFilesOnDisk = true;
            _ArchiveName = archiveName;
            using (FileStream fs = GetArchiveFileStream(archiveName))
            {
                if (fs == null)
                {
                    return;
                }
                CompressDirectory(directory, fs, password, searchPattern, recursion);
            }
            FinalizeUpdate();
        }

        /// <summary>
        /// Packs files in the directory
        /// </summary>
        /// <param name="directory">Directory directory</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressDirectory( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        /// <param name="searchPattern">Search string, such as "*.txt"</param>
        /// <param name="recursion">Search for files recursively</param>
        public void CompressDirectory(
            string directory, Stream archiveStream,
            string password, string searchPattern, bool recursion)
        {            
            List<string> files = new List<string>();
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory \"" + directory + "\" does not exist!");
            }
            else
            {
                if (RecursiveDirectoryEmptyCheck(directory))
                {
                    throw new SevenZipInvalidFileNamesException("the specified directory is empty!");
                }
                if (recursion)
                {
                    AddFilesFromDirectory(directory, files, searchPattern);
                }
                else
                {
                    foreach (FileInfo fi in (new DirectoryInfo(directory)).GetFiles(searchPattern))
                    {
                        files.Add(fi.FullName);
                    }
                }
                int commonRootLength = directory.Length;
                if (directory.EndsWith("\\", StringComparison.OrdinalIgnoreCase))
                {
                    directory = directory.Substring(0, directory.Length - 1);
                }
                else
                {
                    commonRootLength++;
                }                
                if (_PreserveDirectoryRoot)
                {
                    commonRootLength = Path.GetDirectoryName(directory).Length + 1;
                }
                _DirectoryCompress = true;
                CompressFilesEncrypted(archiveStream, commonRootLength, password, files.ToArray());
            }
        }
        #endregion

        #region CompressFileDictionary overloads

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        public void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, string archiveName)
        {
            CompressFileDictionary(fileDictionary, archiveName, "");
        }

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        public void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, string archiveName, string password)
        {
            _CompressingFilesOnDisk = true;
            _ArchiveName = archiveName;
            using (FileStream fs = GetArchiveFileStream(archiveName))
            {
                if (fs == null)
                {
                    return;
                }
                CompressFileDictionary(fileDictionary, fs, password);
            }
            FinalizeUpdate();
        }

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        public void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, Stream archiveStream)
        {
            CompressFileDictionary(fileDictionary, archiveStream, "");
        }

        /// <summary>
        /// Packs the file dictionary into the archive
        /// </summary>
        /// <param name="fileDictionary">Dictionary&lt;file name, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        public void CompressFileDictionary(
            Dictionary<string, string> fileDictionary, Stream archiveStream, string password)
        {
            Dictionary<Stream, string> streamDict = new Dictionary<Stream, string>(fileDictionary.Count);
            foreach (string fn in fileDictionary.Keys)
            {
                streamDict.Add(File.OpenRead(fn), fileDictionary[fn]);
            }
            //The created streams will be automatically disposed inside.
            CompressStreamDictionary(streamDict, archiveStream, password);
        }
        #endregion

        #region CompressStreamDictionary overloads
        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        public void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, string archiveName)
        {
            CompressStreamDictionary(streamDictionary, archiveName, "");
        }

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveName">The archive file name</param>
        /// <param name="password">The archive password</param>
        public void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, string archiveName, string password)
        {
            _CompressingFilesOnDisk = true;
            _ArchiveName = archiveName;
            using (FileStream fs = GetArchiveFileStream(archiveName))
            {
                if (fs == null)
                {
                    return;
                }
                CompressStreamDictionary(streamDictionary, fs, password);
            }
            FinalizeUpdate();
        }

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        public void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, Stream archiveStream)
        {
            CompressStreamDictionary(streamDictionary, archiveStream, "");
        }

        /// <summary>
        /// Packs the stream dictionary into the archive
        /// </summary>
        /// <param name="streamDictionary">Dictionary&lt;file stream, name of the archive entrygrt;</param>
        /// <param name="archiveStream">The archive output stream.
        /// Use CompressStreamDictionary( ... string archiveName ... ) overloads for archiving to disk.</param>
        /// <param name="password">The archive password</param>
        public void CompressStreamDictionary(
            Dictionary<Stream, string> streamDictionary, Stream archiveStream, string password)
        {
            base.ClearExceptions();
            if (streamDictionary.Count > 1 && (_ArchiveFormat == OutArchiveFormat.BZip2 || _ArchiveFormat == OutArchiveFormat.GZip))
            {
                if (!ThrowException(null, new CompressionFailedException("Can not compress more than one file in this format.")))
                {
                    return;
                }
            }
            if (_VolumeSize == 0 || !_CompressingFilesOnDisk)
            {
                ValidateStream(archiveStream);
            }
            foreach (Stream stream in streamDictionary.Keys)
            {
                if (stream == null || !stream.CanSeek || !stream.CanRead)
                {
                    if (!ThrowException(null, new ArgumentException("The specified stream dictionary contains invalid streams.", "streamDictionary")))
                    {
                        return;
                    }
                }
            }
            try
            {
                ISequentialOutStream ArchiveStream;
                using ((ArchiveStream = GetOutStream(archiveStream)) as IDisposable)
                {
                    IInStream InArchiveStream;
                    using ((InArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;
                        if (_Mode == CompressionMode.Create || !_CompressingFilesOnDisk)
                        {
                            SevenZipLibraryManager.LoadLibrary(this, _ArchiveFormat);
                            outArchive = SevenZipLibraryManager.OutArchive(_ArchiveFormat, this);
                        }
                        else
                        {
                            // Create IInArchive, read it and convert to IOutArchive
                            SevenZipLibraryManager.LoadLibrary(
                                this, Formats.InForOutFormats[_ArchiveFormat]);
                            if ((outArchive = MakeOutArchive(InArchiveStream)) == null)
                            {
                                return;
                            }
                        }
                        using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(
                            streamDictionary, password))
                        {
                            try
                            {
                                CheckedExecute(outArchive.UpdateItems(
                                    ArchiveStream, (uint)streamDictionary.Count, auc),
                                    SevenZipCompressionFailedException.DefaultMessage, auc);
                            }                            
                            finally
                            {
                                FreeCompressionCallback(auc);
                            }
                        }
                    }
                }            
            }
            finally
            {
                if (_Mode == CompressionMode.Create || !_CompressingFilesOnDisk)
                {
                    SevenZipLibraryManager.FreeLibrary(this, _ArchiveFormat);
                }
                else
                {
                    SevenZipLibraryManager.FreeLibrary(this, Formats.InForOutFormats[_ArchiveFormat]);
                    File.Delete(_ArchiveName);
                }
                _CompressingFilesOnDisk = false;
                OnCompressionFinished(EventArgs.Empty);                
            }
            ThrowUserException();
        }
        #endregion

        #region CompressStream overloads
        /// <summary>
        /// Compresses the specified stream
        /// </summary>
        /// <param name="inStream">The source uncompressed stream</param>
        /// <param name="outStream">The destination compressed stream</param>
        /// <exception cref="ArgumentException">ArgumentException : specified streams are invalid.</exception>
        public void CompressStream(Stream inStream, Stream outStream)
        {
            CompressStream(inStream, outStream, "");
        }

        /// <summary>
        /// Compresses the specified stream
        /// </summary>
        /// <param name="inStream">The source uncompressed stream</param>
        /// <param name="outStream">The destination compressed stream</param>
        /// <param name="password">The archive password</param>
        /// <exception cref="ArgumentException">ArgumentException : specified streams are invalid.</exception>
        public void CompressStream(Stream inStream, Stream outStream, string password)
        {
            base.ClearExceptions();
            if (!inStream.CanSeek || !inStream.CanRead || !outStream.CanWrite)
            {
                if (!ThrowException(null, new ArgumentException("The specified streams are invalid.")))
                {
                    return;
                }
            }
            try
            {
                SevenZipLibraryManager.LoadLibrary(this, _ArchiveFormat);
                ISequentialOutStream ArchiveStream;
                using ((ArchiveStream = GetOutStream(outStream)) as IDisposable)
                {
                    using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(inStream, password))
                    {
                        try
                        {
                            CheckedExecute(
                                SevenZipLibraryManager.OutArchive(_ArchiveFormat, this).UpdateItems(
                                ArchiveStream, 1, auc),
                                SevenZipCompressionFailedException.DefaultMessage, auc);
                        }                        
                        finally
                        {
                            FreeCompressionCallback(auc);
                        }
                    }
                }
            }
            finally
            {
                SevenZipLibraryManager.FreeLibrary(this, _ArchiveFormat);
                OnCompressionFinished(EventArgs.Empty);                
            }
            ThrowUserException();
        }
    #endregion

        #region ModifyArchiveOverloads

        /// <summary>
        /// Modifies the existing archive: renames files or deletes them.
        /// </summary>
        /// <param name="archiveFileName">The archive file name.</param>
        /// <param name="newFileNames">New file names. Null value to delete the corresponding index.</param>
        /// <param name="password">The archive password.</param>
        public void ModifyArchive(string archiveFileName, Dictionary<int, string> newFileNames, string password)
        {
            base.ClearExceptions();
            if (!File.Exists(archiveFileName))
            {
                if (!ThrowException(null, new ArgumentException("The specified archive does not exist.", "archiveFileNAme")))
                {
                    return;
                }
            }
            if (newFileNames == null || newFileNames.Count == 0)
            {
                if (!ThrowException(null, new ArgumentException("Invalid new file names.", "newFileNames")))
                {
                    return;
                }
            }            
            try
            {
                ISequentialOutStream ArchiveStream;
                using ((ArchiveStream = GetOutStream(File.OpenRead(archiveFileName))) as IDisposable)
                {
                    IInStream InArchiveStream;
                    using ((InArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;
                        // Create IInArchive, read it and convert to IOutArchive
                        SevenZipLibraryManager.LoadLibrary(
                            this, Formats.InForOutFormats[_ArchiveFormat]);
                        if ((outArchive = MakeOutArchive(InArchiveStream)) == null)
                        {
                            return;
                        }
                        using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(null, 0, password))
                        {
                            try
                            {
                                CheckedExecute(
                                    outArchive.UpdateItems(
                                    ArchiveStream, _OldFilesCount, auc),
                                    SevenZipCompressionFailedException.DefaultMessage, auc);
                            }
                            finally
                            {
                                FreeCompressionCallback(auc);
                            }
                        }
                    }
                }
            }
            finally
            {
                SevenZipLibraryManager.FreeLibrary(this, Formats.InForOutFormats[_ArchiveFormat]);
                File.Delete(archiveFileName);
                _CompressingFilesOnDisk = false;
                OnCompressionFinished(EventArgs.Empty);
            }
            ThrowUserException();
        }
        #endregion

        #endregion
        #endif

        /// <summary>
        /// Gets or sets the dictionary size for the managed LZMA algorithm.
        /// </summary>
        public static int LzmaDictionarySize
        {
            get 
            { 
                return _LzmaDictionarySize; 
            }
            set 
            { 
                _LzmaDictionarySize = value; 
            }
        }

        internal static void WriteLzmaProperties(Encoder encoder)
        {
    #region LZMA properties definition
            CoderPropId[] propIDs = 
			{
				CoderPropId.DictionarySize,
				CoderPropId.PosStateBits,
				CoderPropId.LitContextBits,
				CoderPropId.LitPosBits,
				CoderPropId.Algorithm,
				CoderPropId.NumFastBytes,
				CoderPropId.MatchFinder,
				CoderPropId.EndMarker
			};
            object[] properties = 
			{
				_LzmaDictionarySize,
				2,
				3,
				0,
				2,
				256,
				"bt4",
				false
			};
    #endregion
            encoder.SetCoderProperties(propIDs, properties);
        }

        /// <summary>
        /// Compresses the specified stream with LZMA algorithm (C# inside)
        /// </summary>
        /// <param name="inStream">The source uncompressed stream</param>
        /// <param name="outStream">The destination compressed stream</param>
        /// <param name="inLength">The length of uncompressed data (null for inStream.Length)</param>
        /// <param name="codeProgressEvent">The event for handling the code progress</param>
        public static void CompressStream(Stream inStream, Stream outStream, int? inLength, EventHandler<ProgressEventArgs> codeProgressEvent)
        {
            if (!inStream.CanRead || !outStream.CanWrite)
            {
                throw new ArgumentException("The specified streams are invalid.");
            }
            Encoder encoder = new Encoder();
            WriteLzmaProperties(encoder);
            encoder.WriteCoderProperties(outStream);
            long streamSize = inLength.HasValue? inLength.Value : inStream.Length;
            for (int i = 0; i < 8; i++)
            {
                outStream.WriteByte((byte)(streamSize >> (8 * i)));
            }
            encoder.Code(inStream, outStream, -1, -1, new LzmaProgressCallback(streamSize, codeProgressEvent));
        }

        /// <summary>
        /// Compresses byte array with LZMA algorithm (C# inside)
        /// </summary>
        /// <param name="data">Byte array to compress</param>
        /// <returns>Compressed byte array</returns>
        public static byte[] CompressBytes(byte[] data)
        {
            
            using (MemoryStream inStream = new MemoryStream(data))
            {
                using (MemoryStream outStream = new MemoryStream())
                {
                    Encoder encoder = new Encoder();
                    WriteLzmaProperties(encoder);
                    encoder.WriteCoderProperties(outStream);
                    long streamSize = inStream.Length;
                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte)(streamSize >> (8 * i)));
                    encoder.Code(inStream, outStream, -1, -1, null);
                    return outStream.ToArray();
                }
            }
        }
    }
#endif
}
