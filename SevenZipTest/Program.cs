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
using System.Text;
using SevenZip;

namespace SevenZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SevenZipSharp test application.");
            /*SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z");
            tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
            {
                Console.WriteLine(String.Format("[{0}%] {1}", 
                    (100 * (e.FileIndex + 1))/tmp.FilesCount, tmp.ArchiveFileData[e.FileIndex].FileName));
            });
            tmp.ExtractionFinished += new EventHandler((s, e) => {Console.WriteLine("Finished!");});
            tmp.ExtractArchive(@"D:\Temp\");*/
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) => 
            {
                Console.WriteLine(String.Format("[{0}%] {1}",
                    e.PercentDone, e.FileInfo.Name));
            });
            tmp.CompressDirectory(@"D:\Temp",
                @"D:\Temp\arch.7z", OutArchiveFormat.SevenZip);*/
            Console.WriteLine("Press any key to finish.");
            Console.ReadKey();
        }
    }
}
