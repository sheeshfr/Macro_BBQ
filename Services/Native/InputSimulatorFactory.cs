using System;

namespace AutoClicker.Services.Native;

public static class InputSimulatorFactory
{
    public static IInputSimulator Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Win32InputSimulator();
        }
        else if (OperatingSystem.IsLinux())
        {
            return new LinuxUinputSimulator();
        }

        // Fallback default for other platforms
        return new LinuxUinputSimulator();
    }
}
