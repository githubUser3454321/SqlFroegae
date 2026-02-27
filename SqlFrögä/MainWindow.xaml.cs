using Microsoft.UI.Xaml;
using SqlFroega.Views;

namespace SqlFroega;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(LibrarySplitView));
    }
}
