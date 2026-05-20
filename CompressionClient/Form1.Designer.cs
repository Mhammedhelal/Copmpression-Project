using System;
using System.Drawing;
using System.Windows.Forms;

namespace CompressionClient
{
    partial class Form1
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

        // UI Control Declarations
        private Panel _panelHeader;
        private Label _lblTitle;
        private GroupBox _grpConnection;
        private Label _lblServer;
        private TextBox _txtServer;
        private Label _lblPort;
        private NumericUpDown _numPort;
        private Button _btnConnect;
        private Panel _panelStatus;
        private Label _lblConnStatus;

        private GroupBox _grpFile;
        private TextBox _txtFilePath;
        private Button _btnBrowse;
        private Label _lblFileInfo;

        private GroupBox _grpSave;
        private TextBox _txtSavePath;
        private Button _btnSaveBrowse;

        private Button _btnSend;

        private GroupBox _grpProgress;
        private ProgressBar _progressBar;
        private Label _lblProgressText;

        private GroupBox _grpLog;
        private RichTextBox _rtbLog;
        private Button _btnClearLog;

        /// <summary>
        /// Method required for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form Configurations
            this.Text = "File Compression Client";
            this.Size = new Size(680, 720);
            this.MinimumSize = new Size(680, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 244, 248);
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Header Panel 
            _panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(13, 27, 42)
            };

            _lblTitle = new Label
            {
                Text = "File Compression Client",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 10),
                AutoSize = true
            };


            _panelHeader.Controls.Add(_lblTitle);
            this.Controls.Add(_panelHeader);

            // Connection Settings Group Box
            _grpConnection = MakeGroupBox("Server Connection", 88, 636, 110);

            _lblServer = MakeLabel("Server IP:", 16, 26);
            _txtServer = new TextBox
            {
                Text = "127.0.0.1",
                Location = new Point(90, 23),
                Width = 160,
                Font = new Font("Consolas", 10f)
            };

            _lblPort = MakeLabel("Port:", 270, 26);
            _numPort = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 9050,
                Location = new Point(305, 23),
                Width = 80,
                Font = new Font("Consolas", 10f)
            };

            _btnConnect = new Button
            {
                Text = "Test Connection",
                Location = new Point(400, 21),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(27, 122, 143),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Click += BtnConnect_Click;

            _panelStatus = new Panel
            {
                Location = new Point(16, 60),
                Size = new Size(604, 30),
                BackColor = Color.FromArgb(230, 237, 243)
            };

            _lblConnStatus = new Label
            {
                Text = "●  Not connected",
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(8, 6),
                AutoSize = true
            };
            _panelStatus.Controls.Add(_lblConnStatus);

            _grpConnection.Controls.AddRange(new Control[] {
                _lblServer, _txtServer, _lblPort, _numPort, _btnConnect, _panelStatus
            });
            this.Controls.Add(_grpConnection);

            // File Selection Group Box
            _grpFile = MakeGroupBox("Select File to Compress", 210, 636, 100);

            _txtFilePath = new TextBox
            {
                Location = new Point(16, 26),
                Width = 510,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9f),
                PlaceholderText = "No file selected …"
            };

            _btnBrowse = new Button
            {
                Text = "Browse …",
                Location = new Point(535, 24),
                Size = new Size(85, 28),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnBrowse.FlatAppearance.BorderSize = 0;
            _btnBrowse.Click += BtnBrowse_Click;

            _lblFileInfo = new Label
            {
                Text = "No file selected.",
                Location = new Point(16, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5f)
            };

            _grpFile.Controls.AddRange(new Control[] {
                _txtFilePath, _btnBrowse, _lblFileInfo
            });
            this.Controls.Add(_grpFile);

            // Save Location Group Box
            _grpSave = MakeGroupBox("Save Compressed File To", 322, 636, 70);

            _txtSavePath = new TextBox
            {
                Location = new Point(16, 26),
                Width = 510,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9f),
                PlaceholderText = "No save location selected …"
            };

            _btnSaveBrowse = new Button
            {
                Text = "Browse …",
                Location = new Point(535, 24),
                Size = new Size(85, 28),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnSaveBrowse.FlatAppearance.BorderSize = 0;
            _btnSaveBrowse.Click += BtnSaveBrowse_Click;

            _grpSave.Controls.AddRange(new Control[] { _txtSavePath, _btnSaveBrowse });
            this.Controls.Add(_grpSave);

            // Master Send Button
            _btnSend = new Button
            {
                Text = "⬆  Send File & Compress",
                Location = new Point(20, 405),
                Size = new Size(636, 46),
                BackColor = Color.FromArgb(13, 27, 42),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += BtnSend_Click;
            this.Controls.Add(_btnSend);

            // Progress Bar Group Box
            _grpProgress = MakeGroupBox("Progress", 463, 636, 62);

            _progressBar = new ProgressBar
            {
                Location = new Point(16, 24),
                Size = new Size(504, 22),
                Style = ProgressBarStyle.Continuous
            };

            _lblProgressText = new Label
            {
                Text = "Idle",
                Location = new Point(524, 27),
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Segoe UI", 8.5f)
            };

            _grpProgress.Controls.AddRange(new Control[] { _progressBar, _lblProgressText });
            this.Controls.Add(_grpProgress);

            // Output Activity Log Group Box
            _grpLog = MakeGroupBox("Activity Log", 537, 636, 148);

            _rtbLog = new RichTextBox
            {
                Location = new Point(16, 24),
                Size = new Size(504, 108),
                BackColor = Color.FromArgb(20, 30, 40),
                ForeColor = Color.FromArgb(180, 220, 255),
                Font = new Font("Consolas", 8.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };

            _btnClearLog = new Button
            {
                Text = "Clear",
                Location = new Point(524, 24),
                Size = new Size(96, 28),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnClearLog.FlatAppearance.BorderSize = 0;
            _btnClearLog.Click += BtnClearLog_Click;

            _grpLog.Controls.AddRange(new Control[] { _rtbLog, _btnClearLog });
            this.Controls.Add(_grpLog);

            this.ResumeLayout(false);
        }

        // Helper Layout Builders
        private static GroupBox MakeGroupBox(string title, int topMargin, int width, int height)
        {
            return new GroupBox
            {
                Text = title,
                Location = new Point(20, topMargin),
                Size = new Size(width, height),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 90, 130)
            };
        }

        private static Label MakeLabel(string titleText, int xPosition, int yPosition)
        {
            return new Label
            {
                Text = titleText,
                Location = new Point(xPosition, yPosition),
                AutoSize = true,
                ForeColor = Color.FromArgb(60, 60, 60)
            };
        }
    }
}