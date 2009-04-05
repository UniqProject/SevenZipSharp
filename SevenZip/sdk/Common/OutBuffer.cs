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

namespace SevenZip.Sdk.Buffer
{
    using System;

	internal class OutBuffer
	{
		byte[] m_Buffer;
		uint m_Pos;
		uint m_BufferSize;
		System.IO.Stream m_Stream;
		ulong m_ProcessedSize;
        /// <summary>
        /// Initializes a new instance of the OutBuffer class
        /// </summary>
        /// <param name="bufferSize"></param>
		public OutBuffer(uint bufferSize)
		{
			m_Buffer = new byte[bufferSize];
			m_BufferSize = bufferSize;
		}

		public void SetStream(System.IO.Stream stream) { m_Stream = stream; }
		public void FlushStream() { m_Stream.Flush(); }
		public void CloseStream() { m_Stream.Close(); }
		public void ReleaseStream() { m_Stream = null; }

		public void Init()
		{
			m_ProcessedSize = 0;
			m_Pos = 0;
		}

		public void WriteByte(byte b)
		{
			m_Buffer[m_Pos++] = b;
			if (m_Pos >= m_BufferSize)
				FlushData();
		}

		public void FlushData()
		{
			if (m_Pos == 0)
				return;
			m_Stream.Write(m_Buffer, 0, (int)m_Pos);
			m_Pos = 0;
		}

		public ulong GetProcessedSize() { return m_ProcessedSize + m_Pos; }
	}
}
