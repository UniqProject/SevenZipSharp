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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using SevenZip.ComRoutines;

namespace SevenZip
{
    #region Exceptions
    /// <summary>
    /// Exception class for ArchiveExtractCallback
    /// </summary>
    [Serializable]
    public class ExtractionFailedException : SevenZipException
    {
        public const string DefaultMessage = "Could not extract files!";
        public ExtractionFailedException() : base(DefaultMessage) { }
        public ExtractionFailedException(string message) : base(DefaultMessage, message) { }
        public ExtractionFailedException(string message, Exception inner) : base(DefaultMessage, message, inner) { }
        protected ExtractionFailedException( 
            SerializationInfo info, StreamingContext context ) : base( info, context ) { }
    }
    /// <summary>
    /// Exception class for ArchiveUpdateCallback
    /// </summary>
    [Serializable]
    public class CompressionFailedException : SevenZipException
    {
        public const string DefaultMessage = "Could not pack files!";
        public CompressionFailedException() : base(DefaultMessage) { }
        public CompressionFailedException(string message) : base(DefaultMessage, message) { }
        public CompressionFailedException(string message, Exception inner) : base(DefaultMessage, message, inner) { }
        protected CompressionFailedException( 
            SerializationInfo info, StreamingContext context ) : base( info, context ) { }
    }
    #endregion    
    #region EventArgs classes
    /// <summary>
    /// EventArgs for storing PercentDone property
    /// </summary>
    public class PercentDoneEventArgs : EventArgs
    {
        private readonly byte _PercentDone;
        /// <summary>
        /// Gets the percent of finished work
        /// </summary>
        public byte PercentDone
        {
            get
            {
                return _PercentDone;
            }
        }
        /// <summary>
        /// Initializes a new instance of the PercentDoneEventArgs class
        /// </summary>
        /// <param name="percentDone">The percent of finished work</param>
        public PercentDoneEventArgs(byte percentDone)
        {
            if (percentDone > 100 || percentDone < 0)
            {
                throw new ArgumentOutOfRangeException("The percent of finished work must be between 0 and 100.");
            }
            _PercentDone = percentDone;
        }
        /// <summary>
        /// Converts a [0, 1] rate to its percent equivalent
        /// </summary>
        /// <param name="doneRate">The rate of the done work</param>
        /// <returns>Percent integer equivalent</returns>
        internal static byte ProducePercentDone(float doneRate)
        {
            return (byte)Math.Round(100 * doneRate, MidpointRounding.AwayFromZero);
        }
    }
    /// <summary>
    /// EventArgs used to report the index of file which is going to be unpacked
    /// </summary>
    public sealed class IndexEventArgs : PercentDoneEventArgs
    {
        private readonly int _FileIndex;
        /// <summary>
        /// Gets file index in the archive file table
        /// </summary>
        public int FileIndex
        {
            get
            {
                return _FileIndex;
            }
        }
        /// <summary>
        /// Initializes a new instance of the IndexEventArgs class
        /// </summary>
        /// <param name="fileIndex">File index in the archive file table</param>
        /// <param name="percentDone">The percent of finished work</param>
        [CLSCompliantAttribute(false)]
        public IndexEventArgs(uint fileIndex, byte percentDone) 
            : base(percentDone)
        {
            _FileIndex = (int)fileIndex;
        }
    }
    /// <summary>
    /// EventArgs used to report the file information which is going to be packed
    /// </summary>
    public sealed class FileInfoEventArgs : PercentDoneEventArgs
    {
        private readonly FileInfo _FileInfo;
        /// <summary>
        /// Gets file info of the current file
        /// </summary>
        public FileInfo FileInfo
        {
            get
            {
                return _FileInfo;
            }
        }
        /// <summary>
        /// Initializes a new instance of the FileInfoEventArgs class
        /// </summary>
        /// <param name="fileInfo">File info of the current file</param>
        /// <param name="percentDone">The percent of finished work</param>
        public FileInfoEventArgs(FileInfo fileInfo, byte percentDone)
            : base(percentDone)
        {
            _FileInfo = fileInfo;            
        }
    }
    /// <summary>
    /// EventArgs used to report the size of unpacked archive data
    /// </summary>
    public sealed class OpenEventArgs : EventArgs
    {
        private ulong _TotalSize;
        /// <summary>
        /// Size of unpacked archive data
        /// </summary>
        [CLSCompliantAttribute(false)]
        public ulong TotalSize
        {
            get
            {
                return _TotalSize;
            }

            set
            {
                _TotalSize = value;
            }
        }
        /// <summary>
        /// Initializes a new instance of the OpenEventArgs class
        /// </summary>
        /// <param name="totalSize">Size of unpacked archive data</param>
        [CLSCompliantAttribute(false)]
        public OpenEventArgs(ulong totalSize)
        {
            _TotalSize = totalSize;
        }
    }
    #endregion
    /// <summary>
    /// Callback to handle the archive opening
    /// </summary>
    internal sealed class ArchiveOpenCallback : SevenZipBase, IArchiveOpenCallback, ICryptoGetTextPassword
    {
        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class
        /// </summary>
        public ArchiveOpenCallback() : base() { }
        /// <summary>
        /// Initializes a new instance of the ArchiveOpenCallback class
        /// </summary>
        /// <param name="password">Password for the archive</param>
        public ArchiveOpenCallback(string password) : base(password) { }

