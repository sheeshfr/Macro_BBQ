using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AutoClicker.Services.Native;

public static class LinuxSingleInstance
{
    private static Socket? _listenerSocket;
    private static string? _socketPath;
    private static Thread? _listenerThread;
    private static volatile bool _isRunning = false;

    public static bool TryAcquire(Action onWake)
    {
        try
        {
            string uid = Environment.GetEnvironmentVariable("UID") 
                ?? Environment.GetEnvironmentVariable("USER") 
                ?? "1000";
            _socketPath = Path.Combine(Path.GetTempPath(), $"macrobbq_{uid}.sock");

            // 1. Test if another instance is already running by attempting to connect
            try
            {
                using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                clientSocket.Connect(new UnixDomainSocketEndPoint(_socketPath));
                
                // Existing instance is active! Send SHOW command to bring window forward
                byte[] message = Encoding.UTF8.GetBytes("SHOW\n");
                clientSocket.Send(message);
                return false; // Another instance is running; this process should terminate
            }
            catch
            {
                // Socket does not exist or previous instance terminated abruptly
            }

            // 2. Clean up stale socket file if it exists
            try
            {
                if (File.Exists(_socketPath))
                {
                    File.Delete(_socketPath);
                }
            }
            catch { }

            // 3. Bind and listen for future wake signals
            _listenerSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listenerSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
            _listenerSocket.Listen(10);
            _isRunning = true;

            _listenerThread = new Thread(() => ListenLoop(onWake))
            {
                IsBackground = true,
                Name = "LinuxSingleInstanceListener"
            };
            _listenerThread.Start();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Macro BBQ] Linux single-instance setup fallback: {ex.Message}");
            return true;
        }
    }

    private static void ListenLoop(Action onWake)
    {
        byte[] buffer = new byte[64];
        while (_isRunning && _listenerSocket != null)
        {
            try
            {
                using var client = _listenerSocket.Accept();
                int read = client.Receive(buffer);
                if (read > 0)
                {
                    string text = Encoding.UTF8.GetString(buffer, 0, read);
                    if (text.Contains("SHOW"))
                    {
                        onWake();
                    }
                }
            }
            catch
            {
                break;
            }
        }
    }

    public static void Release()
    {
        _isRunning = false;
        try
        {
            _listenerSocket?.Close();
            _listenerSocket?.Dispose();
            _listenerSocket = null;
        }
        catch { }

        try
        {
            if (_socketPath != null && File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
        catch { }
    }
}
