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
using SevenZip.Sdk.Compression.Lzma;

namespace SevenZip
{
    #if LZMA_STREAM
    #if COMPRESS
    /// <summary>
    /// The stream which compresses data with LZMA on the fly.
    /// </summary>
    public class LzmaEncodeStream: Stream
    {
        private readonly MemoryStream _Buffer = new MemoryStream();
        private Stream _Output;
        private readonly int _BufferCapacity = 1 << 18; //256 kb
        private const int _MaxBufferCapacity = 1 << 30; //1 Gb
        private Encoder _LzmaEncoder;
        private bool _Disposed;

        private void Init()
        {                    
            _Buffer.Capacity = _BufferCapacity;
            SevenZipCompressor.LzmaDictionarySize = _BufferCapacity;
            _LzmaEncoder = new Encoder();
            SevenZipCompressor.WriteLzmaProperties(_LzmaEncoder);
        }

        /// <summary>
        /// Initializes a new instance of the LzmaEncodeStream class.
        /// </summary>
        public LzmaEncodeStream()
        {
            _Output = new MemoryStream();            
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the LzmaEncodeStream class.
        /// </summary>
        /// <param name="bufferCapacity">The buffer size. The bigger size, the better compression.</param>
        public LzmaEncodeStream(int bufferCapacity)
        {
            _Output = new MemoryStream();
            if (bufferCapacity > _MaxBufferCapacity)
            {
                throw new ArgumentException("Too large capacity.", "bufferCapacity");
            } 
            _BufferCapacity = bufferCapacity;
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the LzmaEncodeStream class.
        /// </summary>
        /// <param name="outputStream">An output stream which supports writing.</param>
        public LzmaEncodeStream(Stream outputStream)
        {
            if (!outputStream.CanWrite)
            {
                throw new ArgumentException("The specified stream can not write.", "outputStream");
            }
            _Output = outputStream;
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the LzmaEncodeStream class.
        /// </summary>
        /// <param name="outputStream">An output stream which supports writing.</param>
        /// <param name="bufferCapacity">A buffer size. The bigger size, the better compression.</param>
        public LzmaEncodeStream(Stream outputStream, int bufferCapacity)
        {
            if (!outputStream.CanWrite)
            {
                throw new ArgumentException("The specified stream can not write.", "outputStream");
            }
            _Output = outputStream;
            if (bufferCapacity > 1 << 30)
            {
                throw new ArgumentException("Too large capacity.", "bufferCapacity");
            } 
            _BufferCapacity = bufferCapacity;
            Init();
        }

        private void WriteChunk()
        {
            _LzmaEncoder.WriteCoderProperties(_Output);
            long streamSize = _Buffer.Position;
            if (_Buffer.Length != _Buffer.Position)
            {
                _Buffer.SetLength(_Buffer.Position);
            }
            _Buffer.Position = 0;
            for (int i = 0; i < 8; i++)
            {
                _Output.WriteByte((byte)(streamSize >> (8 * i)));
            }
            _LzmaEncoder.Code(_Buffer, _Output, -1, -1, null);
            _Buffer.Position = 0;
        }

        /// <summary>
        /// Converts the LzmaEncodeStream to the LzmaDecodeStream to read data.
        /// </summary>
        /// <returns></returns>
        public LzmaDecodeStream ToDecodeStream()
        {
            Flush();
            return new LzmaDecodeStream(_Output);
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get 
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get 
            {
                return _Buffer.CanWrite;
            }
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be compressed and written.
        /// </summary>
        public override void Flush()
        {            
            WriteChunk();
        }

        /// <summary>
        /// Releases all unmanaged resources used by LzmaEncodeStream.
        /// </summary>
        public new void Dispose()
        {            
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all unmanaged resources used by LzmaEncodeStream.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    Flush();
                    _Buffer.Close();
                    _Output = null;
                }
                _Disposed = true;
            }
        }

        /// <summary>
        /// Gets the length in bytes of the output stream.
        /// </summary>
        public override long Length
        {
            get 
            {
                if (_Output.CanSeek)
                {
                    return _Output.Length;
                }
                else
                {
                    return _Buffer.Position;
                }
            }
        }

        /// <summary>
        /// Gets or sets the position within the output stream.
        /// </summary>
        public override long Position
        {
            get
            {
                if (_Output.CanSeek)
                {
                    return _Output.Position;
                }
                else
                {
                    return _Buffer.Position;
                }
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type System.IO.SeekOrigin indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and compresses it if necessary.
        /// </summary>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            int dataLength = Math.Min(buffer.Length - offset, count);
            int length = count;
            while (_Buffer.Position + dataLength >= _BufferCapacity)
            {
                length = _BufferCapacity - (int)_Buffer.Position;
                _Buffer.Write(buffer, offset, length);
                offset = length + offset;
                dataLength -= length;
                WriteChunk();
            }
            _Buffer.Write(buffer, offset, dataLength);
        }
    }
    #endif
    #endif
}
