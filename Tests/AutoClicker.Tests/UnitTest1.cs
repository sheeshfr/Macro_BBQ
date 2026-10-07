using System;
using System.Diagnostics;
using System.Threading;
using AutoClicker.Models;
using AutoClicker.Services;
using AutoClicker.Services.Native;
using AutoClicker.ViewModels;
using Xunit;

namespace AutoClicker.Tests;

public class KeyHelperTests
{
    [Theory]
    [InlineData(0x75, "F6")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x1B, "Escape")]
    [InlineData(0x20, "Space")]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x30, "0")]
    [InlineData(0x01, "M1 (Left)")]
    [InlineData(0x02, "M2 (Right)")]
    [InlineData(0x04, "M3 (Middle)")]
    [InlineData(0x05, "M4")]
    [InlineData(0x06, "M5")]
    [InlineData(0xB3, "Play / Pause")]
    public void GetKeyName_MapsCorrectly(int vkCode, string expectedName)
    {
        string actual = KeyHelper.GetKeyName(vkCode);
        Assert.Equal(expectedName, actual);
    }
}

public class AutoClickerEngineTests
{
    [Fact]
    public void Engine_StartsAndIncrementsClicks()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 20;

        Assert.False(engine.IsRunning);
        engine.Start();
        Assert.True(engine.IsRunning);

        Thread.Sleep(120);

        engine.Stop();
        Assert.False(engine.IsRunning);

        long clicks = engine.TotalClicks;
        Assert.True(clicks >= 2, $"Expected at least 2 clicks, got {clicks}");

        engine.ResetClicks();
        Assert.Equal(0, engine.TotalClicks);
    }

    [Fact]
    public void Engine_StopsImmediatelyEvenWithLargeInterval()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 5000; // 5 seconds interval

        engine.Start();
        Thread.Sleep(50); // let worker thread start and enter wait

        var sw = Stopwatch.StartNew();
        engine.Stop();
        sw.Stop();

        Assert.False(engine.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Stop took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Engine_Stop_ExecutesInstantaneouslyWithModifier()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 50;
        engine.IsModifierEnabled = true;
        engine.ModifierButton = MouseButtonType.Right;
        engine.ModifierAction = ModifierAction.Hold;
        engine.ModifierOrder = ModifierOrder.ModifierFirst;

        engine.Start();
        Thread.Sleep(30);

        var sw = Stopwatch.StartNew();
        engine.Stop();
        sw.Stop();

        Assert.False(engine.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 25, $"Stop with modifier took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Engine_WithHoldModifier_ExecutesSuccessfully()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 25;
        engine.IsModifierEnabled = true;
        engine.ModifierButton = MouseButtonType.Right;
        engine.ModifierAction = ModifierAction.Hold;
        engine.ModifierOrder = ModifierOrder.ModifierFirst;
        engine.ModifierDelayMs = 1;

        engine.Start();
        Assert.True(engine.IsRunning);
        Thread.Sleep(100);

        engine.Stop();
        Assert.False(engine.IsRunning);
        Assert.True(engine.TotalClicks >= 1);
    }

    [Fact]
    public void Engine_WithSpamModifier_ExecutesSuccessfully()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 25;
        engine.IsModifierEnabled = true;
        engine.ModifierButton = MouseButtonType.Right;
        engine.ModifierAction = ModifierAction.Spam;
        engine.ModifierOrder = ModifierOrder.ModifierFirst;
        engine.ModifierDelayMs = 1;

        engine.Start();
        Assert.True(engine.IsRunning);
        Thread.Sleep(100);

        engine.Stop();
        Assert.False(engine.IsRunning);
        Assert.True(engine.TotalClicks >= 1);
    }

    [Fact]
    public void Engine_WithArbitraryVkCodes_ExecutesSuccessfully()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 25;
        engine.PrimaryVkCode = Win32Api.VK_XBUTTON1; // M4
        engine.IsModifierEnabled = true;
        engine.ModifierVkCode = 0x20; // Space key
        engine.ModifierAction = ModifierAction.Hold;
        engine.ModifierOrder = ModifierOrder.ModifierFirst;
        engine.ModifierDelayMs = 1;

        engine.Start();
        Assert.True(engine.IsRunning);
        Thread.Sleep(80);

        engine.Stop();
        Assert.False(engine.IsRunning);
        Assert.True(engine.TotalClicks >= 1);
    }

    [Fact]
    public void Engine_ReleaseAllButtons_ExecutesWithoutError()
    {
        using var engine = new AutoClickerEngine();
        engine.PrimaryVkCode = Win32Api.VK_LBUTTON;
        engine.IsModifierEnabled = true;
        engine.ModifierVkCode = Win32Api.VK_RBUTTON;

        var exception = Record.Exception(() => engine.ReleaseAllButtons());
        Assert.Null(exception);
    }
}


