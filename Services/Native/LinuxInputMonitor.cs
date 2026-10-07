using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoClicker.Services.Native;

public class LinuxInputMonitor : IGlobalInputHook
{
    private readonly Dictionary<string, int> _deviceMap = new();
    private readonly HashSet<string> _ignoredPaths = new();
    private readonly HashSet<(int fd, int vkCode)> _downKeys = new();
    private readonly object _lock = new();

    private Thread? _workerThread;
    private volatile bool _isRunning = false;
    private bool _isHotkeyDown = false;
    private long _recordingStartedTimestamp = 0;

    private RecordingSlot _currentRecordingSlot = RecordingSlot.None;

    public int HotkeyVkCode { get; set; } = 0x75; // F6 default
    public bool BlockHotkey { get; set; } = false;
    public bool StopOnEscape { get; set; } = true;

    public RecordingSlot CurrentRecordingSlot
    {
        get => _currentRecordingSlot;
        set
        {
            _currentRecordingSlot = value;
            if (value != RecordingSlot.None)
            {
                _recordingStartedTimestamp = Stopwatch.GetTimestamp();
            }
        }
    }

    public bool IsRecording
    {
        get => _currentRecordingSlot != RecordingSlot.None;
        set => CurrentRecordingSlot = value ? RecordingSlot.Hotkey : RecordingSlot.None;
    }

    public event Action<int>? HotkeyRecorded;
    public event Action? RecordingCancelled;
    public event Action<RecordingSlot, int>? SlotRecorded;
    public event Action<RecordingSlot>? SlotCancelled;
    public event Action? HotkeyDown;
    public event Action? HotkeyUp;
    public event Action? EmergencyStopTriggered;

    public LinuxInputMonitor()
    {
        StartMonitorThread();
    }

    public void ResetKeyState()
    {
        lock (_lock)
        {
            _isHotkeyDown = false;
            _downKeys.Clear();
        }
    }

    private void StartMonitorThread()
    {
        _isRunning = true;
        _workerThread = new Thread(MonitorLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "LinuxInputMonitorThread"
        };
        _workerThread.Start();
    }

