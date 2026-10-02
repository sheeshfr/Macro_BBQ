using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutoClicker.Services.Native;

namespace AutoClicker.Services;

public enum RecordingSlot
{
    None,
    Hotkey,
    Primary,
    Modifier
}

public class GlobalKeyboardHook : IDisposable
{
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    private readonly Win32Api.LowLevelKeyboardProc _keyboardProc;
    private readonly Win32Api.LowLevelMouseProc _mouseProc;

    private bool _isHotkeyDown = false;

    private Thread? _hookThread;
    private uint _hookThreadId = 0;
    private volatile bool _isHookRunning = false;
    private readonly ManualResetEventSlim _hookStartedEvent = new(false);

    private Thread? _pollerThread;
    private volatile bool _isPollerRunning = false;

    private RecordingSlot _currentRecordingSlot = RecordingSlot.None;
    private readonly object _recordingLock = new();

    private volatile int _swallowNextKeyUpVkCode = 0;
    private volatile int _swallowNextMouseUpMsg = 0;
    private CancellationTokenSource? _swallowTimeoutCts;

    public int HotkeyVkCode { get; set; } = 0x75; // Default: F6

    public RecordingSlot CurrentRecordingSlot
    {
        get => _currentRecordingSlot;
        set
        {
            lock (_recordingLock)
            {
                _currentRecordingSlot = value;
                if (value != RecordingSlot.None)
                {
                    StartRecordingHooks();
                }
                else
                {
                    StopRecordingHooks();
                }
            }
        }
    }

    public bool IsRecording
    {
        get => CurrentRecordingSlot != RecordingSlot.None;
        set => CurrentRecordingSlot = value ? RecordingSlot.Hotkey : RecordingSlot.None;
    }

    public bool BlockHotkey { get; set; } = false;
    public bool StopOnEscape { get; set; } = true;

    public event Action<int>? HotkeyRecorded;
    public event Action? RecordingCancelled;
    public event Action<RecordingSlot, int>? SlotRecorded;
    public event Action<RecordingSlot>? SlotCancelled;
    public event Action? HotkeyDown;
    public event Action? HotkeyUp;
    public event Action? EmergencyStopTriggered;

