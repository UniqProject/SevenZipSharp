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
using SevenZip.ComRoutines;
using System.Net;

namespace SevenZip
{
    
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
        protected bool DisposeStream;
        protected Uri RequestUri;
        protected long StreamPosition;

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
            BaseStream = baseStream;
            FileName = fileName;
            FileTime = time;
            DisposeStream = disposeStream;
        }

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="baseStream">Worker stream for reading, writing and seeking</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        protected StreamWrapper(Stream baseStream, bool disposeStream)
        {
            BaseStream = baseStream;
            DisposeStream = disposeStream;
        }

        /// <summary>
        /// Initializes a new instance of the StreamWrapper class
        /// </summary>
        /// <param name="requestUri">A System.Uri containing the URI of the requested resource.</param>
        protected StreamWrapper(Uri requestUri)
        {
            BaseStream = WebRequest.Create(requestUri).GetResponse().GetResponseStream();
            RequestUri = requestUri;
            DisposeStream = true;
        }
        /// <summary>
        /// Cleans up any resources used and fixes file attributes
        /// </summary>
        public void Dispose()
        {
            if (DisposeStream)
            {
                BaseStream.Dispose();
            }
            GC.SuppressFinalize(this);
            if (File.Exists(FileName))
            {
                File.SetLastWriteTime(FileName, FileTime);
                File.SetLastAccessTime(FileName, FileTime);
                File.SetCreationTime(FileName, FileTime);
            }
        }

        protected void WebStreamRead(byte[] buf, int offset)
        {
            for (int i = 0; i < offset / WebBufferSize; i++)
            {
                int BytesRead = 0;
                while (BytesRead < WebBufferSize)
                {
                    BytesRead += BaseStream.Read(
                        buf, i * WebBufferSize + BytesRead, WebBufferSize - BytesRead);
                }
            }
            int bytesRead = 0, pos = (offset / WebBufferSize) * WebBufferSize, size = offset % WebBufferSize;
            while (bytesRead < size)
            {
                bytesRead += BaseStream.Read(buf, pos + bytesRead, size - bytesRead);
            }
        }

        public virtual void Seek(long offset, SeekOrigin seekOrigin, IntPtr newPosition)
        {            
            long Position = 0;
            if (RequestUri == null)
            {
                Position = (uint)BaseStream.Seek(offset, seekOrigin);
            }
            else
            {
                if (StreamPosition == 0 && offset == 0 && seekOrigin != SeekOrigin.End)
                {
                    Position = 0;
                }
                else
                {
                    if (seekOrigin == SeekOrigin.Begin)
                    {
                        BaseStream.Dispose();
                        BaseStream = WebRequest.Create(RequestUri).GetResponse().GetResponseStream();
                        StreamPosition = 0;
                    }
                    if (offset > 0 && seekOrigin != SeekOrigin.End)
                    {
                        byte[] buf = new byte[offset];
                        WebStreamRead(buf, (int)offset);
                        StreamPosition += offset;
                        Position = StreamPosition;
                    }
                }
            }
            if (newPosition != IntPtr.Zero)
                Marshal.WriteInt64(newPosition, Position);
        }
    }

    /// <summary>
    /// IInStream wrapper used in stream read operations
    /// </summary>
    internal sealed class InStreamWrapper : StreamWrapper, ISequentialInStream, IInStream
    {
        /// <summary>
        /// Initializes a new instance of the InStreamWrapper class
        /// </summary>
        /// <param name="baseStream">Stream for writing data</param>
        /// <param name="disposeStream">Indicates whether to dispose the baseStream</param>
        public InStreamWrapper(Stream baseStream, bool disposeStream) : base(baseStream, disposeStream) { }

        /// <summary>
        /// Initializes a new instance of the InStreamWrapper class
        /// </summary>
        /// <param name="requestUri">A System.Uri containing the URI of the requested resource.</param>
        public InStreamWrapper(Uri requestUri) : base(requestUri) { } 

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
        public int Read(byte[] data, uint size)
        {
            int ReadCount;
            if (RequestUri == null)
            {
                ReadCount = BaseStream.Read(data, 0, (int)size);
                OnBytesRead(new IntEventArgs(ReadCount));
            }
            else
            {
                WebStreamRead(data, (int)size);
                ReadCount = (int)size;
                StreamPosition += ReadCount;
            }
            return ReadCount;
        }
    }
    /// <summary>
    /// IOutStream wrapper used in stream write operations
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
}