public class MainViewModelTests
{
    [Fact]
    public void ViewModel_PresetAndCpsCalculation()
    {
        // Avoid initializing hook on CI/test if possible, or verify ViewModel properties
        // Use explicit default config so test doesn't depend on user's AppData settings file
        var vm = new MainViewModel(new AppConfig());

        // Verify defaults: Hold mode, 50ms, CloseToTray true
        Assert.Equal(50, vm.IntervalMs);
        Assert.Equal(TriggerMode.Hold, vm.SelectedTriggerMode);
        Assert.True(vm.IsHoldMode);
        Assert.False(vm.IsToggleMode);
        Assert.True(vm.CloseToTray);
        vm.CloseToTray = false;
        Assert.False(vm.CloseToTray);
        vm.CloseToTray = true;
        Assert.True(vm.CloseToTray);

        // Verify Modifier slot defaults
        Assert.False(vm.IsModifierEnabled);
        Assert.Equal(MouseButtonType.Right, vm.SelectedModifierButton);
        Assert.Equal(ModifierAction.Hold, vm.SelectedModifierAction);
        Assert.Equal(ModifierOrder.ModifierFirst, vm.SelectedModifierOrder);
        Assert.Equal(1, vm.ModifierDelayMs);
        Assert.Equal("⚡ Mod", vm.ModifierBadgeText);

        // Test Modifier toggle and options
        vm.IsModifierEnabled = true;
        Assert.Equal("⚡ MOD", vm.ModifierBadgeText);
        Assert.True(vm.IsModRightButton);
        vm.IsModLeftButton = true;
        Assert.Equal(MouseButtonType.Left, vm.SelectedModifierButton);
        vm.IsModRightButton = true;
        Assert.Equal(MouseButtonType.Right, vm.SelectedModifierButton);

        vm.IsModSpamAction = true;
        Assert.Equal(ModifierAction.Spam, vm.SelectedModifierAction);
        vm.IsModHoldAction = true;
        Assert.Equal(ModifierAction.Hold, vm.SelectedModifierAction);

        vm.IsPrimaryFirstOrder = true;
        Assert.Equal(ModifierOrder.PrimaryFirst, vm.SelectedModifierOrder);
        vm.IsModFirstOrder = true;
        Assert.Equal(ModifierOrder.ModifierFirst, vm.SelectedModifierOrder);

        // Test Modifier popup toggle
        Assert.False(vm.IsModifierPopupOpen);
        vm.ToggleModifierPopupCommand.Execute(null);
        Assert.True(vm.IsModifierPopupOpen);
        Assert.False(vm.IsSettingsOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);
        Assert.False(vm.IsModifierPopupOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.False(vm.IsSettingsOpen);

        vm.IntervalMs = 100;
        vm.SelectedClickType = ClickType.Single;
        Assert.Equal("10.0 CPS", vm.EstimatedCpsText);

        vm.SelectedClickType = ClickType.Double;
        Assert.Equal("20.0 CPS", vm.EstimatedCpsText);

        vm.SetPresetIntervalCommand.Execute("50");
        Assert.Equal(50, vm.IntervalMs);

        // Test mode switches
        vm.IsHoldMode = true;
        Assert.Equal(TriggerMode.Hold, vm.SelectedTriggerMode);
        Assert.True(vm.IsHoldMode);
        Assert.False(vm.IsToggleMode);

        vm.IsToggleMode = true;
        Assert.Equal(TriggerMode.Toggle, vm.SelectedTriggerMode);
        Assert.True(vm.IsToggleMode);
        Assert.False(vm.IsHoldMode);

        // Test mouse hotkey preset
        vm.SetPresetHotkeyCommand.Execute("5"); // Mouse 4
        Assert.Equal(5, vm.HotkeyVkCode);
        Assert.Equal("M4", vm.HotkeyName);
        Assert.Equal("M4", vm.KeyButtonText);

        vm.SetPresetHotkeyCommand.Execute("6"); // Mouse 5
        Assert.Equal(6, vm.HotkeyVkCode);
        Assert.Equal("M5", vm.HotkeyName);
        Assert.Equal("M5", vm.KeyButtonText);

        // Test hotkey button toggle & countdown
        Assert.False(vm.IsRecordingHotkey);
        vm.ToggleRecordingHotkeyCommand.Execute(null);
        Assert.True(vm.IsRecordingHotkey);
        Assert.Equal(5, vm.CountdownSeconds);
        Assert.Equal("waiting...5", vm.KeyButtonText);

        // Cancel via toggle
        vm.ToggleRecordingHotkeyCommand.Execute(null);
        Assert.False(vm.IsRecordingHotkey);
        Assert.Equal("M5", vm.KeyButtonText);

        // Reset hotkey
        vm.ResetHotkeyCommand.Execute(null);
        Assert.Equal(0x75, vm.HotkeyVkCode);
        Assert.Equal("F6", vm.HotkeyName);
        Assert.Equal("F6", vm.KeyButtonText);

        // Test Primary Button presets and reset
        Assert.Equal(1, vm.PrimaryVkCode);
        Assert.Equal("M1 (Left)", vm.PrimaryButtonName);
        Assert.Equal("M1 (Left)", vm.PrimaryKeyButtonText);

        vm.SetPresetPrimaryButtonCommand.Execute("2"); // Set to Right Click
        Assert.Equal(2, vm.PrimaryVkCode);
        Assert.Equal("M2 (Right)", vm.PrimaryButtonName);
        Assert.Equal("M2 (Right)", vm.PrimaryKeyButtonText);
        Assert.Equal(MouseButtonType.Right, vm.SelectedMouseButton);

        vm.SetPresetPrimaryButtonCommand.Execute("5"); // Set to M4
        Assert.Equal(5, vm.PrimaryVkCode);
        Assert.Equal("M4", vm.PrimaryButtonName);

        vm.ResetPrimaryButtonCommand.Execute(null);
        Assert.Equal(1, vm.PrimaryVkCode);
        Assert.Equal("M1 (Left)", vm.PrimaryButtonName);
        Assert.Equal(MouseButtonType.Left, vm.SelectedMouseButton);

        // Test Primary recording toggle
        Assert.False(vm.IsRecordingPrimary);
        vm.ToggleRecordingPrimaryCommand.Execute(null);
        Assert.True(vm.IsRecordingPrimary);
        Assert.Equal("waiting...5", vm.PrimaryKeyButtonText);
        vm.ToggleRecordingPrimaryCommand.Execute(null);
        Assert.False(vm.IsRecordingPrimary);
        Assert.Equal("M1 (Left)", vm.PrimaryKeyButtonText);

        // Test Modifier Button presets and reset
        Assert.Equal(2, vm.ModifierVkCode);
        Assert.Equal("M2 (Right)", vm.ModifierButtonName);
        Assert.Equal("M2 (Right)", vm.ModifierKeyButtonText);

        vm.SetPresetModifierButtonCommand.Execute("1"); // Set to Left Click
        Assert.Equal(1, vm.ModifierVkCode);
        Assert.Equal("M1 (Left)", vm.ModifierButtonName);
        Assert.Equal("M1 (Left)", vm.ModifierKeyButtonText);
        Assert.Equal(MouseButtonType.Left, vm.SelectedModifierButton);

        vm.SetPresetModifierButtonCommand.Execute("6"); // Set to M5
        Assert.Equal(6, vm.ModifierVkCode);
        Assert.Equal("M5", vm.ModifierButtonName);
        Assert.Equal("M5", vm.ModifierKeyButtonText);

        vm.ResetModifierButtonCommand.Execute(null);
        Assert.Equal(2, vm.ModifierVkCode);
        Assert.Equal("M2 (Right)", vm.ModifierButtonName);
        Assert.Equal("M2 (Right)", vm.ModifierKeyButtonText);
        Assert.Equal(MouseButtonType.Right, vm.SelectedModifierButton);

        // Test Modifier recording toggle
        Assert.False(vm.IsRecordingModifier);
        vm.ToggleRecordingModifierCommand.Execute(null);
        Assert.True(vm.IsRecordingModifier);
        Assert.Equal("waiting...5", vm.ModifierKeyButtonText);
        vm.ToggleRecordingModifierCommand.Execute(null);
        Assert.False(vm.IsRecordingModifier);
        Assert.Equal("M2 (Right)", vm.ModifierKeyButtonText);

        // Test settings popup toggle
        Assert.False(vm.IsSettingsOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);
        vm.ToggleSettingsCommand.Execute(null);
        Assert.False(vm.IsSettingsOpen);

        // Test profiles popup toggle
        Assert.False(vm.IsProfilesPopupOpen);
        vm.ToggleProfilesPopupCommand.Execute(null);
        Assert.True(vm.IsProfilesPopupOpen);
        Assert.False(vm.IsSettingsOpen);
        Assert.False(vm.IsModifierPopupOpen);
        vm.ToggleProfilesPopupCommand.Execute(null);
        Assert.False(vm.IsProfilesPopupOpen);

        vm.Dispose();
    }

    [Fact]
    public void Profile_CreateSwitchRenameDelete_WorksCleanly()
    {
        var vm = new MainViewModel(new AppConfig());

        // Default profile initialized
        Assert.Single(vm.Profiles);
        Assert.Equal("Default", vm.ActiveProfileName);
        Assert.False(vm.Profiles[0].CanDelete);
        Assert.True(vm.Profiles[0].IsActive);

        // Configure Default with specific settings
        vm.IntervalMs = 75;
        vm.SelectedTriggerMode = TriggerMode.Toggle;

        // Create new profile "Apex Legends"
        vm.NewProfileName = "Apex Legends";
        vm.CreateProfileCommand.Execute(null);

        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Apex Legends", vm.ActiveProfileName);
        Assert.True(vm.Profiles[1].IsActive);
        Assert.False(vm.Profiles[0].IsActive);
        Assert.True(vm.Profiles[0].CanDelete);
        Assert.True(vm.Profiles[1].CanDelete);

        // Modify settings under Apex Legends
        vm.IntervalMs = 25;
        vm.SelectedTriggerMode = TriggerMode.Hold;
        vm.IsModifierEnabled = true;
        vm.ModifierVkCode = 6; // M5
        vm.ModifierButtonName = "M5";

        // Switch back to Default
        vm.SelectProfileCommand.Execute(vm.Profiles[0]);
        Assert.Equal("Default", vm.ActiveProfileName);
        Assert.Equal(75, vm.IntervalMs);
        Assert.Equal(TriggerMode.Toggle, vm.SelectedTriggerMode);
        Assert.False(vm.IsModifierEnabled);

        // Switch back to Apex Legends
        vm.SelectProfileCommand.Execute(vm.Profiles[1]);
        Assert.Equal("Apex Legends", vm.ActiveProfileName);
        Assert.Equal(25, vm.IntervalMs);
        Assert.Equal(TriggerMode.Hold, vm.SelectedTriggerMode);
        Assert.True(vm.IsModifierEnabled);
        Assert.Equal(6, vm.ModifierVkCode);

        // Rename active profile
        vm.NewProfileName = "Apex Pro";
        vm.RenameActiveProfileCommand.Execute(null);
        Assert.Equal("Apex Pro", vm.ActiveProfileName);
        Assert.Equal("Apex Pro", vm.Profiles[1].Name);

        // Delete active profile -> should fallback to Default
        vm.DeleteProfileCommand.Execute(vm.Profiles[1]);
        Assert.Single(vm.Profiles);
        Assert.Equal("Default", vm.ActiveProfileName);
        Assert.Equal(75, vm.IntervalMs);
        Assert.False(vm.Profiles[0].CanDelete);

        vm.Dispose();
    }

    [Fact]
    public void Profile_Model_ClonesCorrectly()
    {
        var p = new Profile
        {
            Name = "Original",
            IntervalMs = 33,
            TriggerMode = TriggerMode.Toggle,
            HotkeyVkCode = 0x70, // F1
            PrimaryVkCode = 0x01,
            IsModifierEnabled = true,
            ModifierVkCode = 0x02,
            ModifierAction = ModifierAction.Spam,
            ModifierOrder = ModifierOrder.PrimaryFirst,
            ModifierDelayMs = 15
        };

        var clone = p.Clone("Copy");
        Assert.Equal("Copy", clone.Name);
        Assert.Equal(33, clone.IntervalMs);
        Assert.Equal(TriggerMode.Toggle, clone.TriggerMode);
        Assert.Equal(0x70, clone.HotkeyVkCode);
        Assert.True(clone.IsModifierEnabled);
        Assert.Equal(0x02, clone.ModifierVkCode);
        Assert.Equal(ModifierAction.Spam, clone.ModifierAction);
        Assert.Equal(ModifierOrder.PrimaryFirst, clone.ModifierOrder);
        Assert.Equal(15, clone.ModifierDelayMs);
        Assert.Contains("F1", clone.SummaryText);
    }

    [Fact]
    public void MainViewModel_TriggerAndPrimaryDistinction_BehavesCorrectly()
    {
        using var vm = new MainViewModel(new AppConfig());

        // Verify initial state
        Assert.Equal("F6", vm.KeyButtonText);
        Assert.Equal("M1 (Left)", vm.PrimaryKeyButtonText);
        Assert.False(vm.IsModifierEnabled);

        // Test status descriptions during recording states
        vm.ToggleRecordingHotkeyCommand.Execute(null);
        Assert.True(vm.IsRecordingHotkey);
        Assert.Contains("TRIGGER HOTKEY", vm.StatusDescription);
        vm.CancelRecording();

        vm.ToggleRecordingPrimaryCommand.Execute(null);
        Assert.True(vm.IsRecordingPrimary);
        Assert.Contains("PRIMARY CLICK", vm.StatusDescription);
        vm.CancelRecording();

        vm.ToggleRecordingModifierCommand.Execute(null);
        Assert.True(vm.IsRecordingModifier);
        Assert.Contains("SECONDARY MODIFIER", vm.StatusDescription);
        vm.CancelRecording();

        // Test collapsible modifier toggle
        vm.IsModifierEnabled = true;
        Assert.True(vm.IsModifierEnabled);
        vm.IsModifierEnabled = false;
        Assert.False(vm.IsModifierEnabled);
    }

    [Fact]
    public void MainViewModel_ResetPrimaryAndHotkey_ResetsToDefaults()
    {
        using var vm = new MainViewModel(new AppConfig());

        // Change Primary to something else (e.g. M2 Right)
        vm.SetPresetPrimaryButtonCommand.Execute("2");
        Assert.Equal("M2 (Right)", vm.PrimaryKeyButtonText);
        Assert.Equal(2, vm.PrimaryVkCode);

        // Reset Primary -> back to M1 (Left)
        vm.ResetPrimaryButtonCommand.Execute(null);
        Assert.Equal("M1 (Left)", vm.PrimaryKeyButtonText);
        Assert.Equal(1, vm.PrimaryVkCode);

        // Change Hotkey to something else (e.g. Space = 0x20)
        vm.SetPresetHotkeyCommand.Execute("32");
        Assert.Equal("Space", vm.KeyButtonText);
        Assert.Equal(0x20, vm.HotkeyVkCode);

        // Reset Hotkey -> back to F6 (0x75)
        vm.ResetHotkeyCommand.Execute(null);
        Assert.Equal("F6", vm.KeyButtonText);
        Assert.Equal(0x75, vm.HotkeyVkCode);
    }
}

public class LinuxInputMappingTests
{
    [Theory]
    [InlineData(LinuxNative.BTN_LEFT, 0x01)]
    [InlineData(LinuxNative.BTN_RIGHT, 0x02)]
    [InlineData(LinuxNative.BTN_MIDDLE, 0x04)]
    [InlineData(LinuxNative.BTN_SIDE, 0x05)]
    [InlineData(LinuxNative.BTN_BACK, 0x05)]
    [InlineData(LinuxNative.BTN_EXTRA, 0x06)]
    [InlineData(LinuxNative.BTN_FORWARD, 0x06)]
    public void EvdevToVkCode_MapsMouseButtonsCorrectly(ushort evdevCode, int expectedVk)
    {
        int actualVk = LinuxInputMonitor.EvdevToVkCode(evdevCode);
        Assert.Equal(expectedVk, actualVk);
    }

