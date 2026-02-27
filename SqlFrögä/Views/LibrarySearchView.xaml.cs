using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SqlFroega.ViewModels;
using Windows.System;

namespace SqlFroega.Views;

public sealed partial class LibrarySearchView : Page
{
    public LibrarySearchView()
    {
        InitializeComponent();
    }

    private LibrarySearchViewModel VM => (LibrarySearchViewModel)DataContext;

    private async void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await VM.SearchCommand.ExecuteAsync(null);
        }
    }
}