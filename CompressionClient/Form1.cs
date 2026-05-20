using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace CompressionClient
{
    public partial class Form1 : Form
    {
        // Global variables to track state
        private string _selectedFilePath = "";
        private string _savePath = "";
        private bool _isSending = false;

        public Form1()
        {
            InitializeComponent();
            AppendLog("Ready. Select a file and click Send.", Color.Gray);
        }

        // Event handler to pick a file to compress
        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select a file to compress";
                dialog.Filter = "All Files (*.*)|*.*";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedFilePath = dialog.FileName;
                    _txtFilePath.Text = _selectedFilePath;

                    FileInfo fileInformation = new FileInfo(_selectedFilePath);
                    _lblFileInfo.Text = $"File: {fileInformation.Name}   " +
                                        $"Size: {FormatBytes(fileInformation.Length)}   " +
                                        $"Modified: {fileInformation.LastWriteTime:yyyy-MM-dd HH:mm}";

                    // Auto-fill save destination with a default .gz extension
                    string defaultSaveLocation = _selectedFilePath + ".gz";
                    _savePath = defaultSaveLocation;
                    _txtSavePath.Text = defaultSaveLocation;

                    UpdateSendButton();
                    AppendLog($"File selected: {fileInformation.Name} ({FormatBytes(fileInformation.Length)})", Color.FromArgb(180, 220, 255));
                }
            }
        }

        // Event handler to manually choose the destination save path
        private void BtnSaveBrowse_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Save compressed file as …";
                dialog.Filter = "GZip Files (*.gz)|*.gz|All Files (*.*)|*.*";
                dialog.DefaultExt = "gz";

                if (!string.IsNullOrEmpty(_selectedFilePath))
                {
                    dialog.FileName = Path.GetFileName(_selectedFilePath) + ".gz";
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _savePath = dialog.FileName;
                    _txtSavePath.Text = _savePath;
                    UpdateSendButton();
                }
            }
        }

        // Event handler to test server connectivity
        private void BtnConnect_Click(object sender, EventArgs e)
        {
            string ipAddress = _txtServer.Text.Trim();
            int portNumber = (int)_numPort.Value;

            _btnConnect.Enabled = false;
            AppendLog($"Testing connection to {ipAddress}:{portNumber} …", Color.Gray);

            // Run connection on a clean background thread to stop the UI form from locking up
            Thread testThread = new Thread(new ThreadStart(delegate
            {
                bool connectionSuccessful = false;
                string errorMessage = "";

                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.Connect(ipAddress, portNumber);
                        connectionSuccessful = true;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }

                // Push results back onto the UI thread safely
                this.Invoke((MethodInvoker)delegate
                {
                    _btnConnect.Enabled = true;
                    if (connectionSuccessful)
                    {
                        SetConnectionStatus(true);
                        AppendLog($"Connection successful: {ipAddress}:{portNumber}", Color.LightGreen);
                    }
                    else
                    {
                        SetConnectionStatus(false);
                        AppendLog($"Connection failed: {errorMessage}", Color.Tomato);
                    }
                });
            }));

            testThread.IsBackground = true;
            testThread.Start();
        }

        // Event handler when clicking 'Send File & Compress'
        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (_isSending) return;

            string targetFile = _selectedFilePath;
            string destinationFile = _savePath;
            string ipAddress = _txtServer.Text.Trim();
            int portNumber = (int)_numPort.Value;

            _isSending = true;
            SetSendButtonState(false, "Sending …");
            SetProgress(0, "Connecting …");

            // Spin up processing on a dedicated background execution path
            Thread workThread = new Thread(new ThreadStart(delegate
            {
                DoSendFile(targetFile, destinationFile, ipAddress, portNumber);
            }));

            workThread.IsBackground = true;
            workThread.Name = "NetworkSendWorker";
            workThread.Start();
        }

        // Core socket transmission engine (runs in background)
        private void DoSendFile(string filePath, string savePath, string ip, int port)
        {
            TcpClient networkClient = null;
            NetworkStream networkStream = null;

            try
            {
                // Step 1: Read input file locally
                Log($"Reading file: {Path.GetFileName(filePath)} …");
                byte[] fileBytes = File.ReadAllBytes(filePath);
                long originalSizeHeader = fileBytes.Length;
                Log($"File size: {FormatBytes(originalSizeHeader)}");
                SetProgress(5, "Read file …");

                // Step 2: Establish raw network connection
                Log($"Connecting to {ip}:{port} …");
                networkClient = new TcpClient();
                networkClient.Connect(ip, port);
                networkStream = networkClient.GetStream();
                Log($"Connected to server.", Color.LightGreen);
                SetProgress(10, "Connected …");
                SetConnectionStatus(true);

                // Step 3: Write out transmission length packet prefix (Big Endian)
                WriteInt64BigEndian(networkStream, originalSizeHeader);
                Log($"Sent file size header: {originalSizeHeader:N0} bytes");
                SetProgress(15, "Sent size header …");

                // Step 4: Stream the actual contents across the connection in smaller buffers
                const int bufferBlockSize = 65536; // 64 KB block
                long totalBytesSentSoFar = 0;
                Stopwatch transferTimer = Stopwatch.StartNew();

                for (int offset = 0; offset < fileBytes.Length; offset += bufferBlockSize)
                {
                    int currentChunkSize = Math.Min(bufferBlockSize, fileBytes.Length - offset);
                    networkStream.Write(fileBytes, offset, currentChunkSize);
                    totalBytesSentSoFar += currentChunkSize;

                    // Update live UI progress status calculation
                    int mathematicalPercentage = (int)(15 + 50.0 * totalBytesSentSoFar / originalSizeHeader);
                    SetProgress(mathematicalPercentage, $"Sending … {FormatBytes(totalBytesSentSoFar)} / {FormatBytes(originalSizeHeader)}");
                }
                networkStream.Flush();
                transferTimer.Stop();

                Log($"File sent in {transferTimer.Elapsed.TotalSeconds:F2}s");
                SetProgress(65, "Waiting for compressed file …");

                // Step 5: Receive compressed package volume size back from server
                long compressedSizeHeader = ReadInt64BigEndian(networkStream);
                Log($"Compressed size: {FormatBytes(compressedSizeHeader)}");
                SetProgress(70, "Receiving compressed file …");

                // Step 6: Pull processing block payload details back through network stream
                byte[] incomingCompressedDataBuffer = new byte[compressedSizeHeader];
                long totalBytesReceivedSoFar = 0;
                transferTimer.Restart();

                while (totalBytesReceivedSoFar < compressedSizeHeader)
                {
                    int readingTargetSize = (int)Math.Min(bufferBlockSize, compressedSizeHeader - totalBytesReceivedSoFar);
                    int actualReadBytesCount = networkStream.Read(incomingCompressedDataBuffer, (int)totalBytesReceivedSoFar, readingTargetSize);
                    
                    if (actualReadBytesCount == 0)
                    {
                        throw new EndOfStreamException("Server dropped connection mid-stream unexpectedly");
                    }
                    totalBytesReceivedSoFar += actualReadBytesCount;

                    int progressPercentage = (int)(70 + 25.0 * totalBytesReceivedSoFar / compressedSizeHeader);
                    SetProgress(progressPercentage, $"Receiving … {FormatBytes(totalBytesReceivedSoFar)} / {FormatBytes(compressedSizeHeader)}");
                }
                transferTimer.Stop();

                Log($"Received {FormatBytes(compressedSizeHeader)} in {transferTimer.Elapsed.TotalSeconds:F2}s");
                SetProgress(95, "Saving file …");

                // Step 7: Push the fully collected byte buffer chunk output onto local filesystem storage disk
                File.WriteAllBytes(savePath, incomingCompressedDataBuffer);

                // Calculations summary data assembly
                double shrinkingRatio = 100.0 * (1.0 - (double)compressedSizeHeader / originalSizeHeader);
                Log($"Done!  {FormatBytes(originalSizeHeader)} → {FormatBytes(compressedSizeHeader)} ({shrinkingRatio:F1}% smaller)", Color.LightGreen);
                Log($"Saved to: {savePath}", Color.FromArgb(180, 220, 180));
                SetProgress(100, $"Done!  {shrinkingRatio:F1}% reduction");

                // Informative complete notice confirmation notification dispatch
                this.Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show($"Compression complete!\n\n" +
                                    $"Original size  : {FormatBytes(originalSizeHeader)}\n" +
                                    $"Compressed size: {FormatBytes(compressedSizeHeader)}\n" +
                                    $"Reduction      : {shrinkingRatio:F1}%\n\n" +
                                    $"Saved to:\n{savePath}", 
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            catch (SocketException ex)
            {
                Log($"Socket error: {ex.Message}", Color.Tomato);
                SetProgress(0, "Error");
                ShowError($"Could not connect to server at {ip}:{port}\n\n{ex.Message}");
                SetConnectionStatus(false);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}", Color.Tomato);
                SetProgress(0, "Error");
                ShowError(ex.Message);
            }
            finally
            {
                // Clean close files and connectivity components safely
                if (networkStream != null) networkStream.Close();
                if (networkClient != null) networkClient.Close();

                _isSending = false;
                SetSendButtonState(true, "⬆  Send File & Compress");
            }
        }

        // Helper event handler to empty log window text content display block layout
        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            _rtbLog.Clear();
        }

        // Endian Helpers to parse cross-platform binary representations smoothly
        private static void WriteInt64BigEndian(Stream networkStreamStream, long rawValue)
        {
            byte[] processingBuffer = BitConverter.GetBytes(rawValue);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(processingBuffer);
            }
            networkStreamStream.Write(processingBuffer, 0, 8);
        }

        private static long ReadInt64BigEndian(Stream networkStreamStream)
        {
            byte[] localHeaderBuffer = new byte[8];
            int processingTotalBytesReadCount = 0;
            
            while (processingTotalBytesReadCount < 8)
            {
                int singleCycleReadResult = networkStreamStream.Read(localHeaderBuffer, processingTotalBytesReadCount, 8 - processingTotalBytesReadCount);
                if (singleCycleReadResult == 0)
                {
                    throw new EndOfStreamException("Connection terminated while trying to process sizes");
                }
                processingTotalBytesReadCount += singleCycleReadResult;
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(localHeaderBuffer);
            }
            return BitConverter.ToInt64(localHeaderBuffer, 0);
        }

        // Cross-Thread Safe User Interface Element Updating Invokers
        private void Log(string messageText, Color? logMessageHighlightColor = null)
        {
            AppendLog(messageText, logMessageHighlightColor ?? Color.FromArgb(180, 220, 255));
        }

        private void AppendLog(string messageText, Color textRenderColor)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { AppendLog(messageText, textRenderColor); });
                return;
            }
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor = Color.FromArgb(100, 130, 100);
            _rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}]  ");
            _rtbLog.SelectionColor = textRenderColor;
            _rtbLog.AppendText(messageText + "\n");
            _rtbLog.ScrollToCaret();
        }

        private void SetProgress(int value, string label)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { SetProgress(value, label); });
                return;
            }
            _progressBar.Value = Math.Max(0, Math.Min(100, value));
            _lblProgressText.Text = label;
        }

        private void SetConnectionStatus(bool connected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { SetConnectionStatus(connected); });
                return;
            }
            if (connected)
            {
                _lblConnStatus.Text = $"Connected to {_txtServer.Text}:{_numPort.Value}";
                _lblConnStatus.ForeColor = Color.FromArgb(40, 167, 69);
            }
            else
            {
                _lblConnStatus.Text = "Not connected";
                _lblConnStatus.ForeColor = Color.FromArgb(150, 150, 150);
            }
        }

        private void SetSendButtonState(bool enabled, string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { SetSendButtonState(enabled, text); });
                return;
            }
            _btnSend.Enabled = enabled;
            _btnSend.Text = text;
            _btnSend.BackColor = enabled ? Color.FromArgb(13, 27, 42) : Color.FromArgb(80, 90, 100);
        }

        private void ShowError(string errorMessageText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { ShowError(errorMessageText); });
                return;
            }
            MessageBox.Show(errorMessageText, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void UpdateSendButton()
        {
            _btnSend.Enabled = !string.IsNullOrEmpty(_selectedFilePath) && !string.IsNullOrEmpty(_savePath);
        }

        private static string FormatBytes(long rawBytesCount)
        {
            if (rawBytesCount < 1024) return $"{rawBytesCount} B";
            if (rawBytesCount < 1024 * 1024) return $"{rawBytesCount / 1024.0:F1} KB";
            return $"{rawBytesCount / 1024.0 / 1024.0:F2} MB";
        }
    }
}