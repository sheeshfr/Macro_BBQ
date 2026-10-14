using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using AutoClicker.Services.Native;

namespace AutoClicker.Models;

public class Profile : ObservableObject
{
    private string _name = "Default";
    public string Name
    {
        get => _name;
        set { SetProperty(ref _name, value); OnPropertyChanged(nameof(SummaryText)); }
    }

    private int _intervalMs = 50;
    public int IntervalMs
    {
        get => _intervalMs;
        set { SetProperty(ref _intervalMs, value); OnPropertyChanged(nameof(SummaryText)); }
    }

    private TriggerMode _triggerMode = TriggerMode.Hold;
    public TriggerMode TriggerMode
    {
        get => _triggerMode;
        set { SetProperty(ref _triggerMode, value); OnPropertyChanged(nameof(SummaryText)); }
    }

    private int _hotkeyVkCode = 0x75; // F6
    public int HotkeyVkCode
    {
        get => _hotkeyVkCode;
        set { SetProperty(ref _hotkeyVkCode, value); OnPropertyChanged(nameof(SummaryText)); }
    }

    private MouseButtonType _mouseButton = MouseButtonType.Left;
    public MouseButtonType MouseButton
    {
        get => _mouseButton;
        set => SetProperty(ref _mouseButton, value);
    }

    private int _primaryVkCode = 0x01; // Left Click
    public int PrimaryVkCode
    {
        get => _primaryVkCode;
        set => SetProperty(ref _primaryVkCode, value);
    }

    private bool _isModifierEnabled = false;
    public bool IsModifierEnabled
    {
        get => _isModifierEnabled;
        set { SetProperty(ref _isModifierEnabled, value); OnPropertyChanged(nameof(SummaryText)); }
    }

    private MouseButtonType _modifierButton = MouseButtonType.Right;
    public MouseButtonType ModifierButton
    {
        get => _modifierButton;
        set => SetProperty(ref _modifierButton, value);
    }

    private int _modifierVkCode = 0x02; // Right Click
    public int ModifierVkCode
    {
        get => _modifierVkCode;
        set => SetProperty(ref _modifierVkCode, value);
    }

    private ModifierAction _modifierAction = ModifierAction.Hold;
    public ModifierAction ModifierAction
    {
        get => _modifierAction;
        set => SetProperty(ref _modifierAction, value);
    }

    private ModifierOrder _modifierOrder = ModifierOrder.ModifierFirst;
    public ModifierOrder ModifierOrder
    {
        get => _modifierOrder;
        set => SetProperty(ref _modifierOrder, value);
    }

    private int _modifierDelayMs = 1;
    public int ModifierDelayMs
    {
        get => _modifierDelayMs;
        set => SetProperty(ref _modifierDelayMs, value);
    }

    private bool _blockHotkey = false;
    public bool BlockHotkey
    {
        get => _blockHotkey;
        set => SetProperty(ref _blockHotkey, value);
    }

    // Runtime UI state (not serialized to JSON)
    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
    private bool _isActive = false;

    [JsonIgnore]
    public bool CanDelete
    {
        get => _canDelete;
        set => SetProperty(ref _canDelete, value);
    }
    private bool _canDelete = false;

    [JsonIgnore]
    public string SummaryText
    {
        get
        {
            string key = KeyHelper.GetKeyName(HotkeyVkCode);
            string mode = TriggerMode == TriggerMode.Hold ? "Hold" : "Toggle";
            string mod = IsModifierEnabled ? " • Mod" : "";
            return $"{key} • {IntervalMs}ms • {mode}{mod}";
        }
    }

    public Profile Clone(string newName)
    {
        return new Profile
        {
            Name = newName,
            IntervalMs = this.IntervalMs,
            TriggerMode = this.TriggerMode,
            HotkeyVkCode = this.HotkeyVkCode,
            MouseButton = this.MouseButton,
            PrimaryVkCode = this.PrimaryVkCode,
            IsModifierEnabled = this.IsModifierEnabled,
            ModifierButton = this.ModifierButton,
            ModifierVkCode = this.ModifierVkCode,
            ModifierAction = this.ModifierAction,
            ModifierOrder = this.ModifierOrder,
            ModifierDelayMs = this.ModifierDelayMs,
            BlockHotkey = this.BlockHotkey
        };
    }
}
