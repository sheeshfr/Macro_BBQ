using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoClicker.Services.Native;

public class LinuxUinputSimulator : IInputSimulator
{
    private int _uinputFd = -1;
    private readonly object _lock = new();
    private readonly HashSet<int> _heldButtons = new();
    private readonly int _eventSize = Marshal.SizeOf<LinuxNative.input_event>();
    private IntPtr _eventBuffer = IntPtr.Zero;

    public bool IsInitialized => _uinputFd >= 0;

    public LinuxUinputSimulator()
    {
        InitializeUinput();
    }

    private void InitializeUinput()
    {
        lock (_lock)
        {
            try
            {
                _uinputFd = LinuxNative.Open("/dev/uinput", LinuxNative.O_WRONLY | LinuxNative.O_NONBLOCK);
                if (_uinputFd < 0)
                {
                    _uinputFd = LinuxNative.Open("/dev/input/uinput", LinuxNative.O_WRONLY | LinuxNative.O_NONBLOCK);
                }

                if (_uinputFd < 0)
                {
                    Console.WriteLine("[Macro BBQ] Warning: Could not open /dev/uinput. Ensure your user has write permissions or run MacroBBQ.sh setup.");
                    return;
                }

                _eventBuffer = Marshal.AllocHGlobal(_eventSize * 2);

                // 1. Enable Event Types
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_EVBIT, (int)LinuxNative.EV_KEY);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_EVBIT, (int)LinuxNative.EV_REL);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_EVBIT, (int)LinuxNative.EV_SYN);

