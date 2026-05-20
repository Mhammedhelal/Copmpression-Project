using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CompressionServer
{
    // ConnectionThread
    // One instance per accepted client. Runs on its own thread.
    class ConnectionThread
    {
        // Shared connection counter and a lock object to prevent race conditions safely
        private static int _activeConnections = 0;
        private static readonly object _counterLock = new object();

        // The already-accepted client for this thread 
        private readonly TcpClient _client;
        private readonly string _clientAddress;

        public ConnectionThread(TcpClient client)
        {
            _client = client;
            // Get the IP address and port as a string to print out to logs
            IPEndPoint remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            _clientAddress = remoteEndPoint.ToString();
        }

        // HandleConnection – entry point called by the spawned Thread
        public void HandleConnection()
        {
            // Thread-safe way to increment a shared counter using a standard lock
            int currentCount = 0;
            lock (_counterLock)
            {
                _activeConnections = _activeConnections + 1;
                currentCount = _activeConnections;
            }

            ThreadedCompressionServer.Log(
                "[+] Connected : " + _clientAddress + "   |   Active: " + currentCount,
                ConsoleColor.Green);

            NetworkStream networkStream = null;
            try
            {
                // Get the stream for reading and writing data
                networkStream = _client.GetStream();

                // STEP 1 : Read the original file size (8 bytes, big-endian) 
                long originalSize = ReadInt64BigEndian(networkStream);

                ThreadedCompressionServer.Log(
                    "    [" + _clientAddress + "] Expecting " + originalSize.ToString("N0") + " bytes",
                    ConsoleColor.Yellow);

                // STEP 2 : Read exactly originalSize bytes of file data 
                Stopwatch timer = new Stopwatch();
                timer.Start();
                byte[] fileData = ReadExactly(networkStream, originalSize);
                timer.Stop();

                ThreadedCompressionServer.Log(
                    "    [" + _clientAddress + "] Received " + fileData.Length.ToString("N0") + " bytes " +
                    "in " + timer.Elapsed.TotalSeconds.ToString("F3") + "s",
                    ConsoleColor.Yellow);

                // STEP 3 : Compress with GZip 
                timer.Restart();
                byte[] compressedData = CompressGzip(fileData);
                timer.Stop();

                double reductionRatio = 0.0;
                if (originalSize > 0)
                {
                    double ratioFraction = (double)compressedData.Length / (double)originalSize;
                    reductionRatio = 100.0 * (1.0 - ratioFraction);
                }
                else
                {
                    reductionRatio = 0.0;
                }

                ThreadedCompressionServer.Log(
                    "    [" + _clientAddress + "] Compressed : " +
                    originalSize.ToString("N0") + " --> " + compressedData.Length.ToString("N0") + " bytes  " +
                    "(" + reductionRatio.ToString("F1") + "% reduction)  [" + timer.Elapsed.TotalMilliseconds.ToString("F0") + " ms]",
                    ConsoleColor.Cyan);

                // STEP 4 : Send compressed size (8 bytes) then data back to client
                WriteInt64BigEndian(networkStream, compressedData.Length);
                networkStream.Write(compressedData, 0, compressedData.Length);
                networkStream.Flush();

                ThreadedCompressionServer.Log(
                    "    [" + _clientAddress + "] Sent " + compressedData.Length.ToString("N0") + " compressed bytes  ",
                    ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                // Simple string concatenation for error reporting
                ThreadedCompressionServer.Log(
                    "[!] " + _clientAddress + " – " + ex.GetType().Name + ": " + ex.Message,
                    ConsoleColor.Red);
            }
            finally
            {
                if (networkStream != null)
                {
                    networkStream.Close();
                }
                if (_client != null)
                {
                    _client.Close();
                }

                // Decrement the active connections inside our safe lock block
                int remainingConnections = 0;
                lock (_counterLock)
                {
                    _activeConnections = _activeConnections - 1;
                    remainingConnections = _activeConnections;
                }

                ThreadedCompressionServer.Log(
                    "[-] Disconnected : " + _clientAddress + "   |   Active: " + remainingConnections,
                    ConsoleColor.DarkGray);
            }
        }

        // GZip Compression using standard streams
        private static byte[] CompressGzip(byte[] originalData)
        {
            MemoryStream memoryStream = new MemoryStream();
            
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(originalData, 0, originalData.Length);
                gzipStream.Flush();
            }

            // Convert the underlying memory stream stream to an array
            byte[] compressedBytes = memoryStream.ToArray();
            memoryStream.Close();
            
            return compressedBytes;
        }

        // Helper to handle Big-Endian Network Byte Order for 64-bit integers
        private static long ReadInt64BigEndian(Stream clientStream)
        {
            byte[] byteBuffer = ReadExactly(clientStream, 8);
            
            // If the local computer is Little Endian (like Windows Intel/AMD), we must reverse bytes
            if (BitConverter.IsLittleEndian == true)
            {
                Array.Reverse(byteBuffer);
            }
            
            long value = BitConverter.ToInt64(byteBuffer, 0);
            return value;
        }

        private static void WriteInt64BigEndian(Stream clientStream, long value)
        {
            byte[] byteBuffer = BitConverter.GetBytes(value);
            
            // If the local computer is Little Endian, change it to Big Endian for the network
            if (BitConverter.IsLittleEndian == true)
            {
                Array.Reverse(byteBuffer);
            }
            
            clientStream.Write(byteBuffer, 0, 8);
        }

        // ReadExactly loop ensuring network fragmentation doesn't drop bytes
        private static byte[] ReadExactly(Stream clientStream, long count)
        {
            int chunkSize = 65536; // 64 KB chunk size buffer boundary
            byte[] trackingBuffer = new byte[count];
            long totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                // Figure out how much space is remaining to be filled
                long remainingBytes = count - totalBytesRead;
                
                // Do not read more than our default chunk allows
                int bytesToReadNext = (int)Math.Min(chunkSize, remainingBytes);
                
                int bytesActuallyRead = clientStream.Read(trackingBuffer, (int)totalBytesRead, bytesToReadNext);
                
                // If network socket returns 0, the client disconnected unexpectedly
                if (bytesActuallyRead == 0)
                {
                    throw new EndOfStreamException("Stream closed after " + totalBytesRead + " of " + count + " bytes");
                }
                
                totalBytesRead = totalBytesRead + bytesActuallyRead;
            }
            
            return trackingBuffer;
        }
    }
}