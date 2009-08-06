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

namespace SevenZip
{
#if UNMANAGED
    /// <summary>
    /// The signature checker class. Original code by Siddharth Uppal, adapted by Markhor.
    /// </summary>
    /// <remarks>Based on the code at http://blog.somecreativity.com/2008/04/08/how-to-check-if-a-file-is-compressed-in-c/#</remarks>
    internal static class FileChecker
    {
        private const int SignatureSize = 16;

        private static void SpecialDetect(Stream stream, int offset, InArchiveFormat expectedFormat)
        {
            if (stream.Length > offset + SignatureSize)
            {
                var signature = new byte[SignatureSize];
                int bytesRequired = SignatureSize;
                int index = 0;
                stream.Seek(offset, SeekOrigin.Begin);
                while (bytesRequired > 0)
                {
                    int bytesRead = stream.Read(signature, index, bytesRequired);
                    bytesRequired -= bytesRead;
                    index += bytesRead;
                }
                string actualSignature = BitConverter.ToString(signature);
                foreach (string expectedSignature in Formats.InSignatureFormats.Keys)
                {
                    if (Formats.InSignatureFormats[expectedSignature] != expectedFormat)
                    {
                        continue;
                    }
                    if (actualSignature.StartsWith(expectedSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            throw new ArgumentException("No signature was found.");
        }

        /// <summary>
        /// Gets the InArchiveFormat for a specific extension.
        /// </summary>
        /// <param name="stream">The stream to identify.</param>
        /// <returns>Corresponding InArchiveFormat.</returns>
        public static InArchiveFormat CheckSignature(Stream stream)
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("The stream must be readable.");
            }
            if (stream.Length < SignatureSize)
            {
                throw new ArgumentException("The stream is invalid.");
            }

            #region Get file signature

            var signature = new byte[SignatureSize];
            int bytesRequired = SignatureSize;
            int index = 0;
            stream.Seek(0, SeekOrigin.Begin);
            while (bytesRequired > 0)
            {
                int bytesRead = stream.Read(signature, index, bytesRequired);
                bytesRequired -= bytesRead;
                index += bytesRead;
            }
            string actualSignature = BitConverter.ToString(signature);

            #endregion

            foreach (string expectedSignature in Formats.InSignatureFormats.Keys)
            {
                if (actualSignature.StartsWith(expectedSignature, StringComparison.OrdinalIgnoreCase) ||
                    actualSignature.Substring(2).StartsWith(expectedSignature, StringComparison.OrdinalIgnoreCase) &&
                    Formats.InSignatureFormats[expectedSignature] == InArchiveFormat.Lzh)
                {
                    return Formats.InSignatureFormats[expectedSignature];
                }
            }

            try
            {
                SpecialDetect(stream, 257, InArchiveFormat.Tar);
            }
            catch (ArgumentException) {}
            try
            {
                SpecialDetect(stream, 8001, InArchiveFormat.Iso);
            }
            catch (ArgumentException) {}
            try
            {
                SpecialDetect(stream, 8801, InArchiveFormat.Iso);
            }
            catch (ArgumentException) {}
            try
            {
                SpecialDetect(stream, 9001, InArchiveFormat.Iso);
            }
            catch (ArgumentException) {}

            throw new ArgumentException("The stream is invalid.");
        }

        /// <summary>
        /// Gets the InArchiveFormat for a specific file name.
        /// </summary>
        /// <param name="fileName">The archive file name.</param>
        /// <returns>Corresponding InArchiveFormat.</returns>
        /// <exception cref="System.ArgumentException"/>
        public static InArchiveFormat CheckSignature(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                try
                {
                    return CheckSignature(fs);
                }
                catch (ArgumentException)
                {
                    return Formats.FormatByFileName(fileName, true);
                }
            }
        }
    }
#endif
}