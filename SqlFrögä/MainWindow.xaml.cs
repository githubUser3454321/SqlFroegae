using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SqlFroega.Views;
using System;
using System.IO;
using WinRT.Interop;

namespace SqlFroega;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(LoginView));
        SetWindowIcon();
    }


    public void NavigateToDashboard()
    {
        RootFrame.Navigate(typeof(LibrarySplitView));
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
