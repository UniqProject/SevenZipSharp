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
using System.Net;

namespace SevenZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SevenZipSharp test application.");

            /*
             Common questions.
             
             You may specify the custom path to 7-zip dll at SevenZipLibraryManager.LibraryFileName 
                or call SevenZipExtractor.SetLibraryPath(@"c:\Program Files\7-Zip\7z.dll");
                or call SevenZipCompressor.SetLibraryPath(@"c:\Program Files\7-Zip\7z.dll");
             
             For adding custom archive extensions, see Formats.InExtensionFormats
             
            */

            #region Extraction test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\test.7z"))
            {
                tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                });
                tmp.FileExists += new EventHandler<FileNameEventArgs>((o, e) =>
                {
                    Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                    //e.Overwrite = false;
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractArchive(@"D:\Temp\");
            }*/  
            #endregion

            #region Compression test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
            {
                Console.WriteLine(String.Format("[{0}%] {1}",
                    e.PercentDone, e.FileInfo.Name));
            });
            tmp.CompressFiles(new string[] { @"c:\log.txt", @"d:\Temp\08022009.jpg" },
                @"d:\Temp\test.bz2", OutArchiveFormat.Zip);
            tmp.CompressDirectory(@"d:\Temp", @"d:\arch.7z", OutArchiveFormat.SevenZip);*/
            #endregion

            #region Multi-threaded extraction test
            /*Thread t1 = new Thread(() =>
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
            t2.Join();*/
            #endregion

            #region Multi-threaded compression test
            /*Thread t1 = new Thread(() =>
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
            t2.Join();*/
            #endregion

            #region Streaming extraction test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(
                File.OpenRead(@"D:\Temp\7z465_extra.7z"), InArchiveFormat.SevenZip))
            {
                tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractArchive(@"D:\Temp\!Пусто");
            }*/
            #endregion

            #region Streaming compression test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
            {
                Console.WriteLine(String.Format("[{0}%] {1}",
                    e.PercentDone, e.FileInfo.Name));
            });
            tmp.CompressDirectory(@"D:\Temp\1",
                File.Create(@"D:\Temp\arch.bz2"), OutArchiveFormat.BZip2);
             */
            #endregion

            #region CompressStream (internal) test
            /*SevenZipCompressor.CompressStream(File.OpenRead(@"D:\Temp\installer.msi"), 
                File.Create(@"D:\Temp\test.lzma"), null, (o, e) =>
            {
                if (e.PercentDelta > 0)
                {
                    Console.Clear();
                    Console.WriteLine(e.PercentDone.ToString() + "%");
                }
            });*/
            #endregion

            #region ExtractFile(Stream) test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
            {
                tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                });
                tmp.FileExists += new EventHandler<FileNameEventArgs>((o, e) =>
                {
                    Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                    //e.Overwrite = false;
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractFile(2, File.Create(@"D:\Temp\!Пусто\test.txt"));
            }*/
            #endregion

            #region CompressStream (external) test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.CompressStream(
                File.OpenRead(@"D:\Temp\test.msi"),
                File.Create(@"D:\Temp\arch.7z"), OutArchiveFormat.SevenZip);
            */ 
            #endregion

            #region Web stream test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(new Uri(@"http://127.0.0.1/test.7z")))
            {
                //For less traffic consumption, do not use this event
                tmp.FileExtractionStarted += new EventHandler<IndexEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, tmp.ArchiveFileData[e.FileIndex].FileName));
                });

                tmp.FileExists += new EventHandler<FileNameEventArgs>((o, e) =>
                {
                    Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                    //e.Overwrite = false;
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractArchive(@"D:\Temp\");
            }*/
            #endregion

            Console.WriteLine("Press any key to finish.");
            Console.ReadKey();
        }
    }
}
