using System;
using System.Runtime.InteropServices;

#pragma warning disable CS8981

namespace AutoClicker.Services.Native;

public static class LinuxNative
{
    public const int O_RDONLY = 0x0000;
    public const int O_WRONLY = 0x0001;
    public const int O_RDWR   = 0x0002;
    public const int O_NONBLOCK = 0x0800;

    // Event Types
    public const ushort EV_SYN = 0x00;
    public const ushort EV_KEY = 0x01;
    public const ushort EV_REL = 0x02;
    public const ushort EV_ABS = 0x03;

    public const ushort SYN_REPORT = 0x00;

    // Relative Axes
    public const ushort REL_X = 0x00;
    public const ushort REL_Y = 0x01;
    public const ushort REL_WHEEL = 0x08;

    // Mouse Buttons
    public const ushort BTN_MOUSE = 0x110;
    public const ushort BTN_LEFT = 0x110;
    public const ushort BTN_RIGHT = 0x111;
    public const ushort BTN_MIDDLE = 0x112;
    public const ushort BTN_SIDE = 0x113; // Mouse 4 (back)
    public const ushort BTN_EXTRA = 0x114; // Mouse 5 (forward)
    public const ushort BTN_FORWARD = 0x115;
    public const ushort BTN_BACK = 0x116;

    // Keyboard Keycodes
    public const ushort KEY_RESERVED = 0;
    public const ushort KEY_ESC = 1;
    public const ushort KEY_1 = 2;
    public const ushort KEY_2 = 3;
    public const ushort KEY_3 = 4;
    public const ushort KEY_4 = 5;
    public const ushort KEY_5 = 6;
    public const ushort KEY_6 = 7;
    public const ushort KEY_7 = 8;
    public const ushort KEY_8 = 9;
    public const ushort KEY_9 = 10;
    public const ushort KEY_0 = 11;
    public const ushort KEY_MINUS = 12;
    public const ushort KEY_EQUAL = 13;
    public const ushort KEY_BACKSPACE = 14;
    public const ushort KEY_TAB = 15;
    public const ushort KEY_Q = 16;
    public const ushort KEY_W = 17;
    public const ushort KEY_E = 18;
    public const ushort KEY_R = 19;
    public const ushort KEY_T = 20;
    public const ushort KEY_Y = 21;
    public const ushort KEY_U = 22;
    public const ushort KEY_I = 23;
    public const ushort KEY_O = 24;
    public const ushort KEY_P = 25;
    public const ushort KEY_LEFTBRACE = 26;
    public const ushort KEY_RIGHTBRACE = 27;
    public const ushort KEY_ENTER = 28;
    public const ushort KEY_LEFTCTRL = 29;
    public const ushort KEY_A = 30;
    public const ushort KEY_S = 31;
    public const ushort KEY_D = 32;
    public const ushort KEY_F = 33;
    public const ushort KEY_G = 34;
    public const ushort KEY_H = 35;
    public const ushort KEY_J = 36;
    public const ushort KEY_K = 37;
    public const ushort KEY_L = 38;
    public const ushort KEY_SEMICOLON = 39;
    public const ushort KEY_APOSTROPHE = 40;
    public const ushort KEY_GRAVE = 41;
    public const ushort KEY_LEFTSHIFT = 42;
    public const ushort KEY_BACKSLASH = 43;
    public const ushort KEY_Z = 44;
    public const ushort KEY_X = 45;
    public const ushort KEY_C = 46;
    public const ushort KEY_V = 47;
    public const ushort KEY_B = 48;
    public const ushort KEY_N = 49;
    public const ushort KEY_M = 50;
    public const ushort KEY_COMMA = 51;
    public const ushort KEY_DOT = 52;
    public const ushort KEY_SLASH = 53;
    public const ushort KEY_RIGHTSHIFT = 54;
    public const ushort KEY_KPASTERISK = 55;
    public const ushort KEY_LEFTALT = 56;
    public const ushort KEY_SPACE = 57;
    public const ushort KEY_CAPSLOCK = 58;
    public const ushort KEY_F1 = 59;
    public const ushort KEY_F2 = 60;
    public const ushort KEY_F3 = 61;
    public const ushort KEY_F4 = 62;
    public const ushort KEY_F5 = 63;
    public const ushort KEY_F6 = 64;
    public const ushort KEY_F7 = 65;
    public const ushort KEY_F8 = 66;
    public const ushort KEY_F9 = 67;
    public const ushort KEY_F10 = 68;
    public const ushort KEY_NUMLOCK = 69;
    public const ushort KEY_SCROLLLOCK = 70;
    public const ushort KEY_KP7 = 71;
    public const ushort KEY_KP8 = 72;
    public const ushort KEY_KP9 = 73;
    public const ushort KEY_KPMINUS = 74;
    public const ushort KEY_KP4 = 75;
    public const ushort KEY_KP5 = 76;
    public const ushort KEY_KP6 = 77;
    public const ushort KEY_KPPLUS = 78;
    public const ushort KEY_KP1 = 79;
    public const ushort KEY_KP2 = 80;
    public const ushort KEY_KP3 = 81;
    public const ushort KEY_KP0 = 82;
    public const ushort KEY_KPDOT = 83;
    public const ushort KEY_F11 = 87;
    public const ushort KEY_F12 = 88;
    public const ushort KEY_KPENTER = 96;
    public const ushort KEY_RIGHTCTRL = 97;
    public const ushort KEY_KPSLASH = 98;
    public const ushort KEY_SYSRQ = 99;
    public const ushort KEY_RIGHTALT = 100;
    public const ushort KEY_HOME = 102;
    public const ushort KEY_UP = 103;
    public const ushort KEY_PAGEUP = 104;
    public const ushort KEY_LEFT = 105;
    public const ushort KEY_RIGHT = 106;
    public const ushort KEY_END = 107;
    public const ushort KEY_DOWN = 108;
    public const ushort KEY_PAGEDOWN = 109;
    public const ushort KEY_INSERT = 110;
    public const ushort KEY_DELETE = 111;
    public const ushort KEY_MUTE = 113;
    public const ushort KEY_VOLUMEDOWN = 114;
    public const ushort KEY_VOLUMEUP = 115;
    public const ushort KEY_PAUSE = 119;
    public const ushort KEY_LEFTMETA = 125;
    public const ushort KEY_RIGHTMETA = 126;
    public const ushort KEY_PLAYPAUSE = 164;
    public const ushort KEY_NEXTSONG = 163;
    public const ushort KEY_PREVIOUSSONG = 165;
    public const ushort KEY_STOPCD = 166;
    public const ushort KEY_F13 = 183;
    public const ushort KEY_F14 = 184;
    public const ushort KEY_F15 = 185;
    public const ushort KEY_F16 = 186;
    public const ushort KEY_F17 = 187;
    public const ushort KEY_F18 = 188;
    public const ushort KEY_F19 = 189;
    public const ushort KEY_F20 = 190;
    public const ushort KEY_F21 = 191;
    public const ushort KEY_F22 = 192;
    public const ushort KEY_F23 = 193;
    public const ushort KEY_F24 = 194;

