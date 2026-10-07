using System;
using System.Runtime.InteropServices;

namespace AutoClicker.Services.Native;

public class Win32InputSimulator : IInputSimulator
{
    public static readonly UIntPtr INJECTED_SIGNATURE = (UIntPtr)0xC11C801;

    public static bool IsMouseButton(int vkCode)
    {
        return vkCode is Win32Api.VK_LBUTTON or Win32Api.VK_RBUTTON or Win32Api.VK_MBUTTON or Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2;
    }

    public void SendButtonDown(int vkCode)
    {
        if (IsMouseButton(vkCode))
        {
            uint flag = vkCode switch
            {
                Win32Api.VK_RBUTTON => Win32Api.MOUSEEVENTF_RIGHTDOWN,
                Win32Api.VK_MBUTTON => Win32Api.MOUSEEVENTF_MIDDLEDOWN,
                Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2 => Win32Api.MOUSEEVENTF_XDOWN,
                _ => Win32Api.MOUSEEVENTF_LEFTDOWN
            };
            uint mouseData = vkCode == Win32Api.VK_XBUTTON2 ? 2u : (vkCode == Win32Api.VK_XBUTTON1 ? 1u : 0u);

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                mi = new Win32Api.MOUSEINPUT
                {
                    mouseData = mouseData,
                    dwFlags = flag,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.mouse_event(flag, 0, 0, mouseData, INJECTED_SIGNATURE);
            }
        }
        else
        {
            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_KEYBOARD,
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = (ushort)vkCode,
                    wScan = 0,
                    dwFlags = Win32Api.KEYEVENTF_KEYDOWN,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.keybd_event((byte)vkCode, 0, Win32Api.KEYEVENTF_KEYDOWN, INJECTED_SIGNATURE);
            }
        }
    }

    public void SendButtonUp(int vkCode)
    {
        if (IsMouseButton(vkCode))
        {
            uint flag = vkCode switch
            {
                Win32Api.VK_RBUTTON => Win32Api.MOUSEEVENTF_RIGHTUP,
                Win32Api.VK_MBUTTON => Win32Api.MOUSEEVENTF_MIDDLEUP,
                Win32Api.VK_XBUTTON1 or Win32Api.VK_XBUTTON2 => Win32Api.MOUSEEVENTF_XUP,
                _ => Win32Api.MOUSEEVENTF_LEFTUP
            };
            uint mouseData = vkCode == Win32Api.VK_XBUTTON2 ? 2u : (vkCode == Win32Api.VK_XBUTTON1 ? 1u : 0u);

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                mi = new Win32Api.MOUSEINPUT
                {
                    mouseData = mouseData,
                    dwFlags = flag,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.mouse_event(flag, 0, 0, mouseData, INJECTED_SIGNATURE);
            }
        }
        else
        {
            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_KEYBOARD,
                ki = new Win32Api.KEYBDINPUT
                {
                    wVk = (ushort)vkCode,
                    wScan = 0,
                    dwFlags = Win32Api.KEYEVENTF_KEYUP,
                    dwExtraInfo = INJECTED_SIGNATURE
                }
            };

            uint sent = Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf<Win32Api.INPUT>());
            if (sent == 0)
            {
                Win32Api.keybd_event((byte)vkCode, 0, Win32Api.KEYEVENTF_KEYUP, INJECTED_SIGNATURE);
            }
        }
    }

    public void ReleaseAllButtons(int primaryVkCode, bool isModifierEnabled, int modifierVkCode)
    {
        try
        {
            SendButtonUp(primaryVkCode);
            if (isModifierEnabled)
            {
                SendButtonUp(modifierVkCode);
            }
        }
        catch
        {
            // Ignore during cleanup
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
