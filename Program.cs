using Avalonia;
using System;
using System.IO;
using System.Threading;

namespace AutoClicker;

sealed class Program
{
    private const string AppMutexName = "MacroBBQ_SingleInstanceMutex";
    private const string WakeEventName = "MacroBBQ_WakeEvent";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Mutex? mutex = null;
        bool hasHandle = false;

        try
        {
            mutex = new Mutex(false, AppMutexName);
            try
            {
                hasHandle = mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                // Previous process terminated unexpectedly, mutex acquired
                hasHandle = true;
            }

            if (!hasHandle)
            {
                // Another instance is already actively running. Signal it to restore/show its window.
                try
                {
                    using var wakeEvent = EventWaitHandle.OpenExisting(WakeEventName);
                    wakeEvent.Set();
                }
                catch
                {
                    // Fallback if handle wasn't open
                }
                return;
            }

            using var wakeHandle = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
            StartWakeListener(wakeHandle);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (hasHandle && mutex != null)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore during exit
                }
            }
            mutex?.Dispose();
        }
    }

    private static void StartWakeListener(EventWaitHandle wakeHandle)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (wakeHandle.WaitOne())
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (Application.Current is App app)
                            {
                                app.ShowMainWindow();
                            }
                        });
                    }
                }
                catch
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "WakeListenerThread"
        };
        thread.Start();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

