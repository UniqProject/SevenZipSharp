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

namespace SevenZip
{
#if UNMANAGED
    /// <summary>
    /// Stream wrapper used in InStreamWrapper
    /// </summary>
    internal class StreamWrapper : IDisposable
    {
        private readonly bool _DisposeStream;

        /// <summary>
        /// File name associated with the stream (for date fix)
        /// </summary>
        private readonly string _FileName;

        private readonly DateTime _FileTime;

        /// <summary>
        /// Worker stream for reading, writing and seeking.
        /// </summary>
        private Stream _BaseStream;

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
            _FileName = fileName;
            _FileTime = time;
            _DisposeStream = disposeStream;
        }

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        protected StreamWrapper(Stream baseStream, bool disposeStream)
        {
            _BaseStream = baseStream;
            _DisposeStream = disposeStream;
        }

        /// <summary>
        /// Gets the worker stream for reading, writing and seeking.
        /// </summary>
        protected Stream BaseStream
        {
            get
            {
                return _BaseStream;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Cleans up any resources used and fixes file attributes.
        /// </summary>
        public void Dispose()
        {
            if (_DisposeStream && _BaseStream != null)
            {
                try
                {
                    _BaseStream.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
                _BaseStream = null;
            }
            GC.SuppressFinalize(this);
            if (!String.IsNullOrEmpty(_FileName) && File.Exists(_FileName))
            {
                try
                {
                    File.SetLastWriteTime(_FileName, _FileTime);
                    File.SetLastAccessTime(_FileName, _FileTime);
                    File.SetCreationTime(_FileName, _FileTime);
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }

        #endregion

        public event EventHandler<IntEventArgs> StreamSeek;

        public virtual void Seek(long offset, SeekOrigin seekOrigin, IntPtr newPosition)
        {
            if (BaseStream != null)
            {
                if (StreamSeek != null)
                {
                    if (BaseStream.Position > offset && seekOrigin == SeekOrigin.Begin)
                    {
                        StreamSeek(this, new IntEventArgs((int) (offset - BaseStream.Position)));
                    }
                }
                long position = BaseStream.Seek(offset, seekOrigin);
                if (newPosition != IntPtr.Zero)
                {
                    Marshal.WriteInt64(newPosition, position);
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
        public InStreamWrapper(Stream baseStream, bool disposeStream) : base(baseStream, disposeStream) {}

        #region ISequentialInStream Members

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
                readCount = BaseStream.Read(data, 0, (int) size);
                if (readCount > 0)
                {
                    OnBytesRead(new IntEventArgs(readCount));
                }
            }
            return readCount;
        }

        #endregion

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
            base(baseStream, fileName, time, disposeStream)
        {
        }

        /// <summary>
        /// Initializes a new instance of the OutStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        public OutStreamWrapper(Stream baseStream, bool disposeStream) :
            base(baseStream, disposeStream)
        {
        }

        #region IOutStream Members

        public int SetSize(long newSize)
        {
            BaseStream.SetLength(newSize);
            return 0;
        }

        #endregion

        #region ISequentialOutStream Members

        /// <summary>
        /// Writes data to the stream
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="size">Array size</param>
        /// <param name="processedSize">Count of written bytes</param>
        /// <returns>Zero if Ok</returns>
        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            BaseStream.Write(data, 0, (int) size);
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int) size);
            }
            OnBytesWritten(new IntEventArgs((int) size));
            return 0;
        }

        #endregion

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
    }

    /// <summary>
    /// Base multi volume stream wrapper class.
    /// </summary>
    internal class MultiStreamWrapper : IDisposable
    {
        private readonly bool _DisposeEnabled;

        protected readonly Dictionary<int, KeyValuePair<long, long>> StreamOffsets =
            new Dictionary<int, KeyValuePair<long, long>>();

        protected readonly List<Stream> Streams = new List<Stream>();
        protected int CurrentStream;
        protected long Position;
        protected long StreamLength;

        /// <summary>
        /// Initializes a new instance of the MultiStreamWrapper class.
        /// </summary>
        /// <param name="dispose">Perform Dispose() if requested to.</param>
        protected MultiStreamWrapper(bool dispose)
        {
            _DisposeEnabled = dispose;
        }

        /// <summary>
        /// Gets the total length of input data.
        /// </summary>
        public long Length
        {
            get
            {
                return StreamLength;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Cleans up any resources used and fixes file attributes.
        /// </summary>
        public virtual void Dispose()
        {
            if (_DisposeEnabled)
            {
                foreach (Stream stream in Streams)
                {
                    try
                    {
                        stream.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        protected static string VolumeNumber(int num)
        {
            if (num < 10)
            {
                return ".00" + num.ToString(CultureInfo.InvariantCulture);
            }
            if (num > 9 && num < 100)
            {
                return ".0" + num.ToString(CultureInfo.InvariantCulture);
            }
            if (num > 99 && num < 1000)
            {
                return "." + num.ToString(CultureInfo.InvariantCulture);
            }
            return String.Empty;
        }

        private int StreamNumberByOffset(long offset)
        {
            foreach (int number in StreamOffsets.Keys)
            {
                if (StreamOffsets[number].Key <= offset &&
                    StreamOffsets[number].Value >= offset)
                {
                    return number;
                }
            }
            return -1;
        }

        public void Seek(long offset, SeekOrigin seekOrigin, IntPtr newPosition)
        {
            long absolutePosition = (seekOrigin == SeekOrigin.Current)
                                        ?
                                            Position + offset
                                        : offset;
            CurrentStream = StreamNumberByOffset(absolutePosition);
            long delta = Streams[CurrentStream].Seek(
                absolutePosition - StreamOffsets[CurrentStream].Key, SeekOrigin.Begin);
            Position = StreamOffsets[CurrentStream].Key + delta;
            if (newPosition != IntPtr.Zero)
            {
                Marshal.WriteInt64(newPosition, Position);
            }
        }
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
        /// <param name="dispose">Perform Dispose() if requested to.</param>
        public InMultiStreamWrapper(string fileName, bool dispose) :
            base(dispose)
        {
            string baseName = fileName.Substring(0, fileName.Length - 4);
            int i = 0;
            while (File.Exists(fileName))
            {
                Streams.Add(new FileStream(fileName, FileMode.Open));
                long length = Streams[i].Length;
                StreamOffsets.Add(i++, new KeyValuePair<long, long>(StreamLength, StreamLength + length));
                StreamLength += length;
                fileName = baseName + VolumeNumber(i + 1);
            }
        }

        #region ISequentialInStream Members

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        /// <param name="data">A data array.</param>
        /// <param name="size">The array size.</param>
        /// <returns>The read bytes count.</returns>
        public int Read(byte[] data, uint size)
        {
            var readSize = (int) size;
            int readCount = Streams[CurrentStream].Read(data, 0, readSize);
            readSize -= readCount;
            Position += readCount;
            while (readCount < (int) size)
            {
                if (CurrentStream == Streams.Count - 1)
                {
                    return readCount;
                }
                CurrentStream++;
                Streams[CurrentStream].Seek(0, SeekOrigin.Begin);
                int count = Streams[CurrentStream].Read(data, readCount, readSize);
                readCount += count;
                readSize -= count;
                Position += count;
            }
            return readCount;
        }

        #endregion
    }

#if COMPRESS
    /// <summary>
    /// IOutStream wrapper used in multi volume stream write operations.
    /// </summary>
    internal sealed class OutMultiStreamWrapper : MultiStreamWrapper, ISequentialOutStream, IOutStream
    {
        private readonly string _ArchiveName;
        private readonly int _VolumeSize;

        /// <summary>
        /// Initializes a new instance of the OutMultiStreamWrapper class.
        /// </summary>
        /// <param name="archiveName">The archive name.</param>
        /// <param name="volumeSize">The volume size.</param>
        public OutMultiStreamWrapper(string archiveName, int volumeSize) :
            base(true)
        {
            _ArchiveName = archiveName;
            _VolumeSize = volumeSize;
            CurrentStream = -1;
            NewVolumeStream();
        }

        #region IDisposable Members

        public override void Dispose()
        {
            Streams[Streams.Count - 1].SetLength(Streams[Streams.Count - 1].Position);
            base.Dispose();
        }

        #endregion

        #region IOutStream Members

        public int SetSize(long newSize)
        {
            return 0;
        }

        #endregion

        #region ISequentialOutStream Members

        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            var offset = 0;
            var originalSize = (int) size;
            Position += size;
            while (size > _VolumeSize - Streams[CurrentStream].Position)
            {
                var count = (int) (_VolumeSize - Streams[CurrentStream].Position);
                Streams[CurrentStream].Write(data, offset, count);
                size -= (uint) count;
                offset += count;
                NewVolumeStream();
            }
            Streams[CurrentStream].Write(data, offset, (int) size);
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, originalSize);
            }
            return 0;
        }

        #endregion

        private void NewVolumeStream()
        {
            CurrentStream++;
            Streams.Add(File.Create(_ArchiveName + VolumeNumber(CurrentStream + 1)));
            Streams[CurrentStream].SetLength(_VolumeSize);
            StreamOffsets.Add(CurrentStream, new KeyValuePair<long, long>(0, _VolumeSize - 1));
        }
    }
#endif

    internal sealed class FakeOutStreamWrapper : ISequentialOutStream, IDisposable
    {
        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ISequentialOutStream Members

        /// <summary>
        /// Does nothing except calling the BytesWritten event
        /// </summary>
        /// <param name="data">Data array</param>
        /// <param name="size">Array size</param>
        /// <param name="processedSize">Count of written bytes</param>
        /// <returns>Zero if Ok</returns>
        public int Write(byte[] data, uint size, IntPtr processedSize)
        {
            OnBytesWritten(new IntEventArgs((int) size));
            if (processedSize != IntPtr.Zero)
            {
                Marshal.WriteInt32(processedSize, (int) size);
            }
            return 0;
        }

        #endregion

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
    }
#endif
}