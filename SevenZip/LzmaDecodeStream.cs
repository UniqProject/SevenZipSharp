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
using SevenZip.Sdk.Compression.Lzma;

namespace SevenZip
{
    #if LZMA_STREAM
    /// <summary>
    /// The stream which decompresses data with LZMA on the fly.
    /// </summary>
    public class LzmaDecodeStream:  Stream
    {
        private Stream _Input;
        private MemoryStream _Buffer = new MemoryStream();
        private Decoder _Decoder = new Decoder();
        private bool _FirstChunkRead;
        private byte[] _CommonProperties;
        private bool _Error;

        /// <summary>
        /// Initializes a new instance of the LzmaDecodeStream class.
        /// </summary>
        /// <param name="encodedStream">A compressed stream.</param>
        public LzmaDecodeStream(Stream encodedStream)
        {
            if (!encodedStream.CanRead)
            {
                throw new ArgumentException("The specified stream can not read.", "encodedStream");
            }
            _Input = encodedStream;
        }

        private void ReadChunk()
        {
            long size;
            byte[] properties = null;
            try
            {
                properties = SevenZipExtractor.GetLzmaProperties(_Input, out size);
            }
            catch (LzmaException)
            {
                _Error = true;
                return;
            }
            if (!_FirstChunkRead)
            {
                _CommonProperties = properties;
            }
            if (_CommonProperties[0] != properties[0]||
                _CommonProperties[1] != properties[1]||
                _CommonProperties[2] != properties[2]||
                _CommonProperties[3] != properties[3]||
                _CommonProperties[4] != properties[4])
            {
                _Error = true;
                return;
            }
            if (_Buffer.Capacity < (int)size)
            {
                _Buffer.Capacity = (int)size;
            }
            _Buffer.SetLength(size);
            _Decoder.SetDecoderProperties(properties);
            _Buffer.Position = 0;
            _Decoder.Code(
                _Input, _Buffer, 0, size, null);
            _Buffer.Position = 0;
        }

        /// <summary>
        /// Gets the chunk size.
        /// </summary>
        public int ChunkSize
        {
            get
            {
                return (int)_Buffer.Length;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get 
            {
                return true;
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
                return false;
            }
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        public override void Flush() { }

        /// <summary>
        /// Gets the length in bytes of the output stream.
        /// </summary>
        public override long Length
        {
            get 
            {
                if (_Input.CanSeek)
                {
                    return _Input.Length;
                }
                else
                {
                    return _Buffer.Length;
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
                if (_Input.CanSeek)
                {
                    return _Input.Position;
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
        /// Reads a sequence of bytes from the current stream and decompresses data if necessary.
        /// </summary>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>        
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Error)
            {
                return 0;
            }

            if (!_FirstChunkRead)
            {
                ReadChunk();
                _FirstChunkRead = true;
            }
            int readCount = 0;
            while (count > _Buffer.Length - _Buffer.Position && !_Error)
            {
                byte[] buf = new byte[_Buffer.Length - _Buffer.Position];
                _Buffer.Read(buf, 0, buf.Length);
                buf.CopyTo(buffer, offset);
                offset += buf.Length;
                count -= buf.Length;
                readCount += buf.Length;
                ReadChunk();
            }
            if (!_Error)
            {
                _Buffer.Read(buffer, offset, count);
                readCount += count;
            }
            return readCount;
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
        /// Writes a sequence of bytes to the current stream.
        /// </summary>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
    #endif
}
