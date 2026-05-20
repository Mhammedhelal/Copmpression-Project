using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CompressionServer
{
    //  MULTI-THREADED FILE COMPRESSION SERVER  –  IT432 Network Programming
    //
    //  Protocol (binary, big-endian):
    //    CLIENT => SERVER :
    //        [8 bytes]  original file size  (Int64, big-endian)
    //        [N bytes]  raw file bytes
    //    SERVER => CLIENT :
    //        [8 bytes]  compressed file size (Int64, big-endian)
    //        [M bytes]  gzip-compressed bytes
    //
    //  Architecture (mirrors ThreadedTcpSrvr from course):
    //    • Main thread   : TcpListener.AcceptTcpClient() loop
    //    • Per-client    : ConnectionThread.HandleConnection() on its own Thread
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "IT432 – Compression Server";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(" IT432 – Multi-Threaded Compression Server");
            Console.ResetColor();

            ThreadedCompressionServer server = new ThreadedCompressionServer();
            server.Start();
        }
    }
}
