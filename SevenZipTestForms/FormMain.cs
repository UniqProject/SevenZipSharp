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
using System.Linq;
using System.Windows.Forms;
using SevenZip;
using System.IO;
using System.Threading;

namespace SevenZipTestForms
{
    public partial class FormMain : Form
    {
        delegate void SetProgressDelegate(ProgressEventArgs args);
        delegate void SetFileNameDelegate(FileNameEventArgs args);
        delegate void SetInfoDelegate(FileInfoEventArgs args);
        delegate void SetOverwriteDelegate(FileOverwriteEventArgs args);
        delegate void SetSettings(SevenZipCompressor compr);
        delegate void SetNoArgsDelegate();

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            cb_Format.Items.AddRange(Enum.GetNames(typeof(OutArchiveFormat)));
            cb_Method.Items.AddRange(Enum.GetNames(typeof(CompressionMethod)));
            cb_Format.SelectedIndex = (int)OutArchiveFormat.SevenZip;
            cb_Method.SelectedIndex = (int)CompressionMethod.Default;
            tb_CompressDirectory.Text = @"D:\Temp\!Пусто";
            tb_CompressOutput.Text = @"D:\Temp\arch.7z";
            tb_ExtractDirectory.Text = @"D:\Temp\!Пусто";
            tb_ExtractArchive.Text = @"D:\Temp\7z465_extra.7z";
            trb_Level.Maximum = Enum.GetNames(typeof(CompressionLevel)).Length - 1;
            trb_Level.Value = (int)CompressionLevel.Normal;
            trb_Level_Scroll(this, EventArgs.Empty);
        }        

