using System;

namespace AutoClicker.Services.Native;

public static class GlobalInputHookFactory
{
    public static IGlobalInputHook Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new GlobalKeyboardHook();
        }
        else if (OperatingSystem.IsLinux())
        {
            return new LinuxInputMonitor();
        }

        return new LinuxInputMonitor();
    }
}
