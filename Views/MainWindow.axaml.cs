using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AutoClicker.ViewModels;

namespace AutoClicker.Views;

public partial class MainWindow : Window
{
    private bool _isForceClose = false;
    private DispatcherTimer? _positionSaveTimer;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
        Opened += OnOpened;
        PositionChanged += OnPositionChanged;

        TriggerKeyButton.AddHandler(InputElement.PointerPressedEvent, OnTriggerButtonPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        PrimaryKeyButton.AddHandler(InputElement.PointerPressedEvent, OnPrimaryButtonPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

        if (IntervalNumericUpDown != null)
        {
            IntervalNumericUpDown.AddHandler(InputElement.KeyUpEvent, (s, e) =>
            {
                if (DataContext is MainViewModel vm && int.TryParse(IntervalNumericUpDown.Text, out int val) && val >= 1)
                {
                    if (vm.IntervalMs != val)
                    {
                        vm.IntervalMs = val;
                    }
                }
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            IntervalNumericUpDown.LostFocus += (s, e) => FlushNumericInputs();
        }

        if (ModifierDelayNumericUpDown != null)
        {
            ModifierDelayNumericUpDown.AddHandler(InputElement.KeyUpEvent, (s, e) =>
            {
                if (DataContext is MainViewModel vm && int.TryParse(ModifierDelayNumericUpDown.Text, out int val) && val >= 1)
                {
                    if (vm.ModifierDelayMs != val)
                    {
                        vm.ModifierDelayMs = val;
                    }
                }
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            ModifierDelayNumericUpDown.LostFocus += (s, e) => FlushNumericInputs();
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.WindowPositionX.HasValue && vm.WindowPositionY.HasValue)
        {
            var savedPoint = new Avalonia.PixelPoint(vm.WindowPositionX.Value, vm.WindowPositionY.Value);
            bool isOnScreen = Screens.All.Any(s => s.WorkingArea.Contains(savedPoint));
            if (isOnScreen)
            {
                Position = savedPoint;
            }
        }
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_positionSaveTimer == null)
        {
            _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _positionSaveTimer.Tick += (s, ev) =>
            {
                _positionSaveTimer.Stop();
                if (DataContext is MainViewModel vm)
                {
                    vm.WindowPositionX = Position.X;
                    vm.WindowPositionY = Position.Y;
                }
            };
        }
        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        FlushNumericInputs();
        if (DataContext is MainViewModel vm)
        {
            vm.SaveConfig();
        }
    }

    private void FlushNumericInputs()
    {
        if (DataContext is not MainViewModel vm) return;

        if (IntervalNumericUpDown != null && int.TryParse(IntervalNumericUpDown.Text, out int intervalVal))
        {
            if (intervalVal >= 1 && vm.IntervalMs != intervalVal)
            {
                vm.IntervalMs = intervalVal;
            }
        }

        if (ModifierDelayNumericUpDown != null && int.TryParse(ModifierDelayNumericUpDown.Text, out int delayVal))
        {
            if (delayVal >= 1 && vm.ModifierDelayMs != delayVal)
            {
                vm.ModifierDelayMs = delayVal;
            }
        }
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        FlushNumericInputs();
        if (DataContext is MainViewModel vm)
        {
            vm.WindowPositionX = Position.X;
            vm.WindowPositionY = Position.Y;
            vm.SaveConfig();
        }

        if (_isForceClose)
        {
            return;
        }

        if (DataContext is MainViewModel vm2 && vm2.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        FlushNumericInputs();
        if (DataContext is MainViewModel vm)
        {
            vm.WindowPositionX = Position.X;
            vm.WindowPositionY = Position.Y;
            vm.SaveConfig();
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnTriggerButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsRightButtonPressed)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ResetHotkey();
            }
            e.Handled = true;
        }
    }

    private void OnPrimaryButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsRightButtonPressed)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ResetPrimaryButton();
            }
            e.Handled = true;
        }
    }

    private void OnNewProfileTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && DataContext is MainViewModel vm)
        {
            vm.CreateProfileCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsRecordingAny)
        {
            // Swallow keyboard keys (such as Spacebar, Enter, etc.) while recording
            // so Avalonia UI controls do not interpret them as button triggers
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && (vm.IsRecordingAny || vm.IsRecentlyRecorded))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }
}