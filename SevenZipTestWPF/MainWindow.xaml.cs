using System;
using System.IO;
using System.Windows;
using CP.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using SevenZip;
using System.Threading;

namespace SevenZipTestWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        delegate void SetProgressDelegate(ProgressEventArgs args);
        delegate void SetFileNameDelegate(FileNameEventArgs args);
        delegate void SetInfoDelegate(FileInfoEventArgs args);
        delegate void SetOverwriteDelegate(FileOverwriteEventArgs args);
        delegate void SetSettings(SevenZipCompressor compr);
        delegate void SetNoArgsDelegate();

        private void b_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ShellFolderBrowser();
            dialog.BrowseFlags |= BrowseFlags.NewDialogStyle;
            dialog.Title = "Select the output folder where to extract files";
            if (dialog.ShowDialog())
            {
                tb_ExtractFolder.Text = dialog.FolderPath;
            }
        }

        private void b_Archive_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = "Select the archive file";
            if (dialog.ShowDialog() == CommonFileDialogResult.OK)
            {
                tb_ExtractArchive.Text = dialog.FileName;
                tb_Messages.Text = "";
                using (var extr = new SevenZipExtractor(dialog.FileName))
                {
                    pb_Extract2.Maximum = extr.ArchiveFileData.Count;
                    tb_Messages.BeginChange();
                    foreach (var item in extr.ArchiveFileData)
                    {
                        tb_Messages.Text += string.Format("{0} [{1}]" + Environment.NewLine, item.FileName, item.Size);
                    }
                    tb_Messages.EndChange();
                    tb_Messages.ScrollToEnd();
                }
            }
        }

        private void b_Extract_Click(object sender, RoutedEventArgs e)
        {
            var worker = new Thread(new ThreadStart(Extract));
            worker.Start(); 
        }

        private void Extract()
        {
            SevenZipExtractor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            string fileName = "";
            string directory = "";
            Dispatcher.Invoke(new SetNoArgsDelegate(() =>
            {
                tb_Messages.Text = "Started" + Environment.NewLine;
                fileName = tb_ExtractArchive.Text;
                directory = tb_ExtractFolder.Text;
            }));
            using (var extr = new SevenZipExtractor(fileName))
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
            Dispatcher.Invoke(new SetNoArgsDelegate(() =>
            {
                pb_Extract1.Value = 0;
                pb_Extract2.Value = 0;
                tb_Messages.Text += "Finished!" + Environment.NewLine;
            }));
        }

        void extr_FileExists(object sender, FileOverwriteEventArgs e)
        {
            Dispatcher.Invoke(new SetOverwriteDelegate((args) =>
            {
                tb_Messages.Text += String.Format(
                    "Warning: \"{0}\" already exists; overwritten" + Environment.NewLine,
                    args.FileName);
            }), e);
        }

        void extr_FileExtractionStarted(object sender, FileInfoEventArgs e)
        {
            Dispatcher.Invoke(new SetInfoDelegate((args) =>
            {
                tb_Messages.Text += String.Format("Extracting \"{0}\"" + Environment.NewLine, args.FileInfo.FileName);
                tb_Messages.ScrollToEnd();
                pb_Extract2.Value += 1;
            }), e);
        }

        void extr_Extracting(object sender, ProgressEventArgs e)
        {
            Dispatcher.Invoke(new SetProgressDelegate((args) =>
            {
                pb_Extract1.Value += args.PercentDelta;
            }), e);
        }

    }
}
