using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlFroega.ViewModels;
using System.Threading.Tasks;

namespace SqlFroega.Views;

public sealed partial class ModuleAdminView : Page
{
    public ModuleAdminView()
    {
        InitializeComponent();
        Loaded += ModuleAdminView_Loaded;
    }

    private ModuleAdminViewModel VM => (ModuleAdminViewModel)DataContext;

    private async void ModuleAdminView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ModuleAdminView_Loaded;
        await VM.LoadCommand.ExecuteAsync(null);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(VM.Selected))
            return;

        var shouldDelete = await ConfirmDeleteModuleAsync(VM.Selected);
        if (!shouldDelete)
            return;

        await VM.DeleteCommand.ExecuteAsync(null);
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
}
