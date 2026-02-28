using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SqlFroega.Application.Abstractions;
using SqlFroega.Views;
using System;
using System.IO;
using WinRT.Interop;

namespace SqlFroega;

public sealed partial class MainWindow : Window
{
    private bool _startupNavigationDone;

    public MainWindow()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            throw new InvalidOperationException(
                "Hoppla! Diese Funktion benötigt Windows 10 (Version 1809) [mindestens 10.0.17763]oder neuer. Bitte aktualisiere dein Betriebssystem, um dieses Feature nutzen zu können.");
        }

        InitializeComponent();
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        SetWindowIcon();
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            var scriptRepository = App.Services.GetRequiredService<IScriptRepository>();
            await scriptRepository.ClearEditLocksAsync(App.CurrentUser?.Username);
        }
        catch
        {
            // Ignore cleanup errors during shutdown.
        }
    }

    public void NavigateToDashboard()
    {
        RootFrame.Navigate(typeof(LibrarySplitView));
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_startupNavigationDone)
        {
            return;
        }

        _startupNavigationDone = true;
        Activated -= MainWindow_Activated;

        var userRepository = App.Services.GetRequiredService<IUserRepository>();
        var scriptRepository = App.Services.GetRequiredService<IScriptRepository>();
        var autoLoginUser = await userRepository.FindActiveByCurrentDeviceAsync();

        if (autoLoginUser is not null)
        {
            App.CurrentUser = autoLoginUser;
            await scriptRepository.ClearEditLocksAsync(App.CurrentUser?.Username);
            NavigateToDashboard();
            return;
        }

        RootFrame.Navigate(typeof(LoginView));
    }

    private void SetWindowIcon()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SqlFroga.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }
}
