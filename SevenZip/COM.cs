/*  This file is part of SevenZipSharp.

    SevenZipSharp is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    SevenZipSharp is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General public License for more details.

    You should have received a copy of the GNU Lesser General public License
    along with SevenZipSharp.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace SevenZip.ComRoutines
{
    /// <summary>
    /// COM VARIANT structure with special interface routines
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    [CLSCompliantAttribute(false)]
    public struct PropVariant
    {
        [FieldOffset(0)]
        private ushort vt;
        /// <summary>
        /// IntPtr variant value
        /// </summary>
        [FieldOffset(8)]
        private IntPtr _Value;
        /// <summary>
        /// Byte variant value
        /// </summary>
        [FieldOffset(8)]
        private byte _ByteValue;
        /// <summary>
        /// Unsigned int variant value
        /// </summary>
        [FieldOffset(8)]
        private UInt32 _UInt32Value;
        /// <summary>
        /// Long variant value
        /// </summary>
        [FieldOffset(8)]
        private Int64 _Int64Value;
        /// <summary>
        /// Unsigned long variant value
        /// </summary>
        [FieldOffset(8)]
        private UInt64 _UInt64Value;
        /// <summary>
        /// FILETIME variant value
        /// </summary>
        [FieldOffset(8)]
        private System.Runtime.InteropServices.ComTypes.FILETIME _FileTime;
        /// <summary>
        /// Gets or sets variant type
        /// </summary>
        public VarEnum VarType
        {
            get
            {
                return (VarEnum)vt;
            }

            set
            {
                vt = (ushort)value;
            }
        }
        /// <summary>
        /// Gets or sets the pointer value of the COM variant
        /// </summary>
        public IntPtr Value
        {
            get
            {
                return _Value;
            }

            set
            {
                _Value = value;
            }
        }
        /// <summary>
        /// Gets or sets the byte value of the COM variant
        /// </summary>
        public byte ByteValue
        {
            get
            {
                return _ByteValue;
            }

            set
            {
                _ByteValue = value;
            }
        }
        /// <summary>
        /// Gets or sets the UInt32 value of the COM variant
        /// </summary>
        public UInt32 UInt32Value
        {
            get
            {
                return _UInt32Value;
            }

            set
            {
                _UInt32Value = value;
            }
        }
        /// <summary>
        /// Gets or sets the Int64 value of the COM variant
        /// </summary>
        public Int64 Int64Value
        {
            get
            {
                return _Int64Value;
            }

            set
            {
                _Int64Value = value;
            }
        }
        /// <summary>
        /// Gets or sets the UInt64 value of the COM variant
        /// </summary>
        public UInt64 UInt64Value
        {
            get
            {
                return _UInt64Value;
            }

            set
            {
                _UInt64Value = value;
            }
        }
        /// <summary>
        /// Gets or sets the FILETIME value of the COM variant
        /// </summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME FileTime
        {
            get
            {
                return _FileTime;
            }

            set
            {
                _FileTime = value;
            }
        }
        /// <summary>
        /// Gets or sets variant type (ushort)
        /// </summary>
        [CLSCompliantAttribute(false)]
        public ushort VarTypeNative
        {
            get
            {
                return vt;
            }

            set
            {
                vt = value;
            }
        }

        /// <summary>
        /// Clears variant
        /// </summary>
        public void Clear()
        {
            switch (VarType)
            {
                case VarEnum.VT_EMPTY:
                    break;
                case VarEnum.VT_NULL:
                case VarEnum.VT_I2:
                case VarEnum.VT_I4:
                case VarEnum.VT_R4:
                case VarEnum.VT_R8:
                case VarEnum.VT_CY:
                case VarEnum.VT_DATE:
                case VarEnum.VT_ERROR:
                case VarEnum.VT_BOOL:
                case VarEnum.VT_I1:
                case VarEnum.VT_UI1:
                case VarEnum.VT_UI2:
                case VarEnum.VT_UI4:
                case VarEnum.VT_I8:
                case VarEnum.VT_UI8:
                case VarEnum.VT_INT:
                case VarEnum.VT_UINT:
                case VarEnum.VT_HRESULT:
                case VarEnum.VT_FILETIME:
                    vt = 0;
                    break;
                default:
                    if (NativeMethods.PropVariantClear(ref this) != (int)OperationResult.Ok)
                    {
                        throw new ArgumentException("PropVariantClear has failed for some reason.");
                    }
                    break;
            }
        }

        /// <summary>
        /// Gets the variant object
        /// </summary>
        /// <returns></returns>
        public object Object
        {
            get
            {
                SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Demand();
                switch (VarType)
                {
                    case VarEnum.VT_EMPTY:
                        return null;
                    case VarEnum.VT_FILETIME:
                        return DateTime.FromFileTime(Int64Value);
                    default:
                        GCHandle PropHandle = GCHandle.Alloc(this, GCHandleType.Pinned);
                        try
                        {
                            return Marshal.GetObjectForNativeVariant(PropHandle.AddrOfPinnedObject());
                        }
                        finally
                        {
                            PropHandle.Free();
                        }
                }
            }
        }

        public override bool Equals(object obj)
        {
            return (obj is PropVariant) ? Equals((PropVariant)obj) : false;
        }

        public bool Equals(PropVariant afi)
        {
            if (afi.VarType != VarType)
            {
                return false;
            }
            if (VarType != VarEnum.VT_BSTR)
            {
                return afi.Int64Value == Int64Value;
            }
            return afi.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "[" + Value.ToString() + "] " + Int64Value.ToString(CultureInfo.CurrentCulture);
        }

        public static bool operator ==(PropVariant afi1, PropVariant afi2)
        {
            return afi1.Equals(afi2);
        }

        public static bool operator !=(PropVariant afi1, PropVariant afi2)
        {
            return !afi1.Equals(afi2);
        }
    }
    /// <summary>
    /// Stores file extraction modes
    /// </summary>
    public enum AskMode : int
    {
        Extract = 0,
        Test,
        Skip
    }
    /// <summary>
    /// Stores operation result values
    /// </summary>
    public enum OperationResult : int
    {
        /// <summary>
        /// Success
        /// </summary>
        Ok = 0,
        UnsupportedMethod,
        DataError,
        CrcError
    }
    /// <summary>
    /// Codes of item properities
    /// </summary>
    [CLSCompliantAttribute(false)]
    public enum ItemPropId : uint
    {
        NoProperty = 0,

        HandlerItemIndex = 2,
        Directory,
        Name,
        Extension,
        IsFolder,
        Size,
        PackedSize,
        Attributes,
        CreationTime,
        LastAccessTime,
        LastWriteTime,
        Solid,
        Commented,
        Encrypted,
        SplitBefore,
        SplitAfter,
        DictionarySize,
        Crc,
        Type,
        IsAnti,
        Method,
        HostOS,
        FileSystem,
        User,
        Group,
        Block,
        Comment,
        Position,
        Prefix,

        TotalSize = 0x1100,
        FreeSpace,
        ClusterSize,
        VolumeName,

        LocalName = 0x1200,
        Provider,

        UserDefined = 0x10000
    }
    /// <summary>
    /// Codes of archive properties or modes
    /// </summary>
    internal enum ArchivePropId : uint
    {
        Name = 0,
        ClassID,
        Extension,
        AddExtension,
        Update,
        KeepName,
        StartSignature,
        FinishSignature,
        Associate
    }

    /// <summary>
    /// 7-zip IProgress imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000000050000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IProgress
    {
        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);
    }
    /// <summary>
    /// 7-zip IArchiveOpenCallback imported interface for handling the opening of an archive
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600100000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IArchiveOpenCallback
    {
        // ref ulong replaced with IntPtr because handlers ofter pass null value
        // read actual value with Marshal.ReadInt64
        void SetTotal(
          IntPtr files,
          IntPtr bytes);
        void SetCompleted(
          IntPtr files,
          IntPtr bytes);
    }
    /// <summary>
    /// 7-zip ICryptoGetTextPassword imported interface for getting the archive password
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000500100000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICryptoGetTextPassword
    {
        /// <summary>
        /// Gets password for the archive
        /// </summary>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        [PreserveSig]
        int CryptoGetTextPassword(
          [MarshalAs(UnmanagedType.BStr)] out string password);
    }
    /// <summary>
    /// 7-zip ICryptoGetTextPassword2 imported interface for setting the archive password
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000500110000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICryptoGetTextPassword2
    {
        /// <summary>
        /// Sets password for the archive
        /// </summary>
        /// <param name="passwordIsDefined">Specifies whether archive has a password or not (0 if not)</param>
        /// <param name="password">Password for the archive</param>
        /// <returns>Zero if everything is OK</returns>
        [PreserveSig]
        int CryptoGetTextPassword2(
          ref int passwordIsDefined,
          [MarshalAs(UnmanagedType.BStr)] out string password);
    }
    /// <summary>
    /// 7-zip IArchiveExtractCallback imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600200000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IArchiveExtractCallback
    {
        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);
        /// <summary>
        /// Gets the stream for file extraction
        /// </summary>
        /// <param name="index">File index in the archive file table</param>
        /// <param name="outStream">Pointer to the stream</param>
        /// <param name="askExtractMode">Extraction mode</param>
        /// <returns>S_OK - OK, S_FALSE - skip this file</returns>
        [PreserveSig]
        int GetStream(
          uint index,
          [Out, MarshalAs(UnmanagedType.Interface)] out ISequentialOutStream outStream,
          AskMode askExtractMode);

        void PrepareOperation(AskMode askExtractMode);
        void SetOperationResult(OperationResult operationResult);
    }
    /// <summary>
    /// 7-zip IArchiveUpdateCallback imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600800000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IArchiveUpdateCallback
    {
        /// <summary>
        /// Gives the size of the unpacked archive files
        /// </summary>
        /// <param name="total">Size of the unpacked archive files (in bytes)</param>
        void SetTotal(ulong total);
        void SetCompleted([In] ref ulong completeValue);
        /// <summary>
        /// Gets archive update mode
        /// </summary>
        /// <param name="index">File index</param>
        /// <param name="newData">1 if new, 0 if not</param>
        /// <param name="newProperties">1 if new, 0 if not</param>
        /// <param name="indexInArchive">-1 if doesn't matter</param>
        /// <returns></returns>
        [PreserveSig]
        int GetUpdateItemInfo(
            uint index, ref int newData,
            ref int newProperties, ref uint indexInArchive);
        [PreserveSig]
        int GetProperty(uint index, ItemPropId propId, ref PropVariant value);
        [PreserveSig]
        int GetStream(
          uint index,
          [Out, MarshalAs(UnmanagedType.Interface)] out ISequentialInStream inStream);
        void SetOperationResult(OperationResult operationResult);
        long EnumProperties(IntPtr enumerator);

    }
    /// <summary>
    /// 7-zip IArchiveOpenVolumeCallback imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600300000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IArchiveOpenVolumeCallback
    {
        void GetProperty(
          ItemPropId propId,
              IntPtr value); // PropVariant
        [PreserveSig]
        int GetStream(
          [MarshalAs(UnmanagedType.LPWStr)] string name,
          [MarshalAs(UnmanagedType.Interface)] out IInStream inStream);
    }
    /// <summary>
    /// 7-zip IInArchiveGetStream imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600400000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IInArchiveGetStream
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        ISequentialInStream GetStream(uint index);
    }
    /// <summary>
    /// 7-zip ISequentialInStream imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300010000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface ISequentialInStream
    {
        /// <summary>
        /// Writes data to 7-zip packer
        /// </summary>
        /// <param name="data">Array of bytes available for writing</param>
        /// <param name="size">Array size</param>
        /// <returns>S_OK if success</returns>
        /// <remarks>If (size > 0) and there are bytes in stream, 
        /// this function must read at least 1 byte.
        /// This function is allowed to read less than "size" bytes.
        /// You must call Read function in loop, if you need exact amount of data.
        /// </remarks>
        uint Read(
          [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size);
    }
    /// <summary>
    /// 7-zip ISequentialOutStream imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300020000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface ISequentialOutStream
    {
        /// <summary>
        /// Writes data to unpacked file stream
        /// </summary>
        /// <param name="data">Array of bytes available for reading</param>
        /// <param name="size">Array size</param>
        /// <param name="processedSize">Processed data size</param>
        /// <returns>S_OK if success</returns>
        /// <remarks>If size != 0, return value is S_OK and (*processedSize == 0),
        ///  then there are no more bytes in stream.
        /// If (size > 0) and there are bytes in stream, 
        /// this function must read at least 1 byte.
        /// This function is allowed to rwrite less than "size" bytes.
        /// You must call Write function in loop, if you need exact amount of data.
        /// </remarks>
        [PreserveSig]
        int Write(
          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size, IntPtr processedSize);
    }
    /// <summary>
    /// 7-zip IInStream imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300030000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IInStream
    {

        uint Read(
          [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size);

        void Seek(
          long offset,
          uint seekOrigin,
              IntPtr newPosition);
    }
    /// <summary>
    /// 7-zip IOutStream imported interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000300040000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IOutStream
    {
        [PreserveSig]
        int Write(
          [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data,
          uint size,
          IntPtr processedSize);

        //[PreserveSig]
        void Seek(
          long offset,
          uint seekOrigin,
          IntPtr newPosition);

        [PreserveSig]
        int SetSize(long newSize);
    }

    /// <summary>
    /// 7-zip essential in archive interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600600000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IInArchive
    {
        /// <summary>
        /// Opens archive for reading
        /// </summary>
        /// <param name="stream">Archive file stream</param>
        /// <param name="maxCheckStartPosition">Maximum start position for checking</param>
        /// <param name="openArchiveCallback">Callback for opening archive</param>
        /// <returns></returns>
        [PreserveSig]
        int Open(
            IInStream stream,
            [In] ref ulong maxCheckStartPosition,
            [MarshalAs(UnmanagedType.Interface)] IArchiveOpenCallback openArchiveCallback);
        /// <summary>
        /// Closes archive
        /// </summary>
        void Close();
        /// <summary>
        /// Gets the number of files in the archive file table            
        /// </summary>
        /// <returns>The number of files in the archive</returns>
        uint GetNumberOfItems();

        /// <summary>
        /// Retrieves specific property data
        /// </summary>
        /// <param name="index">File index in the archive file table</param>
        /// <param name="propId">Property code</param>
        /// <param name="value">Property variant value</param>
        void GetProperty(
          uint index,
              ItemPropId propId,
              ref PropVariant value); // PropVariant

        /// <summary>
        /// Extract files from the opened archive
        /// </summary>
        /// <param name="indexes">indexes of files to be extracted (must be sorted)</param>
        /// <param name="numItems">0xFFFFFFFF means all files</param>
        /// <param name="testMode">testMode != 0 means "test files operation"</param>
        /// <param name="extractCallback">IArchiveExtractCallback for operations handling</param>
        /// <returns>0 if success</returns>
        [PreserveSig]
        int Extract(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] uint[] indexes,
            uint numItems,
            int testMode,
            [MarshalAs(UnmanagedType.Interface)] IArchiveExtractCallback extractCallback);

        /// <summary>
        /// Gets archive property data
        /// </summary>
        /// <param name="propId"></param>
        /// <param name="value"></param>
        void GetArchiveProperty(
          uint propId, // PROPID
              ref PropVariant value); // PropVariant
        /// <summary>
        /// Gets the number of properties
        /// </summary>
        /// <returns>The number of properties</returns>
        uint GetNumberOfProperties();
        void GetPropertyInfo(
          uint index,
          [MarshalAs(UnmanagedType.BStr)] out string name,
          out ItemPropId propId, // PROPID
          out ushort varType); //VARTYPE
        /// <summary>
        /// Gets the number of archive properties
        /// </summary>
        /// <returns>The number of archive properties</returns>
        uint GetNumberOfArchiveProperties();
        void GetArchivePropertyInfo(
          uint index,
          [MarshalAs(UnmanagedType.BStr)] string name,
          ref uint propId, // PROPID
          ref ushort varType); //VARTYPE
    }

    /// <summary>
    /// 7-zip essential out archive interface
    /// </summary>
    [ComImport]
    [Guid("23170F69-40C1-278A-0000-000600A00000")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [CLSCompliantAttribute(false)]
    public interface IOutArchive
    {
        [PreserveSig]
        int UpdateItems(
            [MarshalAs(UnmanagedType.Interface)] ISequentialOutStream outStream,
            uint numItems,
            [MarshalAs(UnmanagedType.Interface)] IArchiveUpdateCallback updateCallback);

        void GetFileTimeType(IntPtr type);
    }
}

namespace SevenZip
{
    using SevenZip.ComRoutines;
    /// <summary>
    /// Stream wrapper used in InStreamWrapper
    /// </summary>
    internal class StreamWrapper : IDisposable
    {
        /// <summary>
        /// Worker stream for reading, writing and seeking
        /// </summary>
        protected Stream BaseStream;
        /// <summary>
        /// File name associated with the stream (for date fix)
        /// </summary>
        protected string FileName;
        protected DateTime FileTime;

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        /// <param name="fileName">File name associated with the stream (for attributes fix)</param>
        /// <param name="time">File last write time (for attributes fix)</param>
        protected StreamWrapper(Stream baseStream, string fileName, DateTime time)
        {
            BaseStream = baseStream;
            FileName = fileName;
            FileTime = time;
        }

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        protected StreamWrapper(Stream baseStream)
        {
            BaseStream = baseStream;
        }
        /// <summary>
        /// Cleans up any resources used and fixes file attributes
        /// </summary>
        public void Dispose()
        {
            BaseStream.Dispose();
            GC.SuppressFinalize(this);
            if (!String.IsNullOrEmpty(FileName))
            {
                File.SetLastWriteTime(FileName, FileTime);
                File.SetLastAccessTime(FileName, FileTime);
                File.SetCreationTime(FileName, FileTime);
            }
        }

        public virtual void Seek(long offset, uint seekOrigin, IntPtr newPosition)
        {
            long Position = (uint)BaseStream.Seek(offset, (SeekOrigin)seekOrigin);
            if (newPosition != IntPtr.Zero)
                Marshal.WriteInt64(newPosition, Position);
        }
    }
    /// <summary>
    /// EventArgs for the InStreamWrapper and OutStreamWrapper classes
    /// </summary>
    internal sealed class IntEventArgs : EventArgs
    {
        private int _Value;

        /// <summary>
        /// Gets the value of the IntEventArgs class
        /// </summary>
        public int Value
        {
            get
            {
                return _Value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the IntEventArgs class
        /// </summary>
        /// <param name="value">Useful data carried by the IntEventArgs class</param>
        public IntEventArgs(int value)
        {
            _Value = value;
        }
    }

    /// <summary>
    /// IInStream wrapper used in stream read operations
    /// </summary>
    internal class InStreamWrapper : StreamWrapper, ISequentialInStream, IInStream
    {
        /// <summary>
        /// Initializes a new instance of the InStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        public InStreamWrapper(Stream baseStream) : base(baseStream) { }

        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were read from the source
        /// </summary>
        public event EventHandler<IntEventArgs> BytesRead;

        private void OnBytesRead(IntEventArgs e)
        {
            if (BytesRead != null)
            {
                BytesRead(this, e);
            }
        }
        /// <summary>
        /// Reads data from the stream
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="size">Array size</param>
        /// <returns>Read bytes count</returns>
        public uint Read(byte[] data, uint size)
        {
            int ReadCount = BaseStream.Read(data, 0, (int)size);
            OnBytesRead(new IntEventArgs(ReadCount));
            return (uint)ReadCount;
        }
    }
    /// <summary>
    /// IOutStream wrapper used in stream write operations
    /// </summary>
    internal class OutStreamWrapper : StreamWrapper, ISequentialOutStream, IOutStream
    {
        /// <summary>
        /// Initializes a new instance of the OutStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="fileName">File name (for attributes fix)</param>
        /// <param name="time">Time of the file creation (for attributes fix)</param>
        public OutStreamWrapper(Stream baseStream, string fileName, DateTime time) :
            base(baseStream, fileName, time) { }
        /// <summary>
        /// Initializes a new instance of the OutStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        public OutStreamWrapper(Stream baseStream) :
            base(baseStream) { }
        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were written
        /// </summary>
        public event EventHandler<IntEventArgs> BytesWritten;

        private void OnBytesWritten(IntEventArgs e)
        {
            if (BytesWritten != null)
            {
                BytesWritten(this, e);
            }
        }

        public int SetSize(long newSize)
        {
            BaseStream.SetLength(newSize);
            return 0;
        }
        /// <summary>
        /// Writes data to the stream
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="size">Array size</param>
        /// <param name="processedSize">Count of written bytes</param>
        /// <returns>Zero if Ok</returns>
        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            BaseStream.Write(data, 0, (int)size);
            OnBytesWritten(new IntEventArgs((int)size));
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int)size);
            }
            return 0;
        }
    }
}
