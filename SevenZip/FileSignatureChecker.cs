using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SevenZip
{
    /// <summary>
    /// The signature checker class. Original code by Siddharth Uppal, adapted by Markhor.
    /// </summary>
    /// <remarks>Based on the code at http://blog.somecreativity.com/2008/04/08/how-to-check-if-a-file-is-compressed-in-c/#</remarks>
    public static class FileChecker
    {
        private const int signatureSize = 16;

        private static InArchiveFormat SpecialDetect(Stream stream, int offset, InArchiveFormat expectedFormat)
        {
            if (stream.Length > offset + signatureSize)
            {
                byte[] signature = new byte[signatureSize];
                int bytesRequired = signatureSize;
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
                        return expectedFormat;
                    }
                }
            }
            throw new ArgumentException();
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
            if (stream.Length < signatureSize)
            {
                throw new ArgumentException("The stream is invalid.");
            }

            #region Get file signature
            byte[] signature = new byte[signatureSize];
            int bytesRequired = signatureSize;
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
            catch (ArgumentException) { }
            try
            {
                SpecialDetect(stream, 8001, InArchiveFormat.Iso);
            }
            catch (ArgumentException) { }
            try
            {
                SpecialDetect(stream, 8801, InArchiveFormat.Iso);
            }
            catch (ArgumentException) { }
            try
            {
                SpecialDetect(stream, 9001, InArchiveFormat.Iso);
            }
            catch (ArgumentException) { }
            
            throw new ArgumentException("The stream is invalid.");            
        }        

        /// <summary>
        /// Gets the InArchiveFormat for a specific file name.
        /// </summary>
        /// <param name="fileName">The archive file name.</param>
        /// <returns>Corresponding InArchiveFormat.</returns>
        public static InArchiveFormat CheckSignature(string fileName)
        {
            using (FileStream fs = File.OpenRead(fileName))
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
}
