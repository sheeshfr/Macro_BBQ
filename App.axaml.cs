using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoClicker.ViewModels;
using AutoClicker.Views;

namespace AutoClicker;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = vm,
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuOpen_Click(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuExit_Click(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    public void ShowMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window window)
        {
            window.Show();
            if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                window.WindowState = Avalonia.Controls.WindowState.Normal;
            }

            var primaryScreen = window.Screens.Primary ?? System.Linq.Enumerable.FirstOrDefault(window.Screens.All);
            if (primaryScreen != null)
            {
                var workArea = primaryScreen.WorkingArea;
                if (!workArea.Contains(window.Position))
                {
                    int w = (int)(window.Bounds.Width > 0 ? window.Bounds.Width : 330);
                    int h = (int)(window.Bounds.Height > 0 ? window.Bounds.Height : 320);
                    window.Position = new Avalonia.PixelPoint(workArea.X + (workArea.Width - w) / 2, workArea.Y + (workArea.Height - h) / 2);
                }
            }

            window.Activate();
            window.Topmost = true;

            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                AutoClicker.Services.Native.Win32Api.ForceForegroundWindow(handle);
            }
        }
    }

    private void ExitApplication()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ForceClose();
            }
            desktop.Shutdown();
        }
    }
}