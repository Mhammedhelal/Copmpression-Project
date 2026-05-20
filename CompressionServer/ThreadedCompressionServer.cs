using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CompressionServer
{
    // ThreadedCompressionServer
    //
    // Mirrors the ThreadedTcpSrvr pattern from the course:
    //   • TcpListener on a well-known port
    //   • Loops forever: for every pending connection, spawn a ConnectionThread
    //   • Each ConnectionThread runs HandleConnection() independently
    class ThreadedCompressionServer
    {
        private const int PORT = 9050;
        private const int BACKLOG = 10;

        private TcpListener _listener;

        public void Start()
        {
            // Set up the listener on any network interface card using our port
            _listener = new TcpListener(IPAddress.Any, PORT);
            _listener.Start(BACKLOG);

            Log("Server started.  Listening on port " + PORT + " ...", ConsoleColor.Green);
            Log("Waiting for clients ...\n", ConsoleColor.Gray);

            // Main loop to accept clients forever
            while (true)
            {
                // This blocks execution until a client actually connects
                TcpClient client = _listener.AcceptTcpClient();

                // Create a separate handler class instance to manage this exact client connection
                ConnectionThread handler = new ConnectionThread(client);

                // Create and spin up a raw thread for the client just like the course manual shows
                ThreadStart threadDelegate = new ThreadStart(handler.HandleConnection);
                Thread workerThread = new Thread(threadDelegate);

                // Start running the thread background task
                workerThread.Start();
            }
        }

        // Object used to make sure logs don't write over each other if lines print at the exact same time
        internal static readonly object ConsoleLock = new object();

        internal static void Log(string message)
        {
            // Call our main helper method passing White as the default color choice
            Log(message, ConsoleColor.White);
        }

        internal static void Log(string message, ConsoleColor color)
        {
            // Lock the console resources so output looks organized and readable
            lock (ConsoleLock)
            {
                // Set the text color
                Console.ForegroundColor = color;

                // Pull the current system time components out explicitly
                DateTime current = DateTime.Now;
                string timeString = current.Hour.ToString("D2") + ":" +
                                    current.Minute.ToString("D2") + ":" +
                                    current.Second.ToString("D2");

                // Print the final string using classic concatenation
                Console.WriteLine("[" + timeString + "]  " + message);

                // Always reset color back to normal default settings
                Console.ResetColor();
            }
        }
    }
}