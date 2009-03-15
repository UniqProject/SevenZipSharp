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
using System.Threading;
using System.IO;
using SevenZip;

namespace SevenZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SevenZipSharp test application.");

            /*
             Common questions.
             
             You may specify custom path to 7-zip dll at SevenZipLibraryManager.LibraryFileName 
             
             For adding custom archive extensions, see Formats.InExtensionFormats
             */

            /*#region Multi-threaded extraction test
            Thread t1 = new Thread(() =>
            {
                using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
                {
                    tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                    {
                        Console.WriteLine(String.Format("[{0}%] {1}",
                            e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                    });
                    tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                    tmp.ExtractArchive(@"D:\Temp\t1");
                }
            });
            Thread t2 = new Thread(() =>
            {
                using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
                {
                    tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                    {
                        Console.WriteLine(String.Format("[{0}%] {1}",
                            e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                    });
                    tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                    tmp.ExtractArchive(@"D:\Temp\t2");
                }
            });
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            #endregion*/

            /*#region Multi-threaded compression test
            Thread t1 = new Thread(() =>
            {
                SevenZipCompressor tmp = new SevenZipCompressor();
                tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileInfo.Name));
                });
                tmp.CompressDirectory(@"D:\Temp",
                    @"D:\Out\arch1.7z", OutArchiveFormat.SevenZip);
            });
            Thread t2 = new Thread(() =>
            {
                SevenZipCompressor tmp = new SevenZipCompressor();
                tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileInfo.Name));
                });
                tmp.CompressDirectory(@"D:\Temp\",
                    @"D:\Out\arch2.7z", OutArchiveFormat.SevenZip);
            });
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            #endregion*/

            /*#region Streaming extraction test
            using (SevenZipExtractor tmp = new SevenZipExtractor(
                File.OpenRead(@"D:\Temp\7z465_extra.7z"), InArchiveFormat.SevenZip))
            {
                tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractArchive(@"D:\Temp");
            }
            #endregion*/

            /*#region Streaming compression test
            SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
            {
                Console.WriteLine(String.Format("[{0}%] {1}",
                    e.PercentDone, e.FileInfo.Name));
            });
            tmp.CompressDirectory(@"D:\Temp\1",
                File.Create(@"D:\Temp\arch.7z"), OutArchiveFormat.SevenZip);
            #endregion*/

            Console.WriteLine("Press any key to finish.");
            Console.ReadKey();
        }
    }
}
