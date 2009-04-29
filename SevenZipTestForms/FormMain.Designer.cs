namespace SevenZipTestForms
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pb_Progress = new System.Windows.Forms.ProgressBar();
            this.pb_Work = new System.Windows.Forms.ProgressBar();
            this.b_Compress = new System.Windows.Forms.Button();
            this.l_Progress = new System.Windows.Forms.Label();
            this.tb_Directory = new System.Windows.Forms.TextBox();
            this.l_Directory = new System.Windows.Forms.Label();
            this.b_Browse = new System.Windows.Forms.Button();
            this.gb_Settings = new System.Windows.Forms.GroupBox();
            this.l_Format = new System.Windows.Forms.Label();
            this.cb_Format = new System.Windows.Forms.ComboBox();
            this.fbd_Directory = new System.Windows.Forms.FolderBrowserDialog();
            this.trb_Level = new System.Windows.Forms.TrackBar();
            this.l_CompressionLevel = new System.Windows.Forms.Label();
            this.l_Method = new System.Windows.Forms.Label();
            this.cb_Method = new System.Windows.Forms.ComboBox();
            this.chb_Sfx = new System.Windows.Forms.CheckBox();
            this.b_BrowseOut = new System.Windows.Forms.Button();
            this.l_Output = new System.Windows.Forms.Label();
            this.tb_Output = new System.Windows.Forms.TextBox();
            this.sfd_Archive = new System.Windows.Forms.SaveFileDialog();
            this.gb_Settings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trb_Level)).BeginInit();
            this.SuspendLayout();
            // 
            // pb_Progress
            // 
            this.pb_Progress.Location = new System.Drawing.Point(12, 25);
            this.pb_Progress.Name = "pb_Progress";
            this.pb_Progress.Size = new System.Drawing.Size(195, 16);
            this.pb_Progress.TabIndex = 0;
            // 
            // pb_Work
            // 
            this.pb_Work.Location = new System.Drawing.Point(12, 47);
            this.pb_Work.MarqueeAnimationSpeed = 25;
            this.pb_Work.Name = "pb_Work";
            this.pb_Work.Size = new System.Drawing.Size(195, 16);
            this.pb_Work.TabIndex = 1;
            // 
            // b_Compress
            // 
            this.b_Compress.Location = new System.Drawing.Point(213, 25);
            this.b_Compress.Name = "b_Compress";
            this.b_Compress.Size = new System.Drawing.Size(66, 38);
            this.b_Compress.TabIndex = 2;
            this.b_Compress.Text = "Compress";
            this.b_Compress.UseVisualStyleBackColor = true;
            this.b_Compress.Click += new System.EventHandler(this.b_Compress_Click);
            // 
            // l_Progress
            // 
            this.l_Progress.AutoSize = true;
            this.l_Progress.Location = new System.Drawing.Point(12, 9);
            this.l_Progress.Name = "l_Progress";
            this.l_Progress.Size = new System.Drawing.Size(48, 13);
            this.l_Progress.TabIndex = 3;
            this.l_Progress.Text = "Progress";
            // 
            // tb_Directory
            // 
            this.tb_Directory.Location = new System.Drawing.Point(12, 84);
            this.tb_Directory.Name = "tb_Directory";
            this.tb_Directory.Size = new System.Drawing.Size(195, 20);
            this.tb_Directory.TabIndex = 4;
            // 
            // l_Directory
            // 
            this.l_Directory.AutoSize = true;
            this.l_Directory.Location = new System.Drawing.Point(12, 68);
            this.l_Directory.Name = "l_Directory";
            this.l_Directory.Size = new System.Drawing.Size(109, 13);
            this.l_Directory.TabIndex = 5;
            this.l_Directory.Text = "Directory to compress";
            // 
            // b_Browse
            // 
            this.b_Browse.Location = new System.Drawing.Point(213, 84);
            this.b_Browse.Name = "b_Browse";
            this.b_Browse.Size = new System.Drawing.Size(66, 20);
            this.b_Browse.TabIndex = 6;
            this.b_Browse.Text = "Browse";
            this.b_Browse.UseVisualStyleBackColor = true;
            this.b_Browse.Click += new System.EventHandler(this.b_Browse_Click);
            // 
            // gb_Settings
            // 
            this.gb_Settings.Controls.Add(this.chb_Sfx);
            this.gb_Settings.Controls.Add(this.l_Method);
            this.gb_Settings.Controls.Add(this.cb_Method);
            this.gb_Settings.Controls.Add(this.l_CompressionLevel);
            this.gb_Settings.Controls.Add(this.trb_Level);
            this.gb_Settings.Controls.Add(this.l_Format);
            this.gb_Settings.Controls.Add(this.cb_Format);
            this.gb_Settings.Location = new System.Drawing.Point(12, 158);
            this.gb_Settings.Name = "gb_Settings";
            this.gb_Settings.Size = new System.Drawing.Size(267, 142);
            this.gb_Settings.TabIndex = 7;
            this.gb_Settings.TabStop = false;
            this.gb_Settings.Text = "Settings";
            // 
            // l_Format
            // 
            this.l_Format.AutoSize = true;
            this.l_Format.Location = new System.Drawing.Point(9, 22);
            this.l_Format.Name = "l_Format";
            this.l_Format.Size = new System.Drawing.Size(39, 13);
            this.l_Format.TabIndex = 1;
            this.l_Format.Text = "Format";
            // 
            // cb_Format
            // 
            this.cb_Format.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_Format.FormattingEnabled = true;
            this.cb_Format.Location = new System.Drawing.Point(54, 19);
            this.cb_Format.Name = "cb_Format";
            this.cb_Format.Size = new System.Drawing.Size(77, 21);
            this.cb_Format.TabIndex = 0;
            // 
            // trb_Level
            // 
            this.trb_Level.Location = new System.Drawing.Point(12, 71);
            this.trb_Level.Name = "trb_Level";
            this.trb_Level.Size = new System.Drawing.Size(242, 45);
            this.trb_Level.TabIndex = 2;
            this.trb_Level.Scroll += new System.EventHandler(this.trb_Level_Scroll);
            // 
            // l_CompressionLevel
            // 
            this.l_CompressionLevel.AutoSize = true;
            this.l_CompressionLevel.Location = new System.Drawing.Point(13, 55);
            this.l_CompressionLevel.Name = "l_CompressionLevel";
            this.l_CompressionLevel.Size = new System.Drawing.Size(92, 13);
            this.l_CompressionLevel.TabIndex = 3;
            this.l_CompressionLevel.Text = "Compression level";
            // 
            // l_Method
            // 
            this.l_Method.AutoSize = true;
            this.l_Method.Location = new System.Drawing.Point(139, 22);
            this.l_Method.Name = "l_Method";
            this.l_Method.Size = new System.Drawing.Size(43, 13);
            this.l_Method.TabIndex = 5;
            this.l_Method.Text = "Method";
            // 
            // cb_Method
            // 
            this.cb_Method.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_Method.FormattingEnabled = true;
            this.cb_Method.Location = new System.Drawing.Point(184, 19);
            this.cb_Method.Name = "cb_Method";
            this.cb_Method.Size = new System.Drawing.Size(70, 21);
            this.cb_Method.TabIndex = 4;
            // 
            // chb_Sfx
            // 
            this.chb_Sfx.AutoSize = true;
            this.chb_Sfx.Location = new System.Drawing.Point(16, 119);
            this.chb_Sfx.Name = "chb_Sfx";
            this.chb_Sfx.Size = new System.Drawing.Size(93, 17);
            this.chb_Sfx.TabIndex = 6;
            this.chb_Sfx.Text = "Self-extraction";
            this.chb_Sfx.UseVisualStyleBackColor = true;
            // 
            // b_BrowseOut
            // 
            this.b_BrowseOut.Location = new System.Drawing.Point(213, 132);
            this.b_BrowseOut.Name = "b_BrowseOut";
            this.b_BrowseOut.Size = new System.Drawing.Size(66, 20);
            this.b_BrowseOut.TabIndex = 10;
            this.b_BrowseOut.Text = "Browse";
            this.b_BrowseOut.UseVisualStyleBackColor = true;
            this.b_BrowseOut.Click += new System.EventHandler(this.b_BrowseOut_Click);
            // 
            // l_Output
            // 
            this.l_Output.AutoSize = true;
            this.l_Output.Location = new System.Drawing.Point(12, 116);
            this.l_Output.Name = "l_Output";
            this.l_Output.Size = new System.Drawing.Size(88, 13);
            this.l_Output.TabIndex = 9;
            this.l_Output.Text = "Archive file name";
            // 
            // tb_Output
            // 
            this.tb_Output.Location = new System.Drawing.Point(12, 132);
            this.tb_Output.Name = "tb_Output";
            this.tb_Output.Size = new System.Drawing.Size(195, 20);
            this.tb_Output.TabIndex = 8;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(290, 312);
            this.Controls.Add(this.b_BrowseOut);
            this.Controls.Add(this.l_Output);
            this.Controls.Add(this.tb_Output);
            this.Controls.Add(this.gb_Settings);
            this.Controls.Add(this.b_Browse);
            this.Controls.Add(this.l_Directory);
            this.Controls.Add(this.tb_Directory);
            this.Controls.Add(this.l_Progress);
            this.Controls.Add(this.b_Compress);
            this.Controls.Add(this.pb_Work);
            this.Controls.Add(this.pb_Progress);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "SevenZipSharp Windows Forms Demonstration";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.gb_Settings.ResumeLayout(false);
            this.gb_Settings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trb_Level)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar pb_Progress;
        private System.Windows.Forms.ProgressBar pb_Work;
        private System.Windows.Forms.Button b_Compress;
        private System.Windows.Forms.Label l_Progress;
        private System.Windows.Forms.TextBox tb_Directory;
        private System.Windows.Forms.Label l_Directory;
        private System.Windows.Forms.Button b_Browse;
        private System.Windows.Forms.GroupBox gb_Settings;
        private System.Windows.Forms.Label l_Format;
        private System.Windows.Forms.ComboBox cb_Format;
        private System.Windows.Forms.FolderBrowserDialog fbd_Directory;
        private System.Windows.Forms.Label l_CompressionLevel;
        private System.Windows.Forms.TrackBar trb_Level;
        private System.Windows.Forms.Label l_Method;
        private System.Windows.Forms.ComboBox cb_Method;
        private System.Windows.Forms.CheckBox chb_Sfx;
        private System.Windows.Forms.Button b_BrowseOut;
        private System.Windows.Forms.Label l_Output;
        private System.Windows.Forms.TextBox tb_Output;
        private System.Windows.Forms.SaveFileDialog sfd_Archive;
    }
}

