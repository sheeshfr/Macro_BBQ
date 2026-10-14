using System;

namespace AutoClicker.Services.Native;

public static class InputSimulatorFactory
{
    public static IInputSimulator Create()
    {
        return new Win32InputSimulator();
    }
}