        #region ICryptoGetTextPassword Members
        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        #endregion

        #region IArchiveOpenCallback Members

        public void SetTotal(IntPtr files, IntPtr bytes) { }

        public void SetCompleted(IntPtr files, IntPtr bytes) { }

        #endregion
    }
    /// <summary>
    /// Archive extraction callback to handle the process of unpacking files
    /// </summary>
    internal sealed class ArchiveExtractCallback : SevenZipBase, IArchiveExtractCallback, ICryptoGetTextPassword, IDisposable
    {
        private OutStreamWrapper _FileStream;
        private IInArchive _Archive;
        private string _Directory;
        private int _FilesCount;
        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _DoneRate;
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
        /// Occurs when the archive is opened and 7-zip sends the size of unpacked data
        /// </summary>
        public event EventHandler<OpenEventArgs> Open;        

        private void OnOpen(OpenEventArgs e)
        {
            if (Open != null)
            {
                Open(this, e);
            }
        }
        private void OnFileExtractionStarted(IndexEventArgs e)
        {
            if (FileExtractionStarted != null)
            {
                FileExtractionStarted(this, e);
            }
        }
        private void OnFileExtractionFinished(EventArgs e)
        {
            if (FileExtractionFinished != null)
            {
                FileExtractionFinished(this, e);
            }
        }
        /// <summary>
        /// Ensures that the directory to the file name is valid and creates intermediate directories if necessary
        /// </summary>
        /// <param name="fileName">File name</param>
        private static void ValidateFileName(string fileName)
        {
            string[] splittedFileName = fileName.Split('\\');            
            if (splittedFileName.Length > 2)
            {
                string tfn = splittedFileName[0];
                for (int i = 1; i < splittedFileName.Length - 1; i++)
                {
                    tfn += '\\' + splittedFileName[i];
                    if (!Directory.Exists(tfn))
                    {
                        Directory.CreateDirectory(tfn);
                    }
                }
            }
        }
        private void Init(IInArchive archive, string directory, int filesCount)
        {
            _Archive = archive;
            _Directory = directory;
            _FilesCount = filesCount;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (!directory.EndsWith("\\", StringComparison.CurrentCulture))
            {
                _Directory += '\\';
            }
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">Archive files count</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount)
            : base()
        {
            Init(archive, directory, filesCount);
        } 
        /// <summary>
        /// Initializes a new instance of the ArchiveExtractCallback class
        /// </summary>
        /// <param name="archive">IInArchive interface for the archive</param>
        /// <param name="directory">Directory where files are to be unpacked to</param>
        /// <param name="filesCount">Archive files count</param>
        /// <param name="password">Password for the archive</param>
        public ArchiveExtractCallback(IInArchive archive, string directory, int filesCount, string password) 
            : base(password)
        {
            Init(archive, directory, filesCount);
        }        
        #region IArchiveExtractCallback Members
        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        public void SetTotal(ulong total)
        {
            OnOpen(new OpenEventArgs(total));
        }

        public void SetCompleted(ref ulong completeValue) { }

        /// <summary>
        /// Sets output stream for writing unpacked data
        /// </summary>
        /// <param name="index">Current file index</param>
        /// <param name="outStream">Output stream pointer</param>
        /// <param name="askExtractMode">Extraction mode</param>
        /// <returns>0 if OK</returns>
        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;
            if (askExtractMode == AskMode.Extract)
            {
                string fileName = _Directory;
                PropVariant Data = new PropVariant();
                _Archive.GetProperty(index, ItemPropId.Directory, ref Data);
                fileName += (string)Data.Object;
                _Archive.GetProperty(index, ItemPropId.IsFolder, ref Data);
                ValidateFileName(fileName);
                if (!NativeMethods.SafeCast<bool>(Data.Object, false))
                {
                    _Archive.GetProperty(index, ItemPropId.LastWriteTime, ref Data);
                    DateTime time = NativeMethods.SafeCast<DateTime>(Data.Object, DateTime.Now);
                    _FileStream = new OutStreamWrapper(File.Create(fileName), fileName, time);
                    outStream = _FileStream;
                }
                else
                {
                    if (!Directory.Exists(fileName))
                    {
                        Directory.CreateDirectory(fileName);
                    }
                }
                _DoneRate += 1.0f / _FilesCount;
                OnFileExtractionStarted(new IndexEventArgs(index, PercentDoneEventArgs.ProducePercentDone(_DoneRate)));
            }
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode) { }
        /// <summary>
        /// Called when the archive was extracted
        /// </summary>
        /// <param name="operationResult"></param>
        public void SetOperationResult(OperationResult operationResult)
        {
            if (operationResult != OperationResult.Ok && ReportErrors)
            {
                throw new ExtractionFailedException();
            }
            else
            {
                _FileStream.Dispose();
                OnFileExtractionFinished(EventArgs.Empty);
            }
        }

        #endregion

