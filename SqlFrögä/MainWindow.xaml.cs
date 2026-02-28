using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SqlFroega.Views;
using WinRT.Interop;

namespace SqlFroega;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(LibrarySplitView));
        SetWindowIcon();
    }

    private void SetWindowIcon()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SqlFrögä.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }
}
