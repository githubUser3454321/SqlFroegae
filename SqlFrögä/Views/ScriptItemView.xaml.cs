using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Collections.Generic;
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

    private void MainModuleAutoSuggest_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is AutoSuggestBox autoSuggest)
            UpdateModuleSuggestions(autoSuggest, autoSuggest.Text);
    }

    private void MainModuleAutoSuggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
            return;

        UpdateModuleSuggestions(sender, sender.Text);
    }

    private void MainModuleAutoSuggest_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string moduleName)
            VM.MainModule = moduleName;
    }

    private void UpdateModuleSuggestions(AutoSuggestBox control, string? typedText)
    {
        var typed = typedText?.Trim() ?? string.Empty;
        control.ItemsSource = VM.AvailableModules
            .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();
    }

    private async void EditRelatedModules_Click(object sender, RoutedEventArgs e) => await ShowRelatedModulesDialogAsync();

    private async void RelatedModulesTextBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (!VM.IsReadOnlyMode)
            await ShowRelatedModulesDialogAsync();
    }

    private async Task ShowRelatedModulesDialogAsync()
    {
        var selectedModules = new ObservableCollection<string>(VM.SelectedRelatedModules);
        var filteredModules = new ObservableCollection<string>();
        var selectedSet = new HashSet<string>(selectedModules, StringComparer.OrdinalIgnoreCase);

        var moduleList = new ListView
        {
            Height = 260,
            SelectionMode = ListViewSelectionMode.Multiple,
            IsItemClickEnabled = false,
            ItemsSource = filteredModules
        };

        void ApplyFilter(string typed)
        {
            var matches = VM.AvailableModules
                .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            filteredModules.Clear();
            foreach (var module in matches)
                filteredModules.Add(module);

            moduleList.SelectedItems.Clear();
            foreach (var module in filteredModules.Where(selectedSet.Contains))
                moduleList.SelectedItems.Add(module);
        }

        var search = new AutoSuggestBox { PlaceholderText = "Modul suchen", Width = 420 };
        search.TextChanged += (_, e) =>
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
                return;

            var typed = search.Text?.Trim() ?? string.Empty;
            var suggestions = VM.AvailableModules
                .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();

            search.ItemsSource = suggestions;
            ApplyFilter(typed);
        };

        search.SuggestionChosen += (_, e) =>
        {
            if (e.SelectedItem is not string moduleName)
                return;

            search.Text = moduleName;
            ApplyFilter(moduleName);
        };

        moduleList.SelectionChanged += (_, _) =>
        {
            selectedSet = moduleList.SelectedItems
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        };

        ApplyFilter(string.Empty);

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(search);
        content.Children.Add(new TextBlock { Text = "Module auswählen (Mehrfachauswahl möglich)", Opacity = 0.75 });
        content.Children.Add(moduleList);

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
            selectedModules.Clear();
            foreach (var module in selectedSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                selectedModules.Add(module);

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
