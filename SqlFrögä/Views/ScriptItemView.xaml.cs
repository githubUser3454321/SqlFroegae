using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SqlFroega.Views;

public sealed partial class ScriptItemView : Page
{
    public ScriptItemView()
    {
        InitializeComponent();
    }

    private ScriptItemViewModel VM => (ScriptItemViewModel)DataContext;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Guid id)
            await VM.LoadAsync(id);
    }

    private void MainModuleAutoSuggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
            return;

        var typed = sender.Text?.Trim() ?? string.Empty;
        sender.ItemsSource = VM.AvailableModules
            .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .ToList();
    }

    private void MainModuleAutoSuggest_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string moduleName)
            VM.MainModule = moduleName;
    }

    private async void EditRelatedModules_Click(object sender, RoutedEventArgs e) => await ShowRelatedModulesDialogAsync();

    private async void RelatedModulesTextBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (!VM.IsReadOnlyMode)
            await ShowRelatedModulesDialogAsync();
    }

    private async Task ShowRelatedModulesDialogAsync()
    {
        var selectedModule = string.Empty;
        var selectedModules = new ObservableCollection<string>(VM.SelectedRelatedModules);

        var search = new AutoSuggestBox { PlaceholderText = "Modul suchen", Width = 420 };
        search.TextChanged += (_, e) =>
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
                return;

            var typed = search.Text?.Trim() ?? string.Empty;
            search.ItemsSource = VM.AvailableModules
                .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Take(25)
                .ToList();
        };

        search.SuggestionChosen += (_, e) =>
        {
            if (e.SelectedItem is string moduleName)
            {
                selectedModule = moduleName;
                search.Text = moduleName;
            }
        };

        var modulesPanel = new StackPanel { Spacing = 6 };
        var modulesScroller = new ScrollViewer
        {
            Height = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = modulesPanel
        };

        async Task RemoveModuleAsync(string moduleName)
        {
            var confirmDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Related Modul entfernen?",
                Content = $"Soll das Modul '{moduleName}' aus der Related-Liste entfernt werden?",
                PrimaryButtonText = "Entfernen",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            var existing = selectedModules.FirstOrDefault(x => string.Equals(x, moduleName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                selectedModules.Remove(existing);
                RenderModuleRows();
            }
        }

        void RenderModuleRows()
        {
            modulesPanel.Children.Clear();
            foreach (var module in selectedModules)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock { Text = module, VerticalAlignment = VerticalAlignment.Center });

                var deleteButton = new Button { Content = "✕", Tag = module };
                deleteButton.Click += async (_, _) => await RemoveModuleAsync(module);
                Grid.SetColumn(deleteButton, 1);
                row.Children.Add(deleteButton);

                modulesPanel.Children.Add(row);
            }
        }

        RenderModuleRows();

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(search);
        content.Children.Add(new TextBlock { Text = "Bereits verknüpfte Module", Opacity = 0.75 });
        content.Children.Add(modulesScroller);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Related Modules verwalten",
            PrimaryButtonText = "Übernehmen",
            CloseButtonText = "Schließen",
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };

        dialog.PrimaryButtonClick += (_, _) =>
        {
            var newModule = string.IsNullOrWhiteSpace(selectedModule) ? search.Text?.Trim() ?? string.Empty : selectedModule;
            if (!string.IsNullOrWhiteSpace(newModule)
                && VM.AvailableModules.Any(x => string.Equals(x, newModule, StringComparison.OrdinalIgnoreCase))
                && !selectedModules.Any(x => string.Equals(x, newModule, StringComparison.OrdinalIgnoreCase)))
            {
                selectedModules.Add(newModule);
            }

            VM.SelectedRelatedModules.Clear();
            foreach (var module in selectedModules)
                VM.SelectedRelatedModules.Add(module);
            VM.RelatedModulesText = string.Join(", ", VM.SelectedRelatedModules);
        };

        await dialog.ShowAsync();
    }

    private async void HistoryItems_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ScriptHistoryItem item)
            return;

        var normalized1 = (item.Content ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        var sqlViewer = new TextBox
        {
            Text = normalized1,
            IsReadOnly = true,
            IsSpellCheckEnabled = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var scroller = new ScrollViewer
        {
            Content = sqlViewer,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var dialogContent = new Grid { Width = 900, Height = 500, MinWidth = 720, MinHeight = 380 };
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dialogContent.Children.Add(new TextBlock
        {
            Text = $"Gültig von {item.ValidFrom:G} bis {item.ValidTo:G}",
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 8)
        });
        Grid.SetRow(scroller, 1);
        dialogContent.Children.Add(scroller);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"SQL snapshot from {item.ValidFrom:G}",
            PrimaryButtonText = "Restore in editor",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            Content = dialogContent,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try { VM.RestoreHistoryVersionCommand.Execute(item); }
            catch (Exception ex) { VM.Error = ex.Message; }
        }
    }
}
