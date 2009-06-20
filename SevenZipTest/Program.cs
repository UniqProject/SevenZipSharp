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
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using SevenZip;
using System.Diagnostics;

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

            #region Temporary test
            //SevenZipCompressor.SetLibraryPath(@"d:\Work\Misc\7zip\9.04\CPP\7zip\Bundles\Format7zF\7z.dll");
            /*DateTime vshellexecute = DateTime.Now;
            ProcessStartInfo si = new ProcessStartInfo();
            si.FileName = @"c:\Program Files\7-Zip\7z.exe";
            si.UseShellExecute = true;
            si.Arguments = "a -r -mx=9 \"d:\\Temp\\arch.7z\" \"c:\\Program Files\\Microsoft Visual Studio 9.0\\Common7\\IDE\"";
            Process p = Process.Start(si);
            p.WaitForExit();
            Console.WriteLine(DateTime.Now.Subtract(vshellexecute));*/

            /*DateTime vsevenzipsharp = DateTime.Now;
            SevenZipCompressor tmp = new SevenZipCompressor(true);
            tmp.CompressionLevel = CompressionLevel.Ultra;
            tmp.CompressDirectory(@"c:\Program Files\Microsoft Visual Studio 9.0\Common7\IDE\", @"D:\Temp\arch1.7z");
            Console.WriteLine(DateTime.Now.Subtract(vsevenzipsharp));
            vsevenzipsharp = DateTime.Now;
            tmp.CompressDirectory(@"c:\Program Files\Microsoft Visual Studio 9.0\Common7\IDE\", @"D:\Temp\arch2.7z");
            Console.WriteLine(DateTime.Now.Subtract(vsevenzipsharp));*/
            #endregion

            #region Extraction test - ExtractFile
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"d:\Temp\7z465_extra.7z"))
            {                
                for (int i = 0; i < tmp.ArchiveFileData.Count; i++)
                {
                    tmp.ExtractFile(tmp.ArchiveFileData[i].Index, @"d:\temp\!Пусто\");
                }
            }
            //*/
            #endregion

            #region Extraction test - multivolumes
            /*SevenZipExtractor.SetLibraryPath(@"d:\Work\Misc\7zip\9.04\CPP\7zip\Bundles\Format7zF\7z.dll");
            using (SevenZipExtractor tmp = new SevenZipExtractor(@"d:\Temp\Test.7z.001"))
            {
                tmp.ExtractArchive(@"d:\Temp\!Пусто");
            }
            //*/
            #endregion

            #region Compression test - very simple
            /*SevenZipCompressor tmp = new SevenZipCompressor();            
            tmp.CompressDirectory(@"D:\Temp\!Пусто", @"D:\Temp\arch.7z");
            //*/
            #endregion

            #region Compression test - features Append mode
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.CompressionMode = CompressionMode.Append;
            tmp.CompressDirectory(@"D:\Temp\!Пусто", @"D:\Temp\arch.7z");
            //*/
            #endregion

            #region Compression test - multivolumes
            /*SevenZipCompressor tmp = new SevenZipCompressor(true);
            tmp.VolumeSize = 200000;
            tmp.CompressDirectory(@"D:\Temp\!Пусто", @"D:\Temp\arch.7z");            
            //*/
            #endregion
            
            #region Extraction test. Shows cancel feature.
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\test.7z"))
            {
                tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    /*if (e.FileInfo.Index == 10)
                    {
                        e.Cancel = true;
                        Console.WriteLine("Cancelled");
                    }
                    else
                    {//*//*
                        
                       Console.WriteLine(String.Format("[{0}%] {1}",
                           e.PercentDone, e.FileInfo.FileName));
                   //}
               });
               tmp.FileExists += new EventHandler<FileOverwriteEventArgs>((o, e) =>
               {
                   Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                   //e.Overwrite = false;
               });
               tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
               tmp.ExtractArchive(@"D:\Temp\!Пусто");
            }
            //*/
            #endregion

            #region Compression test - shows lots of features 
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.ArchiveFormat = OutArchiveFormat.SevenZip;
            tmp.CompressionLevel = CompressionLevel.High;
            tmp.CompressionMethod = CompressionMethod.Ppmd;
            tmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>((s, e) =>
            {
                /*if (e.PercentDone > 50)
                {
                    e.Cancel = true;
                }
                else
                {
                //*//*
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileName));
                //*//*}
            });
            /*
            tmp.FilesFound += new EventHandler<IntEventArgs>((se, ea) => 
            { 
                Console.WriteLine("Number of files: " + ea.Value.ToString()); 
            });
            //*//*
            tmp.CompressFiles(new string[] { @"c:\log.txt", @"d:\Temp\08022009.jpg" },
               @"d:\Temp\test.bz2");*/
            //tmp.CompressDirectory(@"d:\Temp\!Пусто", @"d:\Temp\arch.7z");
            #endregion

            #region Multi-threaded extraction test
            /*Thread t1 = new Thread(() =>
            {
                using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
                {
                    tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                    {
                        Console.WriteLine(String.Format("[{0}%] {1}",
                            e.PercentDone, e.FileInfo.FileName));
                    });
                    tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                    tmp.ExtractArchive(@"D:\Temp\t1");
                }
            });
            Thread t2 = new Thread(() =>
            {
                using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
                {
                    tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                    {
                        Console.WriteLine(String.Format("[{0}%] {1}",
                            e.PercentDone, e.FileInfo.FileName));
                    });
                    tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                    tmp.ExtractArchive(@"D:\Temp\t2");
                }
            });
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
             //*/
            #endregion

            #region Multi-threaded compression test
            /*Thread t1 = new Thread(() =>
            {
                SevenZipCompressor tmp = new SevenZipCompressor();              
                tmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileName));
                });
                tmp.CompressDirectory(@"D:\Temp\t1", @"D:\Temp\arch1.7z");
            });
            Thread t2 = new Thread(() =>
            {
                SevenZipCompressor tmp = new SevenZipCompressor();
                tmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileName));
                });
                tmp.CompressDirectory(@"D:\Temp\t2", @"D:\Temp\arch2.7z");
            });
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            //*/
            #endregion

            #region Streaming extraction test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(
                File.OpenRead(@"D:\Temp\7z465_extra.7z")))
            {
                tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileInfo.FileName));
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractArchive(@"D:\Temp\!Пусто");
            }//*/
            #endregion

            #region Streaming compression test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>((s, e) =>
            {
                Console.WriteLine(String.Format("[{0}%] {1}",
                    e.PercentDone, e.FileName));
            });
            tmp.CompressDirectory(@"D:\Temp\1",
                File.Create(@"D:\Temp\arch.bz2"));
            //*/
            #endregion

            #region CompressStream (managed) test
            /*SevenZipCompressor.CompressStream(File.OpenRead(@"D:\Temp\test.txt"), 
                File.Create(@"D:\Temp\test.lzma"), null, (o, e) =>
            {
                if (e.PercentDelta > 0)
                {
                    Console.Clear();
                    Console.WriteLine(e.PercentDone.ToString() + "%");
                }
            });
            //*/
            #endregion

            #region ExtractFile(Stream) test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
            {
                tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileInfo.FileName));
                });
                tmp.FileExists += new EventHandler<FileOverwriteEventArgs>((o, e) =>
                {
                    Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                    //e.Overwrite = false;
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractFile(2, File.Create(@"D:\Temp\!Пусто\test.txt"));
            }//*/
            #endregion

            #region ExtractFile(Disk) test
            /*using (SevenZipExtractor tmp = new SevenZipExtractor(@"D:\Temp\7z465_extra.7z"))
            {
                tmp.FileExtractionStarted += new EventHandler<FileInfoEventArgs>((s, e) =>
                {
                    Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileInfo.FileName));
                });
                tmp.FileExists += new EventHandler<FileOverwriteEventArgs>((o, e) =>
                {
                    Console.WriteLine("Warning: file \"" + e.FileName + "\" already exists.");
                    //e.Overwrite = false;
                });
                tmp.ExtractionFinished += new EventHandler((s, e) => { Console.WriteLine("Finished!"); });
                tmp.ExtractFile(4, @"D:\Temp\!Пусто");
            }
            //*/
            #endregion            

            #region CompressFiles Zip test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.ArchiveFormat = OutArchiveFormat.Zip;
            tmp.CompressFiles(new string[] {@"d:\Temp\gpl.txt", @"d:\Temp\ru_office.txt" }, @"d:\Temp\arch.zip");
            //*/
            #endregion

            #region CompressStream (external) test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            tmp.CompressStream(
                File.OpenRead(@"D:\Temp\08022009.jpg"),
                File.Create(@"D:\Temp\arch.7z"));
            //*/
            #endregion

            #region CompressFileDictionary test
            /*SevenZipCompressor tmp = new SevenZipCompressor();
            Dictionary<string, string> fileDict = new Dictionary<string, string>();
            fileDict.Add(@"d:\Temp\temp.ini", "test.ini");
            tmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>((o, e) =>
            {               
                Console.WriteLine(String.Format("[{0}%] {1}",
                        e.PercentDone, e.FileName));
            });
            tmp.CompressFileDictionary(fileDict, @"d:\Temp\arch.7z");
            //*/
            #endregion

            #region Toughness test - throws no exceptions and no leaks
            /*
            Console.ReadKey();
            string exeAssembly = Assembly.GetAssembly(typeof(SevenZipExtractor)).FullName;
            AppDomain dom = AppDomain.CreateDomain("Extract");
            for (int i = 0; i < 1000; i++)
            {
                using (SevenZipExtractor tmp = 
                    (SevenZipExtractor)dom.CreateInstance(
                    exeAssembly, typeof(SevenZipExtractor).FullName,
                    false, BindingFlags.CreateInstance, null, 
                    new object[] {@"D:\Temp\7z465_extra.7z"}, 
                    System.Globalization.CultureInfo.CurrentCulture, null, null).Unwrap())
                {                    
                    tmp.ExtractArchive(@"D:\Temp\!Пусто");
                }
                Console.Clear();
                Console.WriteLine(i);
            }
            AppDomain.Unload(dom);           
            //No errors, no leaks*/
            #endregion

            #region Serialization demo
            /*ArgumentException ex = new ArgumentException("blahblah");
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, ex);
                SevenZipCompressor cmpr = new SevenZipCompressor();
                cmpr.CompressStream(ms, File.Create(@"d:\Temp\test.7z"));
            }
            //*/
            #endregion

            #region Compress with custom parameters demo
            /*SevenZipCompressor tmp = new SevenZipCompressor();            
            tmp.ArchiveFormat = OutArchiveFormat.Zip;
            tmp.CompressionMethod = CompressionMethod.Deflate;
            tmp.CompressionLevel = CompressionLevel.Ultra;
            //Number of fast bytes
            tmp.CustomParameters.Add("fb", "256");
            //Number of deflate passes
            tmp.CustomParameters.Add("pass", "4");
            //Multi-threading on
            tmp.CustomParameters.Add("mt", "on");
            tmp.Compressing += new EventHandler<ProgressEventArgs>((s, e) =>
            {
                Console.Clear();
                Console.WriteLine(String.Format("{0}%", e.PercentDone));
            });
            tmp.CompressDirectory(@"d:\Temp\!Пусто", @"d:\Temp\arch.zip");
            //*/
            #endregion

            #region Sfx demo
            /*SevenZipSfx sfx = new SevenZipSfx();
            SevenZipCompressor tmp = new SevenZipCompressor();
            using (MemoryStream ms = new MemoryStream())
            {
                tmp.CompressDirectory(@"d:\Temp\!Пусто", ms);               
                sfx.MakeSfx(ms, @"d:\Temp\test.exe");
            }
            //*/
            #endregion

            #region Lzma Encode/Decode Stream test
            /*using (FileStream output = new FileStream(@"d:\Temp\arch.lzma", FileMode.Create))
            {
                LzmaEncodeStream encoder = new LzmaEncodeStream(output);
                using (FileStream inputSample = new FileStream(@"d:\Temp\tolstoi_lev_voina_i_mir_kniga_1.rtf", FileMode.Open))
                {
                    int bufSize = 24576, count;
                    byte[] buf = new byte[bufSize];
                    while ((count = inputSample.Read(buf, 0, bufSize)) > 0)
                    {
                        encoder.Write(buf, 0, count);
                    }
                }
                encoder.Close();
            }//*/
            /*using (FileStream input = new FileStream(@"d:\Temp\arch.lzma", FileMode.Open))
            {
                LzmaDecodeStream decoder = new LzmaDecodeStream(input);
                using (FileStream output = new FileStream(@"d:\Temp\res.rtf", FileMode.Create))
                {
                    int bufSize = 24576, count;
                    byte[] buf = new byte[bufSize];
                    while ((count = decoder.Read(buf, 0, bufSize)) > 0)
                    {
                        output.Write(buf, 0, count);
                    }
                }
            }//*/
            #endregion

            Console.WriteLine("Press any key to finish.");
            Console.ReadKey();
        }
    }    
}