    private void ScanInputDevices()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists("/dev/input")) return;

                var eventFiles = Directory.GetFiles("/dev/input", "event*");
                var currentSet = new HashSet<string>(eventFiles);

                // 1. Remove disconnected devices without affecting active ones
                var removedPaths = new List<string>();
                foreach (var kvp in _deviceMap)
                {
                    if (!currentSet.Contains(kvp.Key))
                    {
                        try { LinuxNative.Close(kvp.Value); } catch { }
                        removedPaths.Add(kvp.Key);
                    }
                }
                foreach (var path in removedPaths)
                {
                    _deviceMap.Remove(path);
                }

                _ignoredPaths.RemoveWhere(p => !currentSet.Contains(p));

                // 2. Discover new devices (existing devices remain untouched and open)
                foreach (var path in eventFiles)
                {
                    if (_deviceMap.ContainsKey(path) || _ignoredPaths.Contains(path))
                    {
                        continue;
                    }

                    try
                    {
                        int fd = LinuxNative.Open(path, LinuxNative.O_RDONLY | LinuxNative.O_NONBLOCK);
                        if (fd >= 0)
                        {
                            // Filter out our own virtual device
                            byte[] nameBuf = new byte[256];
                            int nameLen = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGNAME_256, Marshal.UnsafeAddrOfPinnedArrayElement(nameBuf, 0));
                            if (nameLen > 0)
                            {
                                string name = System.Text.Encoding.ASCII.GetString(nameBuf, 0, nameLen).TrimEnd('\0');
                                if (name.Contains("Macro BBQ", StringComparison.OrdinalIgnoreCase))
                                {
                                    LinuxNative.Close(fd);
                                    _ignoredPaths.Add(path);
                                    continue;
                                }
                            }

                            // Filter: we only care about devices capable of EV_KEY
                            byte[] evBits = new byte[8];
                            int res = LinuxNative.Ioctl(fd, 0x80084520 /* EVIOCGBIT(0, 8) */, Marshal.UnsafeAddrOfPinnedArrayElement(evBits, 0));
                            bool hasKey = res >= 0 && (evBits[0] & (1 << LinuxNative.EV_KEY)) != 0;

                            if (hasKey)
                            {
                                _deviceMap[path] = fd;
                            }
                            else
                            {
                                LinuxNative.Close(fd);
                                _ignoredPaths.Add(path);
                            }
                        }
                        else
                        {
                            _ignoredPaths.Add(path);
                        }
                    }
                    catch
                    {
                        _ignoredPaths.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Macro BBQ] Error scanning /dev/input devices: {ex.Message}");
            }
        }
    }

    private void CloseAllDevices()
    {
        lock (_lock)
        {
            foreach (var kvp in _deviceMap)
            {
                try { LinuxNative.Close(kvp.Value); } catch { }
            }
            _deviceMap.Clear();
            _ignoredPaths.Clear();
            _downKeys.Clear();
        }
    }

    private void MonitorLoop()
    {
        ScanInputDevices();
        int eventSize = Marshal.SizeOf<LinuxNative.input_event>();
        IntPtr buffer = Marshal.AllocHGlobal(eventSize * 16);
        DateTime lastDeviceScanTime = DateTime.UtcNow;
        DateTime lastPhysicalCheckTime = DateTime.UtcNow;

        try
        {
            while (_isRunning)
            {
                DateTime now = DateTime.UtcNow;

                // Re-check for newly plugged-in devices every 3 seconds (healthy open devices remain open!)
                if ((now - lastDeviceScanTime).TotalSeconds > 3)
                {
                    lastDeviceScanTime = now;
                    ScanInputDevices();
                }

                // Active Hold Watchdog:
                // If hotkey is currently DOWN, verify physical state every 60ms to guarantee zero stuck holds
                if (_isHotkeyDown && (now - lastPhysicalCheckTime).TotalMilliseconds > 60)
                {
                    lastPhysicalCheckTime = now;
                    if (!IsKeyPhysicallyDown(HotkeyVkCode))
                    {
                        lock (_lock)
                        {
                            _downKeys.Clear();
                            _isHotkeyDown = false;
                        }
                        HotkeyUp?.Invoke();
                    }
                }

                int[] fds;
                lock (_lock)
                {
                    fds = _deviceMap.Values.ToArray();
                }

                if (fds.Length == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }

                var pollFds = new LinuxNative.pollfd[fds.Length];
                for (int i = 0; i < fds.Length; i++)
                {
                    pollFds[i] = new LinuxNative.pollfd
                    {
                        fd = fds[i],
                        events = LinuxNative.POLLIN,
                        revents = 0
                    };
                }

                int pollRes = LinuxNative.Poll(pollFds, (uint)pollFds.Length, 50);
                if (pollRes <= 0 || !_isRunning)
                {
                    continue;
                }

                for (int i = 0; i < pollFds.Length; i++)
                {
                    if ((pollFds[i].revents & LinuxNative.POLLIN) != 0)
                    {
                        int fd = pollFds[i].fd;
                        IntPtr bytesRead = LinuxNative.Read(fd, buffer, (IntPtr)(eventSize * 16));
                        int count = (int)bytesRead / eventSize;

                        for (int e = 0; e < count; e++)
                        {
                            IntPtr eventPtr = IntPtr.Add(buffer, e * eventSize);
                            var ev = Marshal.PtrToStructure<LinuxNative.input_event>(eventPtr);

                            if (ev.type == LinuxNative.EV_KEY)
                            {
                                ProcessKeyEvent(fd, ev.code, ev.value);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            CloseAllDevices();
        }
    }

    private void ProcessKeyEvent(int fd, ushort code, int value)
    {
        // value: 0 = Up, 1 = Down, 2 = Autorepeat (ignore autorepeat for down trigger)
        if (value == 2) return;

        bool isDown = value == 1;
        int vkCode = EvdevToVkCode(code);
        if (vkCode == 0) return;

        // Emergency Stop on Escape
        if (code == LinuxNative.KEY_ESC && isDown && StopOnEscape && !IsRecording)
        {
            EmergencyStopTriggered?.Invoke();
            return;
        }

        // Recording active
        if (IsRecording && isDown)
        {
            // Ignore any key/click during the first 120ms of entering recording mode
            // (e.g. the initial mouse down that clicked the UI button)
            if (Stopwatch.GetElapsedTime(_recordingStartedTimestamp).TotalMilliseconds < 120)
            {
                return;
            }

            var slot = _currentRecordingSlot;
            _currentRecordingSlot = RecordingSlot.None;

            if (code == LinuxNative.KEY_ESC)
            {
                RecordingCancelled?.Invoke();
                SlotCancelled?.Invoke(slot);
                return;
            }

            // Left Click safety check when recording hotkey
            if (slot == RecordingSlot.Hotkey && vkCode == 0x01 /* VK_LBUTTON */)
            {
                RecordingCancelled?.Invoke();
                SlotCancelled?.Invoke(slot);
                return;
            }

            if (slot == RecordingSlot.Hotkey)
            {
                HotkeyVkCode = vkCode;
                HotkeyRecorded?.Invoke(vkCode);
            }

            SlotRecorded?.Invoke(slot, vkCode);
            return;
        }

        // Regular trigger monitoring
        if (!IsRecording && vkCode == HotkeyVkCode)
        {
            if (isDown)
            {
                bool wasDown;
                lock (_lock)
                {
                    wasDown = System.Linq.Enumerable.Any(_downKeys, k => k.vkCode == HotkeyVkCode);
                    _downKeys.Add((fd, vkCode));
                }

                if (!wasDown && !_isHotkeyDown)
                {
                    _isHotkeyDown = true;
                    HotkeyDown?.Invoke();
                }
            }
            else
            {
                bool stillDown;
                lock (_lock)
                {
                    _downKeys.Remove((fd, vkCode));
                    stillDown = System.Linq.Enumerable.Any(_downKeys, k => k.vkCode == HotkeyVkCode);
                }

                if (!stillDown && _isHotkeyDown)
                {
                    _isHotkeyDown = false;
                    HotkeyUp?.Invoke();
                }
            }
        }
    }

    public bool IsKeyPhysicallyDown(int vkCode)
    {
        var codes = GetPossibleEvdevCodes(vkCode);
        if (codes.Count == 0) return false;

        int[] fds;
        lock (_lock)
        {
            fds = _deviceMap.Values.ToArray();
        }

        byte[] keyBits = new byte[64];
        GCHandle handle = GCHandle.Alloc(keyBits, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            foreach (int fd in fds)
            {
                Array.Clear(keyBits, 0, keyBits.Length);
                int res = LinuxNative.Ioctl(fd, LinuxNative.EVIOCGKEY_64, ptr);
                if (res >= 0)
                {
                    foreach (ushort code in codes)
                    {
                        int byteIdx = code / 8;
                        int bitIdx = code % 8;
                        if (byteIdx < keyBits.Length)
                        {
                            if ((keyBits[byteIdx] & (1 << bitIdx)) != 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        finally
        {
            handle.Free();
        }

        return false;
    }

    private static List<ushort> GetPossibleEvdevCodes(int vkCode)
    {
        var list = new List<ushort>();
        switch (vkCode)
        {
            case 0x01: list.Add(LinuxNative.BTN_LEFT); break;
            case 0x02: list.Add(LinuxNative.BTN_RIGHT); break;
            case 0x04: list.Add(LinuxNative.BTN_MIDDLE); break;
            case 0x05: // VK_XBUTTON1 (M4)
                list.Add(LinuxNative.BTN_SIDE);
                list.Add(LinuxNative.BTN_BACK);
                break;
            case 0x06: // VK_XBUTTON2 (M5)
                list.Add(LinuxNative.BTN_EXTRA);
                list.Add(LinuxNative.BTN_FORWARD);
                break;
            default:
                if (LinuxUinputSimulator.TryMapVkToEvdev(vkCode, out _, out ushort code))
                {
                    list.Add(code);
                }
                break;
        }
        return list;
    }

    public static int EvdevToVkCode(ushort code)
    {
        // Mouse Buttons
        switch (code)
        {
            case LinuxNative.BTN_LEFT: return 0x01; // VK_LBUTTON
            case LinuxNative.BTN_RIGHT: return 0x02; // VK_RBUTTON
            case LinuxNative.BTN_MIDDLE: return 0x04; // VK_MBUTTON
            case LinuxNative.BTN_SIDE:
            case LinuxNative.BTN_BACK: return 0x05; // VK_XBUTTON1 (M4)
            case LinuxNative.BTN_EXTRA:
            case LinuxNative.BTN_FORWARD: return 0x06; // VK_XBUTTON2 (M5)
        }

        // Function keys F1 - F12
        if (code >= LinuxNative.KEY_F1 && code <= LinuxNative.KEY_F10)
        {
            return 0x70 + (code - LinuxNative.KEY_F1);
        }
        if (code == LinuxNative.KEY_F11) return 0x7A;
        if (code == LinuxNative.KEY_F12) return 0x7B;
        if (code >= LinuxNative.KEY_F13 && code <= LinuxNative.KEY_F24)
        {
            return 0x7C + (code - LinuxNative.KEY_F13);
        }

        // Digits 0-9
        if (code == LinuxNative.KEY_0) return 0x30;
        if (code >= LinuxNative.KEY_1 && code <= LinuxNative.KEY_9)
        {
            return 0x31 + (code - LinuxNative.KEY_1);
        }

        // Letters A-Z
        switch (code)
        {
            case LinuxNative.KEY_A: return 0x41;
            case LinuxNative.KEY_B: return 0x42;
            case LinuxNative.KEY_C: return 0x43;
            case LinuxNative.KEY_D: return 0x44;
            case LinuxNative.KEY_E: return 0x45;
            case LinuxNative.KEY_F: return 0x46;
            case LinuxNative.KEY_G: return 0x47;
            case LinuxNative.KEY_H: return 0x48;
            case LinuxNative.KEY_I: return 0x49;
            case LinuxNative.KEY_J: return 0x4A;
            case LinuxNative.KEY_K: return 0x4B;
            case LinuxNative.KEY_L: return 0x4C;
            case LinuxNative.KEY_M: return 0x4D;
            case LinuxNative.KEY_N: return 0x4E;
            case LinuxNative.KEY_O: return 0x4F;
            case LinuxNative.KEY_P: return 0x50;
            case LinuxNative.KEY_Q: return 0x51;
            case LinuxNative.KEY_R: return 0x52;
            case LinuxNative.KEY_S: return 0x53;
            case LinuxNative.KEY_T: return 0x54;
            case LinuxNative.KEY_U: return 0x55;
            case LinuxNative.KEY_V: return 0x56;
            case LinuxNative.KEY_W: return 0x57;
            case LinuxNative.KEY_X: return 0x58;
            case LinuxNative.KEY_Y: return 0x59;
            case LinuxNative.KEY_Z: return 0x5A;
        }

        // Keypad
        switch (code)
        {
            case LinuxNative.KEY_KP0: return 0x60;
            case LinuxNative.KEY_KP1: return 0x61;
            case LinuxNative.KEY_KP2: return 0x62;
            case LinuxNative.KEY_KP3: return 0x63;
            case LinuxNative.KEY_KP4: return 0x64;
            case LinuxNative.KEY_KP5: return 0x65;
            case LinuxNative.KEY_KP6: return 0x66;
            case LinuxNative.KEY_KP7: return 0x67;
            case LinuxNative.KEY_KP8: return 0x68;
            case LinuxNative.KEY_KP9: return 0x69;
            case LinuxNative.KEY_KPASTERISK: return 0x6A;
            case LinuxNative.KEY_KPPLUS: return 0x6B;
            case LinuxNative.KEY_KPMINUS: return 0x6D;
            case LinuxNative.KEY_KPDOT: return 0x6E;
            case LinuxNative.KEY_KPSLASH: return 0x6F;
            case LinuxNative.KEY_KPENTER: return 0x0D;
        }

        // Other common keys
        switch (code)
        {
            case LinuxNative.KEY_BACKSPACE: return 0x08;
            case LinuxNative.KEY_TAB: return 0x09;
            case LinuxNative.KEY_ENTER: return 0x0D;
            case LinuxNative.KEY_PAUSE: return 0x13;
            case LinuxNative.KEY_CAPSLOCK: return 0x14;
            case LinuxNative.KEY_ESC: return 0x1B;
            case LinuxNative.KEY_SPACE: return 0x20;
            case LinuxNative.KEY_PAGEUP: return 0x21;
            case LinuxNative.KEY_PAGEDOWN: return 0x22;
            case LinuxNative.KEY_END: return 0x23;
            case LinuxNative.KEY_HOME: return 0x24;
            case LinuxNative.KEY_LEFT: return 0x25;
            case LinuxNative.KEY_UP: return 0x26;
            case LinuxNative.KEY_RIGHT: return 0x27;
            case LinuxNative.KEY_DOWN: return 0x28;
            case LinuxNative.KEY_INSERT: return 0x2D;
            case LinuxNative.KEY_DELETE: return 0x2E;
            case LinuxNative.KEY_LEFTMETA: return 0x5B;
            case LinuxNative.KEY_RIGHTMETA: return 0x5C;
            case LinuxNative.KEY_NUMLOCK: return 0x90;
            case LinuxNative.KEY_SCROLLLOCK: return 0x91;
            case LinuxNative.KEY_LEFTSHIFT: return 0xA0;
            case LinuxNative.KEY_RIGHTSHIFT: return 0xA1;
            case LinuxNative.KEY_LEFTCTRL: return 0xA2;
            case LinuxNative.KEY_RIGHTCTRL: return 0xA3;
            case LinuxNative.KEY_LEFTALT: return 0xA4;
            case LinuxNative.KEY_RIGHTALT: return 0xA5;
            case LinuxNative.KEY_MUTE: return 0xAD;
            case LinuxNative.KEY_VOLUMEDOWN: return 0xAE;
            case LinuxNative.KEY_VOLUMEUP: return 0xAF;
            case LinuxNative.KEY_NEXTSONG: return 0xB0;
            case LinuxNative.KEY_PREVIOUSSONG: return 0xB1;
            case LinuxNative.KEY_STOPCD: return 0xB2;
            case LinuxNative.KEY_PLAYPAUSE: return 0xB3;
            case LinuxNative.KEY_SEMICOLON: return 0xBA;
            case LinuxNative.KEY_EQUAL: return 0xBB;
            case LinuxNative.KEY_COMMA: return 0xBC;
            case LinuxNative.KEY_MINUS: return 0xBD;
            case LinuxNative.KEY_DOT: return 0xBE;
            case LinuxNative.KEY_SLASH: return 0xBF;
            case LinuxNative.KEY_GRAVE: return 0xC0;
            case LinuxNative.KEY_LEFTBRACE: return 0xDB;
            case LinuxNative.KEY_BACKSLASH: return 0xDC;
            case LinuxNative.KEY_RIGHTBRACE: return 0xDD;
            case LinuxNative.KEY_APOSTROPHE: return 0xDE;
        }

        return 0;
    }

    public void Dispose()
    {
        _isRunning = false;
        try
        {
            _workerThread?.Join(500);
        }
        catch { }
        lock (_lock)
        {
            CloseAllDevices();
        }
        GC.SuppressFinalize(this);
    }
}