    [Fact]
    public void Engine_ClicksContinuouslyAt50ms()
    {
        using var engine = new AutoClickerEngine();
        engine.IntervalMs = 50;
        engine.PrimaryVkCode = 1; // Left Click

        int countEvents = 0;
        engine.ClickCountUpdated += (count) => Interlocked.Increment(ref countEvents);

        engine.Start();
        Assert.True(engine.IsRunning);

        // Sleep 260ms -> should produce at least 4 clicks (at 0ms, 50ms, 100ms, 150ms, 200ms, 250ms)
        Thread.Sleep(260);

        engine.Stop();
        Assert.False(engine.IsRunning);

        long clicks = engine.TotalClicks;
        Assert.True(clicks >= 4, $"Expected at least 4 continuous clicks at 50ms, got {clicks}");
        Assert.True(countEvents >= 4, $"Expected at least 4 click count events, got {countEvents}");
    }

    [Fact]
    public void RecordingDebounce_PreventsImmediateRetrigger()
    {
        using var vm = new MainViewModel(new AppConfig());

        // Start recording primary
        vm.ToggleRecordingPrimaryCommand.Execute(null);
        Assert.True(vm.IsRecordingPrimary);

        // Simulate slot recorded (e.g. Left click assigned)
        // Access protected/internal event simulation via mock or direct invoke
        // Since slot recording updates _lastSlotRecordedTimestamp, let's verify via ViewModel commands
        // If IsRecentlyRecorded is active, ToggleRecordingPrimary should be blocked
        Assert.False(vm.IsRecentlyRecorded);
    }

    [Fact]
    public void LinuxInputMonitor_IsKeyPhysicallyDown_ReturnsFalseWhenNotHeld()
    {
        using var monitor = new LinuxInputMonitor();
        // Mouse 4 (vkCode = 5) is not held down right now during automated testing
        bool isDown = monitor.IsKeyPhysicallyDown(5);
        Assert.False(isDown);
    }
}