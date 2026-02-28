using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SqlFroega.ViewModels;
using System;
using System.Threading.Tasks;
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

        Loaded += LibrarySplitView_Loaded;
    }

    private LibrarySplitViewModel VM => (LibrarySplitViewModel)DataContext;


    private async void LibrarySplitView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LibrarySplitView_Loaded;
        await VM.RefreshCatalogCommand.ExecuteAsync(null);
    }

    private async void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await VM.SearchCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid scriptId })
            return;

        var shouldDelete = await ConfirmDeleteAsync();
        if (!shouldDelete)
            return;

        await VM.DeleteCommand.ExecuteAsync(scriptId);
    }


    private void CustomerMappingAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenCustomerMappingAdminCommand.Execute(null);
    }

    private void ModuleAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenModuleAdminCommand.Execute(null);
    }


    private void UserManagementAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenUserManagementAdminCommand.Execute(null);
    }
    private async Task<bool> ConfirmDeleteAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Script wirklich löschen?",
            Content = "Diese Aktion entfernt das Script dauerhaft.",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };
        dialog.CloseButtonStyle = App.Current.Resources["LightBluePrimaryDialogButton"] as Style;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