    // Linux uinput IOCTLs (x86_64 and arm64 standard values)
    public const ulong UI_SET_EVBIT   = 0x40045564;
    public const ulong UI_SET_KEYBIT  = 0x40045565;
    public const ulong UI_SET_RELBIT  = 0x40045566;
    public const ulong UI_SET_ABSBIT  = 0x40045567;
    public const ulong UI_DEV_CREATE  = 0x5501;
    public const ulong UI_DEV_DESTROY = 0x5502;
    public const ulong UI_DEV_SETUP   = 0x405c5503;
    public const ulong EVIOCGNAME_256 = 0x81004506;
    public const ulong EVIOCGKEY_64   = 0x80404518;

    // Bus types
    public const ushort BUS_USB = 0x03;

    [StructLayout(LayoutKind.Sequential)]
    public struct input_id
    {
        public ushort bustype;
        public ushort vendor;
        public ushort product;
        public ushort version;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct uinput_setup
    {
        public input_id id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;
        public uint ff_effects_max;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct uinput_user_dev
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string name;
        public input_id id;
        public uint ff_effects_max;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmax;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absmin;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absfuzz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public int[] absflat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct timeval
    {
        public IntPtr tv_sec;
        public IntPtr tv_usec;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct input_event
    {
        public timeval time;
        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pollfd
    {
        public int fd;
        public short events;
        public short revents;
    }

    public const short POLLIN = 0x0001;
    public const short POLLPRI = 0x0002;
    public const short POLLERR = 0x0008;
    public const short POLLHUP = 0x0010;
    public const short POLLNVAL = 0x0020;

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    public static extern int Open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    public static extern int Close(int fd);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    public static extern IntPtr Write(int fd, IntPtr buf, IntPtr count);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    public static extern IntPtr Read(int fd, IntPtr buf, IntPtr count);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static extern int Ioctl(int fd, ulong request, int param);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static extern int Ioctl(int fd, ulong request, ref uinput_setup setup);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static extern int Ioctl(int fd, ulong request, ref uinput_user_dev userDev);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static extern int Ioctl(int fd, ulong request, IntPtr ptr);

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    public static extern int Poll([In, Out] pollfd[] fds, uint nfds, int timeout);
}