    public GlobalKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        StartPollerThread();
    }

    private void StartPollerThread()
    {
        _isPollerRunning = true;
        _pollerThread = new Thread(PollerThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "HotkeyPollerThread"
        };
        _pollerThread.Start();
    }

    private void PollerThreadLoop()
    {
        Win32Api.TimeBeginPeriod(1);
        try
        {
            while (_isPollerRunning)
            {
                if (!IsRecording)
                {
                    // Emergency Stop on Escape (0x1B)
                    if (StopOnEscape && (Win32Api.GetAsyncKeyState(0x1B) & 0x8000) != 0)
                    {
                        EmergencyStopTriggered?.Invoke();
                    }

                    int vk = HotkeyVkCode;
                    if (vk > 0)
                    {
                        bool isDown = (Win32Api.GetAsyncKeyState(vk) & 0x8000) != 0;
                        if (isDown)
                        {
                            if (!_isHotkeyDown)
                            {
                                _isHotkeyDown = true;
                                HotkeyDown?.Invoke();
                            }
                        }
                        else
                        {
                            if (_isHotkeyDown)
                            {
                                _isHotkeyDown = false;
                                HotkeyUp?.Invoke();
                            }
                        }
                    }
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            Win32Api.TimeEndPeriod(1);
        }
    }

    private void StartRecordingHooks()
    {
        if (_hookThread != null && _hookThread.IsAlive) return;

        _hookStartedEvent.Reset();
        _isHookRunning = true;
        _hookThread = new Thread(HookThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "RecordingHookPump"
        };
        if (OperatingSystem.IsWindows())
        {
            _hookThread.SetApartmentState(ApartmentState.STA);
        }
        _hookThread.Start();
        _hookStartedEvent.Wait(2000);
    }

    private void ArmSwallowSafetyTimeout()
    {
        _swallowTimeoutCts?.Cancel();
        _swallowTimeoutCts = new CancellationTokenSource();
        var cts = _swallowTimeoutCts;
        Task.Delay(350, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _swallowNextKeyUpVkCode = 0;
                _swallowNextMouseUpMsg = 0;
                StopRecordingHooks();
            }
        }, TaskScheduler.Default);
    }

    private void StopRecordingHooks()
    {
        lock (_recordingLock)
        {
            _swallowTimeoutCts?.Cancel();
            _isHookRunning = false;
            uint threadId = _hookThreadId;
            Thread? thread = _hookThread;

            if (threadId != 0)
            {
                Win32Api.PostThreadMessage(threadId, 0x0012 /* WM_QUIT */, UIntPtr.Zero, IntPtr.Zero);
            }
            if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
            {
                thread.Join(500);
            }
            _hookThread = null;
            _hookThreadId = 0;
        }
    }

    private void HookThreadLoop()
    {
        _hookThreadId = Win32Api.GetCurrentThreadId();
        IntPtr hMod = IntPtr.Zero;
        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                hMod = Win32Api.GetModuleHandle(curModule.ModuleName);
            }
        }
        catch { }

        _keyboardHookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        if (_keyboardHookId == IntPtr.Zero)
        {
            _keyboardHookId = Win32Api.SetWindowsHookEx(Win32Api.WH_KEYBOARD_LL, _keyboardProc, IntPtr.Zero, 0);
        }

        _mouseHookId = Win32Api.SetWindowsHookEx(Win32Api.WH_MOUSE_LL, _mouseProc, hMod, 0);
        if (_mouseHookId == IntPtr.Zero)
        {
            _mouseHookId = Win32Api.SetWindowsHookEx(Win32Api.WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);
        }

        _hookStartedEvent.Set();

        while (_isHookRunning && Win32Api.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Win32Api.TranslateMessage(ref msg);
            Win32Api.DispatchMessage(ref msg);
        }

        if (_keyboardHookId != IntPtr.Zero)
        {
            Win32Api.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }

        if (_mouseHookId != IntPtr.Zero)
        {
            Win32Api.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    public void ResetKeyState()
    {
        _isHotkeyDown = false;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<Win32Api.KBDLLHOOKSTRUCT>(lParam);
            int vkCode = (int)hookStruct.vkCode;

            // Check if we need to swallow the matching KeyUp of a just-recorded key
            if (_swallowNextKeyUpVkCode != 0 && (msg == Win32Api.WM_KEYUP || msg == Win32Api.WM_SYSKEYUP))
            {
                if (vkCode == _swallowNextKeyUpVkCode)
                {
                    _swallowNextKeyUpVkCode = 0;
                    _swallowTimeoutCts?.Cancel();
                    StopRecordingHooks();
                    return (IntPtr)1; // Swallow key release so UI never sees it
                }
            }

            if (IsRecording)
            {
                if (msg == Win32Api.WM_KEYDOWN || msg == Win32Api.WM_SYSKEYDOWN)
                {
                    var slot = _currentRecordingSlot;
                    _currentRecordingSlot = RecordingSlot.None;

                    if (vkCode == 0x1B) // Escape cancels recording
                    {
                        RecordingCancelled?.Invoke();
                        SlotCancelled?.Invoke(slot);

                        _swallowNextKeyUpVkCode = vkCode;
                        ArmSwallowSafetyTimeout();
                        return (IntPtr)1;
                    }

                    if (slot == RecordingSlot.Hotkey)
                    {
                        HotkeyVkCode = vkCode;
                        HotkeyRecorded?.Invoke(vkCode);
                    }

                    SlotRecorded?.Invoke(slot, vkCode);

                    _swallowNextKeyUpVkCode = vkCode;
                    ArmSwallowSafetyTimeout();
                    return (IntPtr)1; // Swallow key press used for setting hotkey/modifier/primary
                }
            }
        }

        return Win32Api.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            // FAST PATH: Instantly ignore mouse moves and scroll wheel
            if (msg == 0x0200 || msg == 0x020A || msg == 0x020E)
            {
                return Win32Api.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            var hookStruct = Marshal.PtrToStructure<Win32Api.MSLLHOOKSTRUCT>(lParam);

            // Ignore simulated clicks generated by AutoClicker or injected inputs
            if (hookStruct.dwExtraInfo == AutoClickerEngine.INJECTED_SIGNATURE || (hookStruct.flags & 1) != 0)
            {
                return Win32Api.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            // Check if we need to swallow the matching MouseUp of a just-recorded mouse click
            if (_swallowNextMouseUpMsg != 0 && msg == _swallowNextMouseUpMsg)
            {
                _swallowNextMouseUpMsg = 0;
                _swallowTimeoutCts?.Cancel();
                StopRecordingHooks();
                return (IntPtr)1; // Swallow mouse release so UI never sees it
            }

            int vkCode = 0;
            int matchingUpMsg = 0;
            bool isDown = false;

            switch (msg)
            {
                case Win32Api.WM_LBUTTONDOWN:
                    vkCode = Win32Api.VK_LBUTTON;
                    matchingUpMsg = Win32Api.WM_LBUTTONUP;
                    isDown = true;
                    break;
                case Win32Api.WM_RBUTTONDOWN:
                    vkCode = Win32Api.VK_RBUTTON;
                    matchingUpMsg = Win32Api.WM_RBUTTONUP;
                    isDown = true;
                    break;
                case Win32Api.WM_MBUTTONDOWN:
                    vkCode = Win32Api.VK_MBUTTON;
                    matchingUpMsg = Win32Api.WM_MBUTTONUP;
                    isDown = true;
                    break;
                case Win32Api.WM_XBUTTONDOWN:
                    uint xbtnDown = (hookStruct.mouseData >> 16) & 0xFFFF;
                    vkCode = xbtnDown == 1 ? Win32Api.VK_XBUTTON1 : Win32Api.VK_XBUTTON2;
                    matchingUpMsg = Win32Api.WM_XBUTTONUP;
                    isDown = true;
                    break;
            }

            if (vkCode != 0 && IsRecording && isDown)
            {
                var slot = _currentRecordingSlot;
                _currentRecordingSlot = RecordingSlot.None;

                // If user clicks Left Click while recording Hotkey:
                // Cancel hotkey recording safely so Left Click is never accidentally assigned as global trigger
                if (slot == RecordingSlot.Hotkey && vkCode == Win32Api.VK_LBUTTON)
                {
                    RecordingCancelled?.Invoke();
                    SlotCancelled?.Invoke(slot);

                    _swallowNextMouseUpMsg = matchingUpMsg;
                    ArmSwallowSafetyTimeout();
                    return (IntPtr)1; // Swallow left click
                }

                if (slot == RecordingSlot.Hotkey)
                {
                    HotkeyVkCode = vkCode;
                    HotkeyRecorded?.Invoke(vkCode);
                }

                SlotRecorded?.Invoke(slot, vkCode);

                _swallowNextMouseUpMsg = matchingUpMsg;
                ArmSwallowSafetyTimeout();
                return (IntPtr)1; // Swallow mouse click used for setting hotkey/modifier/primary
            }
        }

        return Win32Api.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _swallowTimeoutCts?.Cancel();
        _swallowTimeoutCts?.Dispose();
        _isPollerRunning = false;
        _pollerThread?.Join(500);

        StopRecordingHooks();

        _hookStartedEvent.Dispose();
        GC.SuppressFinalize(this);
    }

    ~GlobalKeyboardHook()
    {
        Dispose();
    }
}