        #region ICryptoGetTextPassword Members
        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_FileStream != null)
            {
                _FileStream.Dispose();
            }
        }

        #endregion
    }
    /// <summary>
    /// Archive update callback to handle the process of packing files
    /// </summary>
    internal sealed class ArchiveUpdateCallback : SevenZipBase, IArchiveUpdateCallback, ICryptoGetTextPassword2, IDisposable
    {
        private InStreamWrapper _FileStream;
        /// <summary>
        /// Array of files to pack
        /// </summary>
        private FileInfo[] _Files;
        /// <summary>
        /// Common file names root length
        /// </summary>
        private int _RootLength;
        /// <summary>
        /// Rate of the done work from [0, 1]
        /// </summary>
        private float _DoneRate;
        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        public ArchiveUpdateCallback(FileInfo[] files, int rootLength)
            : base()
        {
            _Files = files;
            _RootLength = rootLength;
        }
        /// <summary>
        /// Initializes a new instance of the ArchiveUpdateCallback class
        /// </summary>
        /// <param name="files">Array of files to pack</param>
        /// <param name="rootLength">Common file names root length</param>
        /// <param name="password">Archive password</param>
        public ArchiveUpdateCallback(FileInfo[] files, int rootLength, string password)
            : base(password)
        {
            _Files = files;
            _RootLength = rootLength;
        }
        /// <summary>
        /// Occurs when the next file is going to be packed
        /// </summary>
        /// <remarks>Occurs when 7-zip engine requests for an input stream for the next file to pack it</remarks>
        public event EventHandler<FileInfoEventArgs> FileCompressionStarted;
        private void OnFileCompression(FileInfoEventArgs e)
        {
            if (FileCompressionStarted != null)
            {
                FileCompressionStarted(this, e);
            }
        }

        #region IArchiveUpdateCallback Members

        public void SetTotal(ulong total) { }

        public void SetCompleted(ref ulong completeValue) { }

        public int GetUpdateItemInfo(uint index, ref int newData, ref int newProperties, ref uint indexInArchive)
        {
            newData = 1;
            newProperties = 1;
            indexInArchive = UInt32.MaxValue;
            return 0;
        }

        public int GetProperty(uint index, ItemPropId propID, ref PropVariant value)
        {
            switch (propID)
            {
                case ItemPropId.IsAnti:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = 0;
                    break;
                case ItemPropId.Directory:
                    value.VarType = VarEnum.VT_BSTR;
                    value.Value = Marshal.StringToBSTR(_Files[index].FullName.Substring(_RootLength));
                    break;
                case ItemPropId.IsFolder:
                    value.VarType = VarEnum.VT_BOOL;
                    value.UInt64Value = (byte)(_Files[index].Attributes & FileAttributes.Directory);
                    break;
                case ItemPropId.Size:
                    value.VarType = VarEnum.VT_UI8;
                    value.UInt64Value = ((_Files[index].Attributes & FileAttributes.Directory) == 0)?
                        (ulong)_Files[index].Length : 0;
                    break;
                case ItemPropId.Attributes:
                    value.VarType = VarEnum.VT_UI4;
                    value.UInt32Value = (uint)_Files[index].Attributes;
                    break;
                case ItemPropId.CreationTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files[index].CreationTime.ToFileTime();
                    break;
                case ItemPropId.LastAccessTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files[index].LastAccessTime.ToFileTime();
                    break;
                case ItemPropId.LastWriteTime:
                    value.VarType = VarEnum.VT_FILETIME;
                    value.Int64Value = _Files[index].LastWriteTime.ToFileTime();
                    break;
                case ItemPropId.Extension:
                    value.VarType = VarEnum.VT_BSTR;
                    value.Value = Marshal.StringToBSTR(_Files[index].Extension.Substring(1));
                    break;
            }
            return 0;
        }
        /// <summary>
        /// Gets the stream for 7-zip library
        /// </summary>
        /// <param name="index">File index</param>
        /// <param name="inStream">Input file stream</param>
        /// <returns>Zero if Ok</returns>
        public int GetStream(uint index, out ISequentialInStream inStream)
        {
            if ((_Files[index].Attributes & FileAttributes.Directory) == 0)
            {
                _FileStream = new InStreamWrapper(File.OpenRead(_Files[index].FullName));
                inStream = _FileStream;
            }
            else
            {
                inStream = null;
            }
            _DoneRate += 1.0f / _Files.Length;
            OnFileCompression(new FileInfoEventArgs(_Files[index], PercentDoneEventArgs.ProducePercentDone(_DoneRate)));            
            return 0;
        }

        public long EnumProperties(IntPtr enumerator)
        {
            //Not implemented HRESULT
            return 0x80004001L;
        }

        public void SetOperationResult(OperationResult operationResult) { }

        #endregion

        #region ICryptoGetTextPassword2 Members

        public int CryptoGetTextPassword2(ref int passwordIsDefined, out string password)
        {
            passwordIsDefined = String.IsNullOrEmpty(Password) ? 0 : 1;
            password = Password;
            return 0;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_FileStream != null)
            {
                _FileStream.Dispose();
            }
        }

        #endregion
    }
}
