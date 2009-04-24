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
        /// <summary>
        /// Gets the InArchiveFormat for a specific extension.
        /// </summary>
        /// <param name="stream">The stream to identify.</param>
        /// <returns>Corresponding InArchiveFormat.</returns>
        public static InArchiveFormat CheckSignature(Stream stream)
        {
            const int signatureSize = 16;
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
            #region Detect tar
            if (stream.Length > 257 + signatureSize)
            {
                bytesRequired = signatureSize;
                index = 0;
                stream.Seek(257, SeekOrigin.Begin);
                while (bytesRequired > 0)
                {
                    int bytesRead = stream.Read(signature, index, bytesRequired);
                    bytesRequired -= bytesRead;
                    index += bytesRead;
                }
                actualSignature = BitConverter.ToString(signature);
                foreach (string expectedSignature in Formats.InSignatureFormats.Keys)
                {
                    if (Formats.InSignatureFormats[expectedSignature] != InArchiveFormat.Tar)
                    {
                        continue;
                    }
                    if (actualSignature.StartsWith(expectedSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        return InArchiveFormat.Tar;
                    }
                }
            }
            #endregion
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
