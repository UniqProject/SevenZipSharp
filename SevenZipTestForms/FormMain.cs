using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SevenZip;
using System.IO;
using System.Threading;

namespace SevenZipTestForms
{
    public partial class FormMain : Form
    {
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
            tb_Directory.Text = @"D:\Temp\!Пусто";
            tb_Output.Text = @"D:\Temp\arch.7z";
            trb_Level.Maximum = Enum.GetNames(typeof(CompressionLevel)).Length - 1;
            trb_Level.Value = (int)CompressionLevel.Normal;
            trb_Level_Scroll(this, EventArgs.Empty);
        }

        delegate void SetCompressing(ProgressEventArgs args);
        delegate void SetFileCompressionStarted(FileInfoEventArgs args);
        delegate void SetSettings(SevenZipCompressor compr);
        delegate void SetNoArgsDelegate();

        private void Compress()
        {
            SevenZipCompressor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            SevenZipCompressor cmp = new SevenZipCompressor();
            cmp.Compressing += new EventHandler<ProgressEventArgs>(cmp_Compressing);
            cmp.FileCompressionStarted += new EventHandler<FileInfoEventArgs>(cmp_FileCompressionStarted);
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
                directory = tb_Directory.Text;
            }));
            string archFileName = "";
            this.Invoke(new SetNoArgsDelegate(() =>
            {
                archFileName = tb_Output.Text;
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
            pb_Work.Style = ProgressBarStyle.Marquee;
            Thread worker = new Thread(new ThreadStart(Compress));
            worker.Start();            
        }

        void cmp_Compressing(object sender, ProgressEventArgs e)
        {
            if (pb_Progress.InvokeRequired)
            {
                pb_Progress.Invoke(new SetCompressing((args) =>
                {
                    pb_Progress.Increment(args.PercentDelta);
                }), e);
            }
            else
            {
                pb_Progress.Increment(e.PercentDelta);
            }
        }

        void cmp_FileCompressionStarted(object sender, FileInfoEventArgs e)
        {
            if (l_Progress.InvokeRequired)
            {
                l_Progress.Invoke(new SetFileCompressionStarted((args) =>
                {
                    l_Progress.Text = String.Format("Compressing \"{0}\"", e.FileName);
                }), e);
            }
        }

        void cmp_CompressionFinished(object sender, EventArgs e)
        {
            if (l_Progress.InvokeRequired)
            {
                l_Progress.Invoke(new SetNoArgsDelegate(() =>
                {
                    l_Progress.Text = "Finished";
                    pb_Work.Style = ProgressBarStyle.Blocks;
                    pb_Progress.Value = 0;
                }));                
            }
        }

        private void b_Browse_Click(object sender, EventArgs e)
        {
            if (fbd_Directory.ShowDialog() == DialogResult.OK)
            {
                tb_Directory.Text = fbd_Directory.SelectedPath;
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
                tb_Output.Text = sfd_Archive.FileName;
            }
        }
    }
}
