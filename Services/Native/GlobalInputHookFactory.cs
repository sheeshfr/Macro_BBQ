using System;

namespace AutoClicker.Services.Native;

public static class GlobalInputHookFactory
{
    public static IGlobalInputHook Create()
    {
        return new GlobalKeyboardHook();
    }
}
