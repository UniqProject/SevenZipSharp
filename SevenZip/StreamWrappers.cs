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
using SevenZip.ComRoutines;

namespace SevenZip
{
    #if UNMANAGED
    /// <summary>
    /// Stream wrapper used in InStreamWrapper
    /// </summary>
    internal class StreamWrapper : IDisposable
    {
        /// <summary>
        /// Worker stream for reading, writing and seeking.
        /// </summary>
        private Stream _BaseStream;
        /// <summary>
        /// File name associated with the stream (for date fix)
        /// </summary>
        protected string FileName;
        protected DateTime FileTime;
        protected bool DisposeStream;

        protected const int WebBufferSize = 1000;

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        /// <param name="fileName">File name associated with the stream (for attributes fix)</param>
        /// <param name="time">File last write time (for attributes fix)</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        protected StreamWrapper(Stream baseStream, string fileName, DateTime time, bool disposeStream)
        {
            _BaseStream = baseStream;
            FileName = fileName;
            FileTime = time;
            DisposeStream = disposeStream;
        }

        /// <summary>
        /// Gets the worker stream for reading, writing and seeking.
        /// </summary>
        public Stream BaseStream
        {
            get
            {
                return _BaseStream;
            }
        }

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        protected StreamWrapper(Stream baseStream, bool disposeStream)
        {
            _BaseStream = baseStream;
            DisposeStream = disposeStream;
        }