        private void Compress()
        {
            SevenZipCompressor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            SevenZipCompressor cmp = new SevenZipCompressor();
            cmp.Compressing += new EventHandler<ProgressEventArgs>(cmp_Compressing);
            cmp.FileCompressionStarted += new EventHandler<FileNameEventArgs>(cmp_FileCompressionStarted);
            cmp.CompressionFinished += new EventHandler(cmp_CompressionFinished);
            this.Invoke(new SetSettings((compressor)=>
            {
                compressor.ArchiveFormat = (OutArchiveFormat)Enum.Parse(typeof(OutArchiveFormat), cb_Format.Text);
                compressor.CompressionLevel = (CompressionLevel)trb_Level.Value;
                compressor.CompressionMethod = (CompressionMethod)cb_Method.SelectedIndex;
            }), cmp);
            string directory = "";
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                directory = tb_CompressDirectory.Text;
            }));
            string archFileName = "";
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                archFileName = tb_CompressOutput.Text;
            }));
            bool sfxMode = false;
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                sfxMode = chb_Sfx.Checked;
            }));
            if (!sfxMode)
            {
                cmp.CompressDirectory(directory, archFileName);
            }
            else
            {
                SevenZipSfx sfx = new SevenZipSfx();
                using (MemoryStream ms = new MemoryStream())
                {
                    cmp.CompressDirectory(directory, ms);
                    sfx.MakeSfx(ms, archFileName.Substring(0, archFileName.LastIndexOf('.')) + ".exe");
                }
            }
        }

        private void b_Compress_Click(object sender, EventArgs e)
        {
            pb_CompressWork.Style = ProgressBarStyle.Marquee;
            Thread worker = new Thread(new ThreadStart(Compress));
            worker.Start();            
        }

        void cmp_Compressing(object sender, ProgressEventArgs e)
        {
            pb_CompressProgress.Invoke(new SetProgressDelegate((args) =>
            {
                pb_CompressProgress.Increment(args.PercentDelta);
            }), e);
        }

        void cmp_FileCompressionStarted(object sender, FileNameEventArgs e)
        {
            l_CompressProgress.Invoke(new SetFileNameDelegate((args) =>
            {
                l_CompressProgress.Text = String.Format("Compressing \"{0}\"", e.FileName);
            }), e);
        }

        void cmp_CompressionFinished(object sender, EventArgs e)
        {
            l_CompressProgress.Invoke(new SetNoArgsDelegate(() =>
            {
                l_CompressProgress.Text = "Finished";
                pb_CompressWork.Style = ProgressBarStyle.Blocks;
                pb_CompressProgress.Value = 0;
            }));
        }

        private void b_Browse_Click(object sender, EventArgs e)
        {
            if (fbd_Directory.ShowDialog() == DialogResult.OK)
            {
                tb_CompressDirectory.Text = fbd_Directory.SelectedPath;
            }
        }

        private void trb_Level_Scroll(object sender, EventArgs e)
        {
            l_CompressionLevel.Text = String.Format("Compression level: {0}", (CompressionLevel)trb_Level.Value);
        }

        private void b_BrowseOut_Click(object sender, EventArgs e)
        {
            if (sfd_Archive.ShowDialog() == DialogResult.OK)
            {
                tb_CompressOutput.Text = sfd_Archive.FileName;
            }
        }

        private void Extract()
        {
            SevenZipExtractor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            string fileName = "";
            string directory = "";
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                fileName = tb_ExtractArchive.Text;
                directory = tb_ExtractDirectory.Text;
            }));
            using (SevenZipExtractor extr = new SevenZipExtractor(fileName))
            {
                extr.Extracting += new EventHandler<ProgressEventArgs>(extr_Extracting);
                extr.FileExtractionStarted += new EventHandler<FileInfoEventArgs>(extr_FileExtractionStarted);
                extr.FileExists += new EventHandler<FileOverwriteEventArgs>(extr_FileExists);
                extr.ExtractionFinished += new EventHandler(extr_ExtractionFinished);
                extr.ExtractArchive(directory);
            }
        }

        void extr_ExtractionFinished(object sender, EventArgs e)
        {
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                pb_ExtractWork.Style = ProgressBarStyle.Blocks;
                pb_ExtractProgress.Value = 0;
                l_ExtractProgress.Text = "Finished";
            }));
        }

        void extr_FileExists(object sender, FileOverwriteEventArgs e)
        {
            tb_Messages.Invoke(new SetOverwriteDelegate((args) =>
            {
                tb_Messages.Text += String.Format("Warning: \"{0}\" already exists; overwritten\r\n", args.FileName);
            }), e);
        }

        void extr_FileExtractionStarted(object sender, FileInfoEventArgs e)
        {
            l_ExtractProgress.Invoke(new SetInfoDelegate((args) =>
            {
                l_ExtractProgress.Text = String.Format("Extracting \"{0}\"", args.FileInfo.FileName);
            }), e);
        }

        void extr_Extracting(object sender, ProgressEventArgs e)
        {
            pb_ExtractProgress.Invoke(new SetProgressDelegate((args) =>
            {
                pb_ExtractProgress.Increment(args.PercentDelta);
            }), e);
        }

        private void b_Extract_Click(object sender, EventArgs e)
        {
            pb_ExtractWork.Style = ProgressBarStyle.Marquee;
            Thread worker = new Thread(new ThreadStart(Extract));
            worker.Start(); 
        }

        private void b_ExtractBrowseDirectory_Click(object sender, EventArgs e)
        {
            if (fbd_Directory.ShowDialog() == DialogResult.OK)
            {
                tb_ExtractDirectory.Text = fbd_Directory.SelectedPath;
            }
        }

        private void b_ExtractBrowseArchive_Click(object sender, EventArgs e)
        {
            if (ofd_Archive.ShowDialog() == DialogResult.OK)
            {
                tb_ExtractArchive.Text = ofd_Archive.FileName;
                using (SevenZipExtractor extr = new SevenZipExtractor(ofd_Archive.FileName))
                {
                    tb_Messages.Lines = extr.ArchiveFileNames.ToArray<string>();
                }
            }
        }
    }
}
