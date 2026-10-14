using System;
using System.Text;

namespace AutoClicker.Services.Native;

public static class KeyHelper
{
    public static string GetKeyName(int vkCode)
    {
        // Mouse Buttons
        switch (vkCode)
        {
            case 0x01: return "M1 (Left)";
            case 0x02: return "M2 (Right)";
            case 0x04: return "M3 (Middle)";
            case 0x05: return "M4";
            case 0x06: return "M5";
        }

        // Special friendly names for function keys F1 - F24
        if (vkCode >= 0x70 && vkCode <= 0x87)
        {
            return $"F{vkCode - 0x70 + 1}";
        }

        // Numbers 0-9
        if (vkCode >= 0x30 && vkCode <= 0x39)
        {
            return ((char)vkCode).ToString();
        }

        // Letters A-Z
        if (vkCode >= 0x41 && vkCode <= 0x5A)
        {
            return ((char)vkCode).ToString();
        }

        // Numpad 0-9
        if (vkCode >= 0x60 && vkCode <= 0x69)
        {
            return $"Num {vkCode - 0x60}";
        }

        // Known named keys
        switch (vkCode)
        {
            case 0x08: return "Backspace";
            case 0x09: return "Tab";
            case 0x0D: return "Enter";
            case 0x13: return "Pause";
            case 0x14: return "Caps Lock";
            case 0x1B: return "Escape";
            case 0x20: return "Space";
            case 0x21: return "Page Up";
            case 0x22: return "Page Down";
            case 0x23: return "End";
            case 0x24: return "Home";
            case 0x25: return "Left Arrow";
            case 0x26: return "Up Arrow";
            case 0x27: return "Right Arrow";
            case 0x28: return "Down Arrow";
            case 0x2C: return "Print Screen";
            case 0x2D: return "Insert";
            case 0x2E: return "Delete";
            case 0x5B: return "Left Win";
            case 0x5C: return "Right Win";
            case 0x6A: return "Num *";
            case 0x6B: return "Num +";
            case 0x6D: return "Num -";
            case 0x6E: return "Num .";
            case 0x6F: return "Num /";
            case 0x90: return "Num Lock";
            case 0x91: return "Scroll Lock";
            case 0xA0: return "Left Shift";
            case 0xA1: return "Right Shift";
            case 0xA2: return "Left Ctrl";
            case 0xA3: return "Right Ctrl";
            case 0xA4: return "Left Alt";
            case 0xA5: return "Right Alt";
            case 0xA6: return "Browser Back";
            case 0xA7: return "Browser Forward";
            case 0xA8: return "Browser Refresh";
            case 0xA9: return "Browser Stop";
            case 0xAA: return "Browser Search";
            case 0xAB: return "Browser Favorites";
            case 0xAC: return "Browser Home";
            case 0xAD: return "Volume Mute";
            case 0xAE: return "Volume Down";
            case 0xAF: return "Volume Up";
            case 0xB0: return "Next Track";
            case 0xB1: return "Previous Track";
            case 0xB2: return "Stop Media";
            case 0xB3: return "Play / Pause";
            case 0xBA: return ";";
            case 0xBB: return "=";
            case 0xBC: return ",";
            case 0xBD: return "-";
            case 0xBE: return ".";
            case 0xBF: return "/";
            case 0xC0: return "`";
            case 0xDB: return "[";
            case 0xDC: return "\\";
            case 0xDD: return "]";
            case 0xDE: return "'";
        }

        // Fallback to Win32 GetKeyNameText
        try
        {
            uint scanCode = Win32Api.MapVirtualKey((uint)vkCode, 0); // MAPVK_VK_TO_VSC = 0
            if (scanCode != 0)
            {
                int lParam = (int)(scanCode << 16);
                var sb = new StringBuilder(256);
                if (Win32Api.GetKeyNameText(lParam, sb, sb.Capacity) > 0)
                {
                    return sb.ToString();
                }
            }
        }
        catch
        {
            // Ignore fallback error
        }

        return $"Key 0x{vkCode:X2}";
    }
}
