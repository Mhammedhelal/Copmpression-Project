# IT432 – Network Programming
## Multi-Threaded File Compression Server + WinForms Client
### مشروع: خادم ضغط الملفات متعدد الخيوط + واجهة رسومية WinForms

---

## Project Structure / هيكل المشروع

```
CompressionApp.sln
│
├── CompressionServer/              ← Console Application (Server)
│   ├── Program.cs                  ← Entry point
│   ├── ThreadedCompressionServer.cs ← TcpListener + thread spawning
│   └── ConnectionThread.cs         ← Per-client handler thread
│
└── CompressionClient/              ← WinForms Application (Client)
    ├── Program.cs                  ← STAThread entry point
    └── MainForm.cs                 ← Full GUI + networking logic
```

---

## Architecture / المعمارية

### Server (Multi-Threaded Pattern from Course)

```
ThreadedCompressionServer  (Main Thread)
│
│   TcpListener.AcceptTcpClient()  ← blocks until client connects
│       │
│       └──► ConnectionThread  (New Thread per client)
│                 │
│                 ├─ ReadExactly(8 bytes)      → receive file size
│                 ├─ ReadExactly(N bytes)      → receive raw file
│                 ├─ GZipStream(MemoryStream)  → compress in memory
│                 ├─ WriteInt64BigEndian(8 B)  → send compressed size
│                 └─ ns.Write(M bytes)         → send compressed file
│
│   (loops back to AcceptTcpClient for next client)
```

This mirrors the **ThreadedTcpSrvr** pattern from the course:
```csharp
// Course pattern (lecture 6):
ConnectionThread newconnection = new ConnectionThread();
Thread newthread = new Thread(new ThreadStart(newconnection.HandleConnection));
newthread.Start();
```

---

## Communication Protocol / بروتوكول الاتصال

```
CLIENT → SERVER                     SERVER → CLIENT
┌──────────────────────┐            ┌──────────────────────┐
│  8 bytes             │            │  8 bytes             │
│  Int64 big-endian    │            │  Int64 big-endian    │
│  (original file size)│            │  (compressed size)   │
├──────────────────────┤            ├──────────────────────┤
│  N bytes             │            │  M bytes             │
│  Raw file data       │            │  GZip compressed     │
└──────────────────────┘            └──────────────────────┘
```

**Why big-endian?**  
Network byte order is big-endian (standard). C# `BitConverter` is little-endian 
on x86, so bytes are reversed before sending/after receiving.

**Why 8 bytes (Int64)?**  
Supports files up to 9.2 exabytes — future-proof for any file size.

---

## Classes / الفئات

### `ThreadedCompressionServer`
| Member | Role |
|--------|------|
| `Start()` | Creates `TcpListener`, enters accept loop |
| `Log(msg, color)` | Thread-safe console output with timestamp |

### `ConnectionThread`
| Member | Role |
|--------|------|
| `_activeConnections` | `static int` — shared counter, all threads |
| `HandleConnection()` | Full receive → compress → send workflow |
| `CompressGzip(byte[])` | Wraps `GZipStream` over `MemoryStream` |
| `ReadExactly(stream, n)` | Loop-reads until exactly n bytes received |
| `ReadInt64BigEndian()` | Reads 8-byte big-endian length prefix |
| `WriteInt64BigEndian()` | Writes 8-byte big-endian length prefix |

### `MainForm` (WinForms Client)
| Member | Role |
|--------|------|
| `BtnBrowse_Click` | OpenFileDialog to select source file |
| `BtnSaveBrowse_Click` | SaveFileDialog for output .gz path |
| `BtnConnect_Click` | Background thread: test TCP connection |
| `BtnSend_Click` | Spawns `sendThread` (keeps UI responsive) |
| `DoSendFile(...)` | Runs on background thread: full protocol |
| `SetProgress(int, string)` | Thread-safe `Invoke` to update ProgressBar |
| `AppendLog(string, Color)` | Thread-safe `Invoke` to write to RichTextBox |

---

## Key C# Concepts Used / المفاهيم المستخدمة

| Concept | Class / API | From Lecture |
|---------|------------|--------------|
| TCP Client-Server | `TcpListener`, `TcpClient`, `NetworkStream` | Lecture 3-4 |
| Multi-threading | `Thread`, `ThreadStart`, `Interlocked` | Lecture 6 |
| Stream I/O | `NetworkStream.Read/Write`, `MemoryStream` | Lecture 1 |
| GZip Compression | `GZipStream(CompressionLevel.Optimal)` | Lecture 8 |
| Binary protocol | `BitConverter` + byte reversal | Lecture 2 |
| WinForms threading | `this.Invoke(MethodInvoker)` | Lecture 6 |
| Background threads | `thread.IsBackground = true` | Lecture 7 |

---

## How to Run / طريقة التشغيل

### Requirements
- .NET 6.0 SDK or later
- Windows (for WinForms client)

### Step 1 – Build
```bash
cd CompressionApp
dotnet build CompressionApp.sln
```
Or open `CompressionApp.sln` in Visual Studio 2022 and press **Ctrl+Shift+B**.

### Step 2 – Run the Server
```bash
cd CompressionServer
dotnet run
```
Expected output:
```
[09:00:01]  Server started.  Listening on port 9050 ...
[09:00:01]  Waiting for clients ...
```

### Step 3 – Run the Client
```bash
cd CompressionClient
dotnet run
```
Or press **F5** in Visual Studio with `CompressionClient` set as startup project.

### Step 4 – Use the Client
1. Enter server IP (e.g. `127.0.0.1`) and port (`9050`)
2. Click **Browse** → select any file
3. Choose save location for the `.gz` output
4. Click **⬆ Send File & Compress**
5. Watch live progress bar and activity log
6. Result dialog shows compression ratio

---

## Testing Multiple Clients / اختبار عدة عملاء

Open multiple instances of the client (or run from different machines).  
The server log shows active connection count:

```
[09:05:01]  [+] Connected : 127.0.0.1:54321   |   Active: 1
[09:05:02]  [+] Connected : 127.0.0.1:54322   |   Active: 2
[09:05:02]  [+] Connected : 127.0.0.1:54323   |   Active: 3
    [127.0.0.1:54321] Compressed: 10,240,000 → 3,120,000 bytes  (69.5% reduction)
[09:05:04]  [-] Disconnected : 127.0.0.1:54321   |   Active: 2
```

---

## GZip Compression Detail / تفاصيل ضغط GZip

```csharp
using (MemoryStream ms = new MemoryStream())
{
    using (GZipStream gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
    {
        gz.Write(data, 0, data.Length);
    }   // Dispose flushes gzip footer
    return ms.ToArray();
}
```

`GZipStream` is listed in the course slides as a non-abstract subclass of `Stream`:
> *"System.IO.Compression.GZipStream – Provides stream access to compressed data"*

---

## Course Connection / الصلة بالمقرر

| Course Topic | Implementation |
|-------------|----------------|
| `ThreadedTcpSrvr` (Lecture 6) | `ThreadedCompressionServer` + `ConnectionThread` |
| `NetworkStream` (Lecture 3-4) | `client.GetStream()` for all I/O |
| `GZipStream` (Lecture 1) | `CompressGzip()` in `ConnectionThread` |
| `BinaryReader/Writer` (Lecture 2) | `ReadInt64BigEndian/WriteInt64BigEndian` |
| `MemoryStream` (Lecture 1) | Compression staging buffer |
| `Thread.IsBackground` (Lecture 7) | Server client threads + client send thread |
| `Invoke` for UI (Lecture 6) | All `SetProgress`, `AppendLog`, `ShowError` |
