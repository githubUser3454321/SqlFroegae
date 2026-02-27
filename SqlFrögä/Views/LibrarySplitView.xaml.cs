using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SqlFroega.ViewModels;
using System;
using Windows.System;

namespace SqlFroega.Views;

public sealed partial class LibrarySplitView : Page
{
    public LibrarySplitView()
    {
        InitializeComponent();

        // Frame in VM registrieren, damit VM rechts navigieren kann.
        VM.AttachDetailFrame(DetailFrame);

        // Startzustand rechts: "New" oder leer
        DetailFrame.Navigate(typeof(ScriptItemView), Guid.Empty);
    }

    private LibrarySplitViewModel VM => (LibrarySplitViewModel)DataContext;

    private async void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await VM.SearchCommand.ExecuteAsync(null);
        }
    }
}