using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace AutoClicker.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MacroBBQ";
    private const string LegacyAppName = "ClickboyBBQ";

    public static string GetExecutablePath()
    {
        return Environment.ProcessPath 
            ?? Process.GetCurrentProcess().MainModule?.FileName 
            ?? Path.Combine(AppContext.BaseDirectory, "MacroBBQ.exe");
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null || key?.GetValue(LegacyAppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyStartup(bool enable, bool openMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = GetExecutablePath();
                string command = $"\"{exePath}\"";
                if (openMinimized)
                {
                    command += " --minimized";
                }
                key.SetValue(AppName, command);
                key.DeleteValue(LegacyAppName, false);
            }
            else
            {
                key.DeleteValue(AppName, false);
                key.DeleteValue(LegacyAppName, false);
            }
        }
        catch
        {
            // Silently handle if registry access is constrained
        }
    }
}
