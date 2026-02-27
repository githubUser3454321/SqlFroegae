using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using SqlFroega.ViewModels;

namespace SqlFroega.Views;

public sealed partial class LibraryView : Page
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private LibraryViewModel VM => (LibraryViewModel)DataContext;

    private async void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await VM.SearchCommand.ExecuteAsync(null);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var text = VM.SelectedDetail?.Content;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }
}