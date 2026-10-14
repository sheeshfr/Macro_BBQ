using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoClicker.Models;

namespace AutoClicker.Services;

public class AppConfig
{
    public string ActiveProfileName { get; set; } = "Default";
    public List<Profile> Profiles { get; set; } = new();

    // General app settings
    public bool BlockHotkey { get; set; } = false;
    public bool StopOnEscape { get; set; } = true;
    public bool CloseToTray { get; set; } = true;

    // Window position
    public int? WindowPositionX { get; set; }
    public int? WindowPositionY { get; set; }

    // Legacy fields for backward compatibility with previous versions
    public int IntervalMs { get; set; } = 50;
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Hold;
    public int HotkeyVkCode { get; set; } = 0x75; // F6
    public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;
    public int PrimaryVkCode { get; set; } = 0x01; // Left Click
    public bool IsModifierEnabled { get; set; } = false;
    public MouseButtonType ModifierButton { get; set; } = MouseButtonType.Right;
    public int ModifierVkCode { get; set; } = 0x02; // Right Click
    public ModifierAction ModifierAction { get; set; } = ModifierAction.Hold;
    public ModifierOrder ModifierOrder { get; set; } = ModifierOrder.ModifierFirst;
    public int ModifierDelayMs { get; set; } = 1;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(List<Profile>))]
public partial class AppConfigJsonContext : JsonSerializerContext
{
}

public static class SettingsService
{
    private static readonly object FileLock = new();
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacroBBQ");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly string OldSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClickboyBBQ",
        "settings.json");

    public static AppConfig Load()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(SettingsPath) && File.Exists(OldSettingsPath))
                {
                    try
                    {
                        Directory.CreateDirectory(SettingsDirectory);
                        File.Copy(OldSettingsPath, SettingsPath, true);
                    }
                    catch
                    {
                        // Fall through to try loading old path directly if copy failed
                    }
                }

                if (File.Exists(SettingsPath))
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            string json = File.ReadAllText(SettingsPath);
                            var config = (AppConfig?)JsonSerializer.Deserialize(json, typeof(AppConfig), AppConfigJsonContext.Default);
                            if (config != null)
                            {
                                EnsureValidProfiles(config);
                                return config;
                            }
                            break;
                        }
                        catch (IOException) when (attempt < 2)
                        {
                            System.Threading.Thread.Sleep(20);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to defaults
            }

            var defaultConfig = new AppConfig();
            EnsureValidProfiles(defaultConfig);
            return defaultConfig;
        }
    }

    public static void Save(AppConfig config)
    {
        lock (FileLock)
        {
            try
            {
                EnsureValidProfiles(config);
                Directory.CreateDirectory(SettingsDirectory);
                string json = JsonSerializer.Serialize(config, typeof(AppConfig), AppConfigJsonContext.Default);

                string tempPath = Path.Combine(SettingsDirectory, $"settings.{Guid.NewGuid():N}.tmp");

                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        File.WriteAllText(tempPath, json);
                        File.Move(tempPath, SettingsPath, overwrite: true);
                        return;
                    }
                    catch (IOException) when (attempt < 4)
                    {
                        System.Threading.Thread.Sleep(25);
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                    }
                }

                // Fallback direct write
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently ignore if disk/permission restricted
            }
        }
    }

    private static void EnsureValidProfiles(AppConfig config)
    {
        if (config.Profiles == null || config.Profiles.Count == 0)
        {
            config.Profiles = new List<Profile>
            {
                new Profile
                {
                    Name = "Default",
                    IntervalMs = config.IntervalMs > 0 ? config.IntervalMs : 50,
                    TriggerMode = config.TriggerMode,
                    HotkeyVkCode = config.HotkeyVkCode != 0 ? config.HotkeyVkCode : 0x75,
                    MouseButton = config.MouseButton,
                    PrimaryVkCode = config.PrimaryVkCode != 0 ? config.PrimaryVkCode : 1,
                    IsModifierEnabled = config.IsModifierEnabled,
                    ModifierButton = config.ModifierButton,
                    ModifierVkCode = config.ModifierVkCode != 0 ? config.ModifierVkCode : 2,
                    ModifierAction = config.ModifierAction,
                    ModifierOrder = config.ModifierOrder,
                    ModifierDelayMs = Math.Max(1, config.ModifierDelayMs),
                    BlockHotkey = config.BlockHotkey
                }
            };
            config.ActiveProfileName = "Default";
        }
        else
        {
            foreach (var p in config.Profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                {
                    p.Name = "Default";
                }
                if (p.IntervalMs <= 0)
                {
                    p.IntervalMs = config.IntervalMs > 0 ? config.IntervalMs : 50;
                }
                if (p.HotkeyVkCode <= 0)
                {
                    p.HotkeyVkCode = config.HotkeyVkCode != 0 ? config.HotkeyVkCode : 0x75;
                }
                if (p.PrimaryVkCode <= 0)
                {
                    p.PrimaryVkCode = config.PrimaryVkCode != 0 ? config.PrimaryVkCode : 1;
                }
                if (p.ModifierDelayMs <= 0)
                {
                    p.ModifierDelayMs = Math.Max(1, config.ModifierDelayMs);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(config.ActiveProfileName) ||
            !config.Profiles.Any(p => string.Equals(p.Name, config.ActiveProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            config.ActiveProfileName = config.Profiles[0].Name;
        }
    }
}
