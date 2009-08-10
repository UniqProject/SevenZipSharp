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
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using SevenZip.Sdk;
using SevenZip.Sdk.Compression.Lzma;

namespace SevenZip
{
#if COMPRESS
    /// <summary>
    /// Class for packing files into 7-zip archives
    /// </summary>
    public sealed class SevenZipCompressor
#if UNMANAGED
        : SevenZipBase
#endif
    {
#if UNMANAGED
        private bool _CompressingFilesOnDisk;
        /// <summary>
        /// Gets or sets the archiving compression level.
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; }
        private OutArchiveFormat _ArchiveFormat = OutArchiveFormat.SevenZip;
        private CompressionMethod _CompressionMethod = CompressionMethod.Default;
        /// <summary>
        /// Gets the custom compression parameters - for advanced users only.
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; private set; }
        private int _VolumeSize;
        private string _ArchiveName;
        /// <summary>
        /// Gets or sets the value indicating whether to include empty directories to archives.
        /// </summary>
        public bool IncludeEmptyDirectories { get; set; }
        /// <summary>
        /// Gets or sets the value indicating whether to preserve the directory root for CompressDirectory.
        /// </summary>
        public bool PreserveDirectoryRoot { get; set; }
        /// <summary>
        /// Gets or sets the value indicating whether to preserve directory structure.
        /// </summary>
        public bool DirectoryStructure { get; set; }
        private bool _DirectoryCompress;
        /// <summary>
        /// Gets or sets the compression mode.
        /// </summary>
        public CompressionMode CompressionMode { get; set; }
        private UpdateData _UpdateData;
        private uint _OldFilesCount;
        /// <summary>
        /// Gets or sets the value indicating whether to encrypt 7-Zip archive headers.
        /// </summary>
        public bool EncryptHeaders { get; set; }
        /// <summary>
        /// Gets or sets the value indicating whether to compress files only open for writing.
        /// </summary>
        public bool ScanOnlyWritable { get; set; }
        /// <summary>
        /// Gets or sets the encryption method for zip archives.
        /// </summary>
        public ZipEncryptionMethod ZipEncryptionMethod { get; set; }
        /// <summary>
        /// Gets or sets the temporary folder path.
        /// </summary>
        public string TempFolderPath { get; set; }
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
        public SevenZipCompressor()
        {
            DirectoryStructure = true;
            TempFolderPath = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User) + "\\";
            CompressionLevel = CompressionLevel.Normal;
            CompressionMode = CompressionMode.Create;
            ZipEncryptionMethod = ZipEncryptionMethod.ZipCrypto;
            CustomParameters = new Dictionary<string, string>();
            _UpdateData = new UpdateData();
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

        #region Private functions

        private IOutArchive MakeOutArchive(IInStream inArchiveStream)
        {
            IInArchive inArchive = SevenZipLibraryManager.InArchive(
                Formats.InForOutFormats[_ArchiveFormat], this);
            using (ArchiveOpenCallback openCallback = GetArchiveOpenCallback())
            {
                ulong checkPos = 1 << 15;
                if (inArchive.Open(inArchiveStream, ref checkPos, openCallback) != (int) OperationResult.Ok)
                {
                    if (
                        !ThrowException(null, new SevenZipArchiveException("Can not update the archive: Open() failed.")))
                    {
                        return null;
                    }
                }
                _OldFilesCount = inArchive.GetNumberOfItems();
            }
            return (IOutArchive) inArchive;
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

        private bool SwitchIsInCustomParameters(string name)
        {
            return CustomParameters.ContainsKey(name);
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
                    ISetProperties setter = CompressionMode == CompressionMode.Create && _UpdateData.FileNamesToModify == null
                                                ?
                                                    (ISetProperties)
                                                    SevenZipLibraryManager.OutArchive(_ArchiveFormat, this)
                                                :
                                                    (ISetProperties) SevenZipLibraryManager.InArchive(
                                                                         Formats.InForOutFormats[_ArchiveFormat], this);
                    if (setter == null)
                    {
                        if (!ThrowException(null,
                                            new CompressionFailedException(
                                                "The specified archive format is unsupported.")))
                        {
                            return;
                        }
                    }
                    if (CustomParameters.ContainsKey("x") || CustomParameters.ContainsKey("m"))
                    {
                        if (
                            !ThrowException(null,
                                            new CompressionFailedException(
                                                "The specified compression parameters are invalid.")))
                        {
                            return;
                        }
                    }
                    var names = new List<IntPtr>(2 + CustomParameters.Count);
                    var values = new List<PropVariant>(2 + CustomParameters.Count);
                    var sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                    sp.Demand();

                    #region Initialize compression properties

                    if (_CompressionMethod == CompressionMethod.Default)
                    {
                        names.Add(Marshal.StringToBSTR("x"));
                        values.Add(new PropVariant());
                        foreach (string key in CustomParameters.Keys)
                        {
                            names.Add(Marshal.StringToBSTR(key));
                            var pv = new PropVariant();
                            if (key == "fb" || key == "pass" || key == "d")
                            {
                                pv.VarType = VarEnum.VT_UI4;
                                pv.UInt32Value = Convert.ToUInt32(CustomParameters[key], CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                pv.VarType = VarEnum.VT_BSTR;
                                pv.Value = Marshal.StringToBSTR(CustomParameters[key]);
                            }
                            values.Add(pv);
                        }
                    }
                    else
                    {
                        names.Add(Marshal.StringToBSTR("x"));
                        names.Add(_ArchiveFormat == OutArchiveFormat.Zip
                                      ?
                                          Marshal.StringToBSTR("m")
                                      : Marshal.StringToBSTR("0"));
                        values.Add(new PropVariant());
                        var pv = new PropVariant
                                 {
                                     VarType = VarEnum.VT_BSTR,
                                     Value = Marshal.StringToBSTR(Formats.MethodNames[_CompressionMethod])
                                 };
                        values.Add(pv);
                        foreach (string key in CustomParameters.Keys)
                        {
                            names.Add(Marshal.StringToBSTR(key));
                            pv = new PropVariant();
                            if (key == "fb" || key == "pass" || key == "d")
                            {
                                pv.VarType = VarEnum.VT_UI4;
                                pv.UInt32Value = Convert.ToUInt32(CustomParameters[key], CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                pv.VarType = VarEnum.VT_BSTR;
                                pv.Value = Marshal.StringToBSTR(CustomParameters[key]);
                            }
                            values.Add(pv);
                        }
                    }

                    #endregion

                    #region Set compression level

                    PropVariant clpv = values[0];
                    clpv.VarType = VarEnum.VT_UI4;
                    switch (CompressionLevel)
                    {
                        case CompressionLevel.None:
                            clpv.UInt32Value = 0;
                            break;
                        case CompressionLevel.Fast:
                            clpv.UInt32Value = 1;
                            break;
                        case CompressionLevel.Low:
                            clpv.UInt32Value = 3;
                            break;
                        case CompressionLevel.Normal:
                            clpv.UInt32Value = 5;
                            break;
                        case CompressionLevel.High:
                            clpv.UInt32Value = 7;
                            break;
                        case CompressionLevel.Ultra:
                            clpv.UInt32Value = 9;
                            break;
                    }
                    values[0] = clpv;

                    #endregion

                    #region Encrypt headers

                    if (EncryptHeaders && _ArchiveFormat == OutArchiveFormat.SevenZip &&
                        !SwitchIsInCustomParameters("he"))
                    {
                        names.Add(Marshal.StringToBSTR("he"));
                        var tmp = new PropVariant {VarType = VarEnum.VT_BSTR, Value = Marshal.StringToBSTR("on")};
                        values.Add(tmp);
                    }

                    #endregion

                    #region Zip Encryption

                    if (_ArchiveFormat == OutArchiveFormat.Zip && ZipEncryptionMethod != ZipEncryptionMethod.ZipCrypto &&
                        !SwitchIsInCustomParameters("em"))
                    {
                        names.Add(Marshal.StringToBSTR("em"));
                        var tmp = new PropVariant
                                  {
                                      VarType = VarEnum.VT_BSTR,
                                      Value =
                                          Marshal.StringToBSTR(Enum.GetName(typeof (ZipEncryptionMethod),
                                                                            ZipEncryptionMethod))
                                  };
                        values.Add(tmp);
                    }

                    #endregion

                    GCHandle namesHandle = GCHandle.Alloc(names.ToArray(), GCHandleType.Pinned);
                    GCHandle valuesHandle = GCHandle.Alloc(values.ToArray(), GCHandleType.Pinned);
                    try
                    {
                        if (setter != null) //ReSharper
                            setter.SetProperties(namesHandle.AddrOfPinnedObject(), valuesHandle.AddrOfPinnedObject(),
                                                 names.Count);
                    }
                    finally
                    {
                        namesHandle.Free();
                        valuesHandle.Free();
                    }
                    break;
            }
        }

        /// <summary>
        /// Finds the common root of file names
        /// </summary>
        /// <param name="files">Array of file names</param>
        /// <returns>Common root</returns>
        private static int CommonRoot(ICollection<string> files)
        {
            var splittedFileNames = new List<string[]>(files.Count);
            foreach (string fn in files)
            {
                splittedFileNames.Add(fn.Split(Path.DirectorySeparatorChar));
            }
            int minSplitLength = splittedFileNames[0].Length - 1;
            if (files.Count > 1)
            {
                for (int i = 1; i < files.Count; i++)
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
                for (int j = 1; j < files.Count; j++)
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
            string commonRoot;
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
        private static bool RecursiveDirectoryEmptyCheck(string directory)
        {
            var di = new DirectoryInfo(directory);
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
            var fis = new List<FileInfo>(files.Length);
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
                    var fns = new List<string>(files.Length);
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
        private void AddFilesFromDirectory(string directory, ICollection<string> files, string searchPattern)
        {
            var di = new DirectoryInfo(directory);
            foreach (FileInfo fi in di.GetFiles(searchPattern))
            {
                if (!ScanOnlyWritable)
                {
                    files.Add(fi.FullName);
                }
                else
                {
                    try
                    {
                        using (fi.OpenWrite()) {}
                        files.Add(fi.FullName);
                    }
                    catch (IOException) {}
                }
            }
            foreach (DirectoryInfo cdi in di.GetDirectories())
            {
                if (IncludeEmptyDirectories)
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
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password))
                                            ?
                                                new ArchiveUpdateCallback(files, rootLength, this, GetUpdateData(),
                                                                          DirectoryStructure)
                                            :
                                                new ArchiveUpdateCallback(files, rootLength, password, this,
                                                                          GetUpdateData(), DirectoryStructure);
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
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password))
                                            ?
                                                new ArchiveUpdateCallback(inStream, this, GetUpdateData(),
                                                                          DirectoryStructure)
                                            :
                                                new ArchiveUpdateCallback(inStream, password, this, GetUpdateData(),
                                                                          DirectoryStructure);
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
            ArchiveUpdateCallback auc = (String.IsNullOrEmpty(password))
                                            ?
                                                new ArchiveUpdateCallback(streamDict, this, GetUpdateData(),
                                                                          DirectoryStructure)
                                            :
                                                new ArchiveUpdateCallback(streamDict, password, this, GetUpdateData(),
                                                                          DirectoryStructure);
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

        private string GetTempArchiveFileName(string archiveName)
        {
            return TempFolderPath + Path.GetFileName(archiveName) + ".~";
        }

        private FileStream GetArchiveFileStream(string archiveName)
        {
            if ((CompressionMode != CompressionMode.Create || _UpdateData.FileNamesToModify != null) && !File.Exists(archiveName))
            {
                if (
                    !ThrowException(null, new CompressionFailedException("file \"" + archiveName + "\" does not exist.")))
                {
                    return null;
                }
            }
            return _VolumeSize == 0
                       ? CompressionMode == CompressionMode.Create && _UpdateData.FileNamesToModify == null
                             ?
                                 File.Create(archiveName)
                             : File.Create(GetTempArchiveFileName(archiveName))
                       : null;
        }

        private void FinalizeUpdate()
        {
            if (_VolumeSize == 0 && (CompressionMode != CompressionMode.Create || _UpdateData.FileNamesToModify != null))
            {
                File.Move(GetTempArchiveFileName(_ArchiveName), _ArchiveName);
            }
        }

        private UpdateData GetUpdateData()
        {
            if (_UpdateData.FileNamesToModify == null)
            {
                var updateData = new UpdateData {Mode = (InternalCompressionMode) ((int) CompressionMode)};
                switch (CompressionMode)
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
            return _UpdateData;
        }

        private ISequentialOutStream GetOutStream(Stream outStream)
        {
            if (!_CompressingFilesOnDisk)
            {
                return new OutStreamWrapper(outStream, false);
            }
            if (_VolumeSize == 0 || CompressionMode != CompressionMode.Create || _UpdateData.FileNamesToModify != null)
            {
                return new OutStreamWrapper(outStream, true);
            }
            return new OutMultiStreamWrapper(_ArchiveName, _VolumeSize);
        }

        private IInStream GetInStream()
        {
            return File.Exists(_ArchiveName) &&
                   (CompressionMode != CompressionMode.Create && _CompressingFilesOnDisk || _UpdateData.FileNamesToModify != null)
                       ?
                           new InStreamWrapper(
                               new FileStream(_ArchiveName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                               true)
                       : null;
        }

        private ArchiveOpenCallback GetArchiveOpenCallback()
        {
            return String.IsNullOrEmpty(Password)
                       ?
                           new ArchiveOpenCallback(_ArchiveName)
                       :
                           new ArchiveOpenCallback(_ArchiveName, Password);
        }

        #endregion

        #region Core public Members

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
                    AddException(ex);
                }
            }
        }

        #endregion

        #region Properties        

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
                _CompressionMethod = !MethodIsValid(value) ? CompressionMethod.Default : value;
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
                _VolumeSize = value > 0 ? value : 0;
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
            ClearExceptions();
            if (fileFullNames.Length > 1 &&
                (_ArchiveFormat == OutArchiveFormat.BZip2 || _ArchiveFormat == OutArchiveFormat.GZip))
            {
                if (
                    !ThrowException(null,
                                    new CompressionFailedException("Can not compress more than one file in this format.")))
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
                files = ProduceFileInfoArray(fileFullNames, commonRootLength, _DirectoryCompress, DirectoryStructure);
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
                ISequentialOutStream sequentialArchiveStream;
                using ((sequentialArchiveStream = GetOutStream(archiveStream)) as IDisposable)
                {
                    IInStream inArchiveStream;
                    using ((inArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;
                        if (CompressionMode == CompressionMode.Create || !_CompressingFilesOnDisk)
                        {
                            SevenZipLibraryManager.LoadLibrary(this, _ArchiveFormat);
                            outArchive = SevenZipLibraryManager.OutArchive(_ArchiveFormat, this);
                        }
                        else
                        {
                            // Create IInArchive, read it and convert to IOutArchive
                            SevenZipLibraryManager.LoadLibrary(
                                this, Formats.InForOutFormats[_ArchiveFormat]);
                            if ((outArchive = MakeOutArchive(inArchiveStream)) == null)
                            {
                                return;
                            }
                        }
                        using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(
                            files, commonRootLength, password))
                        {
                            try
                            {
                                if (files != null) //ReSharper
                                    CheckedExecute(
                                        outArchive.UpdateItems(
                                            sequentialArchiveStream, (uint) files.Length + _OldFilesCount, auc),
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
                if (CompressionMode == CompressionMode.Create || !_CompressingFilesOnDisk)
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
                if (fs == null && _VolumeSize == 0)
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
            var files = new List<string>();
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Directory \"" + directory + "\" does not exist!");
            }
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
            if (PreserveDirectoryRoot)
            {
                string upperRoot = Path.GetDirectoryName(directory);
                commonRootLength = upperRoot.Length + (upperRoot.EndsWith("\\")? 0 : 1);
            }
            _DirectoryCompress = true;
            CompressFilesEncrypted(archiveStream, commonRootLength, password, files.ToArray());
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
            var streamDict = new Dictionary<Stream, string>(fileDictionary.Count);
            foreach (string fn in fileDictionary.Keys)
            {
                streamDict.Add(
                    new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    fileDictionary[fn]);
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
            ClearExceptions();
            if (streamDictionary.Count > 1 &&
                (_ArchiveFormat == OutArchiveFormat.BZip2 || _ArchiveFormat == OutArchiveFormat.GZip))
            {
                if (
                    !ThrowException(null,
                                    new CompressionFailedException("Can not compress more than one file in this format.")))
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
                    if (
                        !ThrowException(null,
                                        new ArgumentException(
                                            "The specified stream dictionary contains invalid streams.",
                                            "streamDictionary")))
                    {
                        return;
                    }
                }
            }
            try
            {
                ISequentialOutStream sequentialArchiveStream;
                using ((sequentialArchiveStream = GetOutStream(archiveStream)) as IDisposable)
                {
                    IInStream inArchiveStream;
                    using ((inArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;
                        if (CompressionMode == CompressionMode.Create || !_CompressingFilesOnDisk)
                        {
                            SevenZipLibraryManager.LoadLibrary(this, _ArchiveFormat);
                            outArchive = SevenZipLibraryManager.OutArchive(_ArchiveFormat, this);
                        }
                        else
                        {
                            // Create IInArchive, read it and convert to IOutArchive
                            SevenZipLibraryManager.LoadLibrary(
                                this, Formats.InForOutFormats[_ArchiveFormat]);
                            if ((outArchive = MakeOutArchive(inArchiveStream)) == null)
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
                                                   sequentialArchiveStream, (uint) streamDictionary.Count, auc),
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
                if (CompressionMode == CompressionMode.Create || !_CompressingFilesOnDisk)
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
            ClearExceptions();
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
                ISequentialOutStream sequentialArchiveStream;
                using ((sequentialArchiveStream = GetOutStream(outStream)) as IDisposable)
                {
                    using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(inStream, password))
                    {
                        try
                        {
                            CheckedExecute(
                                SevenZipLibraryManager.OutArchive(_ArchiveFormat, this).UpdateItems(
                                    sequentialArchiveStream, 1, auc),
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

        #region ModifyArchive overloads

        /// <summary>
        /// Modifies the existing archive: renames files or deletes them.
        /// </summary>
        /// <param name="archiveName">The archive file name.</param>
        /// <param name="newFileNames">New file names. Null value to delete the corresponding index.</param>
        public void ModifyArchive(string archiveName, Dictionary<int, string> newFileNames)
        {
            ModifyArchive(archiveName, newFileNames, "");
        }

        /// <summary>
        /// Modifies the existing archive: renames files or deletes them.
        /// </summary>
        /// <param name="archiveName">The archive file name.</param>
        /// <param name="newFileNames">New file names. Null value to delete the corresponding index.</param>
        /// <param name="password">The archive password.</param>
        public void ModifyArchive(string archiveName, Dictionary<int, string> newFileNames, string password)
        {
            ClearExceptions();
            if (!SevenZipLibraryManager.ModifyCapable)
            {
                throw new SevenZipLibraryException("The specified 7zip native library does not support this method.");
            }
            if (!File.Exists(archiveName))
            {
                if (!ThrowException(null, new ArgumentException("The specified archive does not exist.", "archiveName")))
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
                using (var extr = new SevenZipExtractor(archiveName))
                {
                    _UpdateData = new UpdateData();
                    var archiveData = new ArchiveFileInfo[extr.ArchiveFileData.Count];
                    extr.ArchiveFileData.CopyTo(archiveData, 0);
                    _UpdateData.ArchiveFileData = new List<ArchiveFileInfo>(archiveData);
                }
                _UpdateData.FileNamesToModify = newFileNames;
                _UpdateData.Mode = InternalCompressionMode.Modify;
            }
            catch (SevenZipException e)
            {
                if (!ThrowException(null, e))
                {
                    return;
                }
            }
            try
            {
                ISequentialOutStream sequentialArchiveStream;
                _CompressingFilesOnDisk = true;
                using ((sequentialArchiveStream = GetOutStream(GetArchiveFileStream(archiveName))) as IDisposable)
                {
                    IInStream inArchiveStream;
                    _ArchiveName = archiveName;
                    using ((inArchiveStream = GetInStream()) as IDisposable)
                    {
                        IOutArchive outArchive;
                        // Create IInArchive, read it and convert to IOutArchive
                        SevenZipLibraryManager.LoadLibrary(
                            this, Formats.InForOutFormats[_ArchiveFormat]);
                        if ((outArchive = MakeOutArchive(inArchiveStream)) == null)
                        {
                            return;
                        }
                        using (ArchiveUpdateCallback auc = GetArchiveUpdateCallback(null, 0, password))
                        {
                            try
                            {
                                CheckedExecute(
                                    outArchive.UpdateItems(
                                        sequentialArchiveStream, _OldFilesCount, auc),
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
                File.Delete(archiveName);
                FinalizeUpdate();
                _CompressingFilesOnDisk = false;
                _UpdateData.FileNamesToModify = null;
                _UpdateData.ArchiveFileData = null;
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
        public static void CompressStream(Stream inStream, Stream outStream, int? inLength,
                                          EventHandler<ProgressEventArgs> codeProgressEvent)
        {
            if (!inStream.CanRead || !outStream.CanWrite)
            {
                throw new ArgumentException("The specified streams are invalid.");
            }
            var encoder = new Encoder();
            WriteLzmaProperties(encoder);
            encoder.WriteCoderProperties(outStream);
            long streamSize = inLength.HasValue ? inLength.Value : inStream.Length;
            for (int i = 0; i < 8; i++)
            {
                outStream.WriteByte((byte) (streamSize >> (8*i)));
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
            using (var inStream = new MemoryStream(data))
            {
                using (var outStream = new MemoryStream())
                {
                    var encoder = new Encoder();
                    WriteLzmaProperties(encoder);
                    encoder.WriteCoderProperties(outStream);
                    long streamSize = inStream.Length;
                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte) (streamSize >> (8*i)));
                    encoder.Code(inStream, outStream, -1, -1, null);
                    return outStream.ToArray();
                }
            }
        }
    }
#endif
}