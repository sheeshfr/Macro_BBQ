using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AutoClicker.ViewModels;

namespace AutoClicker.Views;

public partial class MainWindow : Window
{
    private bool _isForceClose = false;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        Closed += OnClosed;

        TriggerKeyButton.AddHandler(InputElement.PointerPressedEvent, OnTriggerButtonPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        PrimaryKeyButton.AddHandler(InputElement.PointerPressedEvent, OnPrimaryButtonPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isForceClose)
        {
            return;
        }

        if (DataContext is MainViewModel vm && vm.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
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
}