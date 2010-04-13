using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using CP.Windows.Forms;

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

        private void b_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ShellFolderBrowser();
            dialog.BrowseFlags |= BrowseFlags.NewDialogStyle;
            dialog.Title = "Select the output folder where to extract files";
            if (dialog.ShowDialog())
            {
                tb_Folder.Text = dialog.FolderPath;
            }
        }

        private void b_Archive_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = "Select the archive file";
            if (dialog.ShowDialog() == CommonFileDialogResult.OK)
            {
                tb_Archive.Text = dialog.FileName;
            }
        }
    }
}
