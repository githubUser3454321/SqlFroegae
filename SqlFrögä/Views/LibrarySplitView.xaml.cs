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

    private async void DeleteModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string moduleName })
            return;

        var shouldDelete = await ConfirmDeleteModuleAsync(moduleName);
        if (!shouldDelete)
            return;

        await VM.RemoveModuleCommand.ExecuteAsync(moduleName);
    }


    private async Task<bool> ConfirmDeleteModuleAsync(string moduleName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Modul wirklich löschen?",
            Content = $"Das Modul '{moduleName}' wird aus der Modulverwaltung entfernt und aus Main-/Related-Modulen aller Scripts gelöscht.",
            PrimaryButtonText = "Modul löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
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

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