        /// <summary>
        /// Cleans up any resources used and fixes file attributes.
        /// </summary>
        public void Dispose()
        {
            if (DisposeStream && _BaseStream != null)
            {
                try
                {
                    _BaseStream.Dispose();
                }
                catch (ObjectDisposedException) { }
                _BaseStream = null;
            }
            GC.SuppressFinalize(this);
            if (!String.IsNullOrEmpty(FileName) && File.Exists(FileName))
            {
                try
                {
                    File.SetLastWriteTime(FileName, FileTime);
                    File.SetLastAccessTime(FileName, FileTime);
                    File.SetCreationTime(FileName, FileTime);
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }        

        public event EventHandler<IntEventArgs> StreamSeek;

        public virtual void Seek(long offset, SeekOrigin seekOrigin, IntPtr newPosition)
        {
            if (BaseStream != null)
            {
                if (StreamSeek != null)
                {
                    if (BaseStream.Position > offset && seekOrigin == SeekOrigin.Begin)
                    {
                        StreamSeek(this, new IntEventArgs((int)(offset - BaseStream.Position)));
                    }
                }
                long Position = BaseStream.Seek(offset, seekOrigin);               
                if (newPosition != IntPtr.Zero)
                {
                    Marshal.WriteInt64(newPosition, Position);
                }
            }            
        }
    }

    /// <summary>
    /// IInStream wrapper used in stream read operations.
    /// </summary>
    internal sealed class InStreamWrapper : StreamWrapper, ISequentialInStream, IInStream
    {
        /// <summary>
        /// Initializes a new instance of the InStreamWrapper class.
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        public InStreamWrapper(Stream baseStream, bool disposeStream) : base(baseStream, disposeStream) { }

        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were read from the source.
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
        /// Reads data from the stream.
        /// </summary>
        /// <param name="data">A data array.</param>
        /// <param name="size">The array size.</param>
        /// <returns>The read bytes count.</returns>
        public int Read(byte[] data, uint size)
        {
            int readCount = 0;
            if (BaseStream != null)
            {
                readCount = BaseStream.Read(data, 0, (int)size);
                if (readCount > 0)
                {
                    OnBytesRead(new IntEventArgs(readCount));
                }
            }
            return readCount;
        }        
    }
    
    /// <summary>
    /// IOutStream wrapper used in stream write operations.
    /// </summary>
    internal sealed class OutStreamWrapper : StreamWrapper, ISequentialOutStream, IOutStream
    {
        /// <summary>
        /// Initializes a new instance of the OutStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="fileName">File name (for attributes fix)</param>
        /// <param name="time">Time of the file creation (for attributes fix)</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        public OutStreamWrapper(Stream baseStream, string fileName, DateTime time, bool disposeStream) :
            base(baseStream, fileName, time, disposeStream) { }
        /// <summary>
        /// Initializes a new instance of the OutStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        public OutStreamWrapper(Stream baseStream, bool disposeStream) :
            base(baseStream, disposeStream) { }
        
        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were written.
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
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int)size);
            }
            OnBytesWritten(new IntEventArgs((int)size));
            return 0;
        }        
    }

    /// <summary>
    /// Base multi volume stream wrapper class.
    /// </summary>
    internal class MultiStreamWrapper : IDisposable
    {
        protected long _Position;
        protected int _CurrentStream;
        protected long _Length;
        protected readonly List<Stream> _Streams = new List<Stream>();
        protected readonly Dictionary<int, KeyValuePair<long, long>> _StreamOffsets =
            new Dictionary<int, KeyValuePair<long, long>>();

        protected static string VolumeNumber(int num)
        {
            if (num < 10)
            {
                return ".00" + num.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (num > 9 && num < 100)
            {
                return ".0" + num.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (num > 99 && num < 1000)
            {
                return "." + num.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return String.Empty;
        }

        private int StreamNumberByOffset(long offset)
        {
            foreach (int number in _StreamOffsets.Keys)
            {
                if (_StreamOffsets[number].Key <= offset &&
                    _StreamOffsets[number].Value >= offset)
                {
                    return number;
                }
            }
            return -1;
        }

        /// <summary>
        /// Gets the total length of input data.
        /// </summary>
        public long Length
        {
            get
            {
                return _Length;
            }
        }

        public void Seek(long offset, SeekOrigin seekOrigin, IntPtr newPosition)
        {
            long absolutePosition = (seekOrigin == SeekOrigin.Current) ?
                _Position + offset : offset;
            _CurrentStream = StreamNumberByOffset(absolutePosition);
            long delta = _Streams[_CurrentStream].Seek(
                absolutePosition - _StreamOffsets[_CurrentStream].Key, SeekOrigin.Begin);
            _Position = _StreamOffsets[_CurrentStream].Key + delta;
            if (newPosition != IntPtr.Zero)
            {
                Marshal.WriteInt64(newPosition, _Position);
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Cleans up any resources used and fixes file attributes.
        /// </summary>
        public virtual void Dispose()
        {
            foreach (Stream stream in _Streams)
            {
                try
                {
                    stream.Dispose();
                }
                catch (ObjectDisposedException) { }
            }
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// IInStream wrapper used in stream multi volume read operations.
    /// </summary>
    internal sealed class InMultiStreamWrapper : MultiStreamWrapper, ISequentialInStream, IInStream
    {
        /// <summary>
        /// Initializes a new instance of the InMultiStreamWrapper class.
        /// </summary>
        /// <param name="fileName">The archive file name.</param>
        public InMultiStreamWrapper(string fileName)
        {
            string baseName = fileName.Substring(0, fileName.Length - 4);
            int i = 0;
            while (File.Exists(fileName))
            {
                _Streams.Add(new FileStream(fileName, FileMode.Open));
                long length = _Streams[i].Length;
                _StreamOffsets.Add(i++, new KeyValuePair<long, long>(_Length, _Length + length));
                _Length += length;
                fileName = baseName + VolumeNumber(i + 1);
            }
        }

        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were read from the source.
        /// </summary>
        public event EventHandler<IntEventArgs> BytesRead;

        private void OnBytesRead(IntEventArgs e)
        {
            if (BytesRead != null)
            {
                BytesRead(this, e);
            }
        }             

        #region IInStream Members

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        /// <param name="data">A data array.</param>
        /// <param name="size">The array size.</param>
        /// <returns>The read bytes count.</returns>
        public int Read(byte[] data, uint size)
        {
            int readSize = (int)size;
            int readCount = _Streams[_CurrentStream].Read(data, 0, readSize);
            readSize -= readCount;
            _Position += readCount;
            while (readCount < (int)size)
            {
                if (_CurrentStream == _Streams.Count - 1)
                {
                    return readCount;
                }
                _CurrentStream++;
                _Streams[_CurrentStream].Seek(0, SeekOrigin.Begin);
                int count = _Streams[_CurrentStream].Read(data, readCount, readSize);
                readCount += count;
                readSize -= count;
                _Position += count;
            }
            if (readCount > 0)
            {
                OnBytesRead(new IntEventArgs(readCount));
            }
            return readCount;
        }        

        #endregion        
    }

    #if COMPRESS
    /// <summary>
    /// IOutStream wrapper used in multi volume stream write operations.
    /// </summary>
    internal sealed class OutMultiStreamWrapper : MultiStreamWrapper, ISequentialOutStream, IOutStream, IDisposable
    {
        private readonly int _VolumeSize;
        private readonly string _ArchiveName;

        /// <summary>
        /// Initializes a new instance of the OutMultiStreamWrapper class.
        /// </summary>
        /// <param name="archiveName">The archive name.</param>
        /// <param name="volumeSize">The volume size.</param>
        public OutMultiStreamWrapper(string archiveName, int volumeSize)
        {
            _ArchiveName = archiveName;
            _VolumeSize = volumeSize;
            _CurrentStream = -1;
            NewVolumeStream();
        }

        /// <summary>
        /// Occurs when IntEventArgs.Value bytes were written.
        /// </summary>
        public event EventHandler<IntEventArgs> BytesWritten;

        private void OnBytesWritten(IntEventArgs e)
        {
            if (BytesWritten != null)
            {
                BytesWritten(this, e);
            }
        }

        private void NewVolumeStream()
        {
            _CurrentStream++;
            _Streams.Add(File.Create(_ArchiveName + VolumeNumber(_CurrentStream + 1)));
            _Streams[_CurrentStream].SetLength(_VolumeSize);
            _StreamOffsets.Add(_CurrentStream, new KeyValuePair<long,long>(0, _VolumeSize - 1));
        }

        #region IOutStream Members

        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            int offset = 0, count, originalSize = (int)size;
            _Position += size;
            while (size > _VolumeSize - _Streams[_CurrentStream].Position)
            {
                count = (int)(_VolumeSize - _Streams[_CurrentStream].Position);
                _Streams[_CurrentStream].Write(data, offset, count);
                size -= (uint)count;
                offset += count;
                NewVolumeStream();
            }
            _Streams[_CurrentStream].Write(data, offset, (int)size);            
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, originalSize);
            }
            OnBytesWritten(new IntEventArgs((int)size));
            return 0;
        }

        public int SetSize(long newSize)
        {
            return 0;
        }

        #endregion  
      
        public override void Dispose()
        {
            _Streams[_Streams.Count - 1].SetLength(_Streams[_Streams.Count - 1].Position);
            base.Dispose();
        }
    }
    #endif

    internal sealed class FakeOutStreamWrapper : ISequentialOutStream, IDisposable
    {
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

        /// <summary>
        /// Does nothing except calling the BytesWritten event
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="size">Array size</param>
        /// <param name="processedSize">Count of written bytes</param>
        /// <returns>Zero if Ok</returns>
        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            OnBytesWritten(new IntEventArgs((int)size));
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int)size);
            }
            return 0;
        }

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        #endregion
    }
    #endif
}