                // 2. Enable Mouse Buttons & Relative axes
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_LEFT);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_RIGHT);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_MIDDLE);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_SIDE);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_EXTRA);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_FORWARD);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, (int)LinuxNative.BTN_BACK);

                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_RELBIT, (int)LinuxNative.REL_X);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_RELBIT, (int)LinuxNative.REL_Y);
                LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_RELBIT, (int)LinuxNative.REL_WHEEL);

                // 3. Enable Standard Keyboard Keys (Keys 1 through 255)
                for (int key = 1; key <= 255; key++)
                {
                    LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_SET_KEYBIT, key);
                }

                // 4. Setup Device
                var setup = new LinuxNative.uinput_setup
                {
                    id = new LinuxNative.input_id
                    {
                        bustype = LinuxNative.BUS_USB,
                        vendor = 0x1234,
                        product = 0x5678,
                        version = 1
                    },
                    name = "Macro BBQ Virtual Controller",
                    ff_effects_max = 0
                };

                int setupRes = LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_DEV_SETUP, ref setup);
                if (setupRes < 0)
                {
                    // Fallback to legacy uinput_user_dev ioctl
                    var userDev = new LinuxNative.uinput_user_dev
                    {
                        name = "Macro BBQ Virtual Controller",
                        id = new LinuxNative.input_id
                        {
                            bustype = LinuxNative.BUS_USB,
                            vendor = 0x1234,
                            product = 0x5678,
                            version = 1
                        }
                    };
                    LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_DEV_SETUP, ref userDev);
                }

                // 5. Create Device
                int createRes = LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_DEV_CREATE, 0);
                if (createRes < 0)
                {
                    Console.WriteLine("[Macro BBQ] Error: UI_DEV_CREATE failed.");
                    LinuxNative.Close(_uinputFd);
                    _uinputFd = -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Macro BBQ] Failed to initialize uinput device: {ex.Message}");
                if (_uinputFd >= 0)
                {
                    LinuxNative.Close(_uinputFd);
                    _uinputFd = -1;
                }
            }
        }
    }

    public void SendButtonDown(int vkCode)
    {
        lock (_lock)
        {
            _heldButtons.Add(vkCode);
        }
        EmitKeyOrButton(vkCode, 1);
    }

    public void SendButtonUp(int vkCode)
    {
        lock (_lock)
        {
            _heldButtons.Remove(vkCode);
        }
        EmitKeyOrButton(vkCode, 0);
    }

    public void ReleaseAllButtons(int primaryVkCode, bool isModifierEnabled, int modifierVkCode)
    {
        SendButtonUp(primaryVkCode);
        if (isModifierEnabled)
        {
            SendButtonUp(modifierVkCode);
        }

        lock (_lock)
        {
            if (_heldButtons.Count > 0)
            {
                foreach (int code in _heldButtons)
                {
                    EmitKeyOrButton(code, 0);
                }
                _heldButtons.Clear();
            }
        }
    }

    private void EmitKeyOrButton(int vkCode, int value)
    {
        if (_uinputFd < 0 || _eventBuffer == IntPtr.Zero) return;

        if (TryMapVkToEvdev(vkCode, out ushort type, out ushort code))
        {
            lock (_lock)
            {
                if (_uinputFd < 0 || _eventBuffer == IntPtr.Zero) return;

                var evKey = new LinuxNative.input_event
                {
                    time = new LinuxNative.timeval { tv_sec = IntPtr.Zero, tv_usec = IntPtr.Zero },
                    type = type,
                    code = code,
                    value = value
                };

                var evSyn = new LinuxNative.input_event
                {
                    time = new LinuxNative.timeval { tv_sec = IntPtr.Zero, tv_usec = IntPtr.Zero },
                    type = LinuxNative.EV_SYN,
                    code = LinuxNative.SYN_REPORT,
                    value = 0
                };

                Marshal.StructureToPtr(evKey, _eventBuffer, false);
                Marshal.StructureToPtr(evSyn, IntPtr.Add(_eventBuffer, _eventSize), false);
                LinuxNative.Write(_uinputFd, _eventBuffer, (IntPtr)(_eventSize * 2));
            }
        }
    }

    public static bool TryMapVkToEvdev(int vkCode, out ushort type, out ushort code)
    {
        type = LinuxNative.EV_KEY;

        // Mouse buttons
        switch (vkCode)
        {
            case 0x01: // VK_LBUTTON
                code = LinuxNative.BTN_LEFT;
                return true;
            case 0x02: // VK_RBUTTON
                code = LinuxNative.BTN_RIGHT;
                return true;
            case 0x04: // VK_MBUTTON
                code = LinuxNative.BTN_MIDDLE;
                return true;
            case 0x05: // VK_XBUTTON1 (M4)
                code = LinuxNative.BTN_SIDE;
                return true;
            case 0x06: // VK_XBUTTON2 (M5)
                code = LinuxNative.BTN_EXTRA;
                return true;
        }

        // Function keys F1 - F24
        if (vkCode >= 0x70 && vkCode <= 0x7B) // F1 - F12
        {
            code = (ushort)(LinuxNative.KEY_F1 + (vkCode - 0x70));
            return true;
        }
        if (vkCode >= 0x7C && vkCode <= 0x87) // F13 - F24
        {
            code = (ushort)(LinuxNative.KEY_F13 + (vkCode - 0x7C));
            return true;
        }

        // Digits 0-9
        if (vkCode == 0x30) { code = LinuxNative.KEY_0; return true; }
        if (vkCode >= 0x31 && vkCode <= 0x39)
        {
            code = (ushort)(LinuxNative.KEY_1 + (vkCode - 0x31));
            return true;
        }

        // Letters A-Z
        switch (vkCode)
        {
            case 0x41: code = LinuxNative.KEY_A; return true;
            case 0x42: code = LinuxNative.KEY_B; return true;
            case 0x43: code = LinuxNative.KEY_C; return true;
            case 0x44: code = LinuxNative.KEY_D; return true;
            case 0x45: code = LinuxNative.KEY_E; return true;
            case 0x46: code = LinuxNative.KEY_F; return true;
            case 0x47: code = LinuxNative.KEY_G; return true;
            case 0x48: code = LinuxNative.KEY_H; return true;
            case 0x49: code = LinuxNative.KEY_I; return true;
            case 0x4A: code = LinuxNative.KEY_J; return true;
            case 0x4B: code = LinuxNative.KEY_K; return true;
            case 0x4C: code = LinuxNative.KEY_L; return true;
            case 0x4D: code = LinuxNative.KEY_M; return true;
            case 0x4E: code = LinuxNative.KEY_N; return true;
            case 0x4F: code = LinuxNative.KEY_O; return true;
            case 0x50: code = LinuxNative.KEY_P; return true;
            case 0x51: code = LinuxNative.KEY_Q; return true;
            case 0x52: code = LinuxNative.KEY_R; return true;
            case 0x53: code = LinuxNative.KEY_S; return true;
            case 0x54: code = LinuxNative.KEY_T; return true;
            case 0x55: code = LinuxNative.KEY_U; return true;
            case 0x56: code = LinuxNative.KEY_V; return true;
            case 0x57: code = LinuxNative.KEY_W; return true;
            case 0x58: code = LinuxNative.KEY_X; return true;
            case 0x59: code = LinuxNative.KEY_Y; return true;
            case 0x5A: code = LinuxNative.KEY_Z; return true;
        }

        // Common keys
        switch (vkCode)
        {
            case 0x08: code = LinuxNative.KEY_BACKSPACE; return true;
            case 0x09: code = LinuxNative.KEY_TAB; return true;
            case 0x0D: code = LinuxNative.KEY_ENTER; return true;
            case 0x13: code = LinuxNative.KEY_PAUSE; return true;
            case 0x14: code = LinuxNative.KEY_CAPSLOCK; return true;
            case 0x1B: code = LinuxNative.KEY_ESC; return true;
            case 0x20: code = LinuxNative.KEY_SPACE; return true;
            case 0x21: code = LinuxNative.KEY_PAGEUP; return true;
            case 0x22: code = LinuxNative.KEY_PAGEDOWN; return true;
            case 0x23: code = LinuxNative.KEY_END; return true;
            case 0x24: code = LinuxNative.KEY_HOME; return true;
            case 0x25: code = LinuxNative.KEY_LEFT; return true;
            case 0x26: code = LinuxNative.KEY_UP; return true;
            case 0x27: code = LinuxNative.KEY_RIGHT; return true;
            case 0x28: code = LinuxNative.KEY_DOWN; return true;
            case 0x2D: code = LinuxNative.KEY_INSERT; return true;
            case 0x2E: code = LinuxNative.KEY_DELETE; return true;
            case 0x5B: code = LinuxNative.KEY_LEFTMETA; return true;
            case 0x5C: code = LinuxNative.KEY_RIGHTMETA; return true;
            case 0x60: code = LinuxNative.KEY_KP0; return true;
            case 0x61: code = LinuxNative.KEY_KP1; return true;
            case 0x62: code = LinuxNative.KEY_KP2; return true;
            case 0x63: code = LinuxNative.KEY_KP3; return true;
            case 0x64: code = LinuxNative.KEY_KP4; return true;
            case 0x65: code = LinuxNative.KEY_KP5; return true;
            case 0x66: code = LinuxNative.KEY_KP6; return true;
            case 0x67: code = LinuxNative.KEY_KP7; return true;
            case 0x68: code = LinuxNative.KEY_KP8; return true;
            case 0x69: code = LinuxNative.KEY_KP9; return true;
            case 0x6A: code = LinuxNative.KEY_KPASTERISK; return true;
            case 0x6B: code = LinuxNative.KEY_KPPLUS; return true;
            case 0x6D: code = LinuxNative.KEY_KPMINUS; return true;
            case 0x6E: code = LinuxNative.KEY_KPDOT; return true;
            case 0x6F: code = LinuxNative.KEY_KPSLASH; return true;
            case 0x90: code = LinuxNative.KEY_NUMLOCK; return true;
            case 0x91: code = LinuxNative.KEY_SCROLLLOCK; return true;
            case 0xA0: code = LinuxNative.KEY_LEFTSHIFT; return true;
            case 0xA1: code = LinuxNative.KEY_RIGHTSHIFT; return true;
            case 0xA2: code = LinuxNative.KEY_LEFTCTRL; return true;
            case 0xA3: code = LinuxNative.KEY_RIGHTCTRL; return true;
            case 0xA4: code = LinuxNative.KEY_LEFTALT; return true;
            case 0xA5: code = LinuxNative.KEY_RIGHTALT; return true;
            case 0xAD: code = LinuxNative.KEY_MUTE; return true;
            case 0xAE: code = LinuxNative.KEY_VOLUMEDOWN; return true;
            case 0xAF: code = LinuxNative.KEY_VOLUMEUP; return true;
            case 0xB0: code = LinuxNative.KEY_NEXTSONG; return true;
            case 0xB1: code = LinuxNative.KEY_PREVIOUSSONG; return true;
            case 0xB2: code = LinuxNative.KEY_STOPCD; return true;
            case 0xB3: code = LinuxNative.KEY_PLAYPAUSE; return true;
            case 0xBA: code = LinuxNative.KEY_SEMICOLON; return true;
            case 0xBB: code = LinuxNative.KEY_EQUAL; return true;
            case 0xBC: code = LinuxNative.KEY_COMMA; return true;
            case 0xBD: code = LinuxNative.KEY_MINUS; return true;
            case 0xBE: code = LinuxNative.KEY_DOT; return true;
            case 0xBF: code = LinuxNative.KEY_SLASH; return true;
            case 0xC0: code = LinuxNative.KEY_GRAVE; return true;
            case 0xDB: code = LinuxNative.KEY_LEFTBRACE; return true;
            case 0xDC: code = LinuxNative.KEY_BACKSLASH; return true;
            case 0xDD: code = LinuxNative.KEY_RIGHTBRACE; return true;
            case 0xDE: code = LinuxNative.KEY_APOSTROPHE; return true;
        }

        code = 0;
        return false;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_uinputFd >= 0)
            {
                try
                {
                    LinuxNative.Ioctl(_uinputFd, LinuxNative.UI_DEV_DESTROY, 0);
                    LinuxNative.Close(_uinputFd);
                }
                catch { }
                _uinputFd = -1;
            }

            if (_eventBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_eventBuffer);
                _eventBuffer = IntPtr.Zero;
            }
        }
        GC.SuppressFinalize(this);
    }
}
