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
            ?? (OperatingSystem.IsWindows() 
                ? @"R:\github\Macro BBQ\publish\MacroBBQ.exe"
                : "/usr/local/bin/macrobbq");
    }

    private static string GetLinuxAutostartPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "autostart", "macrobbq.desktop");
    }

    public static bool IsStartupEnabled()
    {
        if (OperatingSystem.IsWindows())
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
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                return File.Exists(GetLinuxAutostartPath());
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public static void ApplyStartup(bool enable, bool openMinimized)
    {
        if (OperatingSystem.IsWindows())
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
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                string autostartPath = GetLinuxAutostartPath();
                if (enable)
                {
                    string dir = Path.GetDirectoryName(autostartPath)!;
                    Directory.CreateDirectory(dir);

                    string exePath = GetExecutablePath();
                    string execLine = openMinimized ? $"\"{exePath}\" --minimized" : $"\"{exePath}\"";

                    string content = $"""
                        [Desktop Entry]
                        Type=Application
                        Version=1.0
                        Name=Macro BBQ
                        GenericName=Game Macro & Auto Clicker
                        Comment=Ultra-fast gaming auto-clicker and input macro utility
                        Exec={execLine}
                        Icon=macrobbq
                        Terminal=false
                        Categories=Game;Utility;
                        X-GNOME-Autostart-enabled=true
                        """;

                    File.WriteAllText(autostartPath, content);
                }
                else
                {
                    if (File.Exists(autostartPath))
                    {
                        File.Delete(autostartPath);
                    }
                }
            }
            catch
            {
                // Silently handle if filesystem access is constrained
            }
        }
    }
}
