using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace SqlFroega.Views;

public sealed partial class ScriptItemView : Page
{
    private readonly Dictionary<Button, DragPointerState> _dragPointers = new();
    private const double DragStartThreshold = 6;

    public ScriptItemView()
    {
        InitializeComponent();
        RegisterDragPointerHandlers(CopyButton);
        RegisterDragPointerHandlers(CopyRenderedButton);
    }

    private void RegisterDragPointerHandlers(Button button)
    {
        button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(CopyDragSource_PointerPressed), true);
        button.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(CopyDragSource_PointerMoved), true);
        button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(CopyDragSource_PointerReleased), true);
        button.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(CopyDragSource_PointerCanceled), true);
    }

    private sealed class DragPointerState
    {
        public uint PointerId { get; init; }
        public Point StartPoint { get; init; }
        public bool IsDragging { get; set; }
    }

    private ScriptItemViewModel VM => (ScriptItemViewModel)DataContext;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        VM.WarningRequested -= OnWarningRequestedAsync;
        VM.WarningRequested += OnWarningRequestedAsync;

        if (e.Parameter is Guid id)
            await VM.LoadAsync(id);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        VM.WarningRequested -= OnWarningRequestedAsync;
        _ = VM.ReleaseEditLockAsync();
    }

    private Task OnWarningRequestedAsync(string message)
        => ShowSimpleMessageAsync("Hinweis zu zwischenzeitlichen Änderungen", message);

    private async Task ShowSimpleMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "Verstanden"
        };

        ApplyPrimaryDialogStyle(dialog);
        await dialog.ShowAsync();
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

    private void CustomerCodeAutoSuggest_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is AutoSuggestBox autoSuggest)
            UpdateCustomerCodeSuggestions(autoSuggest, autoSuggest.Text);
    }

    private void CustomerCodeAutoSuggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
            return;

        UpdateCustomerCodeSuggestions(sender, sender.Text);
    }

    private void CustomerCodeAutoSuggest_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not CustomerMappingItem mapping)
            return;

        sender.Text = mapping.CustomerCode;

        if (sender == SelectedCustomerCodeAutoSuggest)
            VM.SelectedCustomerCode = mapping.CustomerCode;
        else if (sender == ScriptCustomerCodeAutoSuggest)
            VM.ScriptCustomerCode = mapping.CustomerCode;
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

    private void UpdateCustomerCodeSuggestions(AutoSuggestBox control, string? typedText)
    {
        var typed = typedText?.Trim() ?? string.Empty;
        control.ItemsSource = VM.CustomerMappings
            .Where(x =>
                string.IsNullOrWhiteSpace(typed)
                || x.CustomerCode.Contains(typed, StringComparison.OrdinalIgnoreCase)
                || x.CustomerName.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();
    }

    private void CopyDragSource_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var point = e.GetCurrentPoint(button);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _dragPointers[button] = new DragPointerState
        {
            PointerId = e.Pointer.PointerId,
            StartPoint = point.Position,
            IsDragging = false
        };
    }

    private async void CopyDragSource_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (!_dragPointers.TryGetValue(button, out var state))
            return;

        if (state.IsDragging || state.PointerId != e.Pointer.PointerId)
            return;

        var point = e.GetCurrentPoint(button);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _dragPointers.Remove(button);
            return;
        }

        var dx = point.Position.X - state.StartPoint.X;
        var dy = point.Position.Y - state.StartPoint.Y;
        var distanceSquared = dx * dx + dy * dy;
        if (distanceSquared < DragStartThreshold * DragStartThreshold)
            return;

        state.IsDragging = true;
        await button.StartDragAsync(point);
    }

    private void CopyDragSource_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
            _dragPointers.Remove(button);
    }

    private void CopyDragSource_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
            _dragPointers.Remove(button);
    }



    private void CopyButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var text = VM.GetCopyText();
        if (string.IsNullOrWhiteSpace(text))
        {
            args.Cancel = true;
            return;
        }

        args.Data.SetText(text);
        args.AllowedOperations = DataPackageOperation.Copy;
    }

    private async void CopyRenderedButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var text = await VM.GetRenderedCopyTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                args.Cancel = true;
                return;
            }

            args.Data.SetText(text);
            args.AllowedOperations = DataPackageOperation.Copy;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!VM.RequiresSqlContentChangeReason())
        {
            await VM.SaveWithMetadataAsync(null);
            return;
        }

        var reason = await ShowSqlChangeReasonDialogAsync();
        if (reason is null)
            return;

        await VM.SaveWithMetadataAsync(reason);
    }

    private async Task<string?> ShowSqlChangeReasonDialogAsync()
    {
        var reasonBox = new TextBox
        {
            PlaceholderText = "Grund für SQL-Änderung eingeben",
            IsSpellCheckEnabled = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 140
        };

        var scroller = new ScrollViewer
        {
            Content = reasonBox,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var dialogContent = new Grid { Width = 900, Height = 300, MinWidth = 720, MinHeight = 220 };
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dialogContent.Children.Add(new TextBlock
        {
            Text = "Für SQL-Content-Änderungen ist ein Begründungstext verpflichtend.",
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 8)
        });
        Grid.SetRow(scroller, 1);
        dialogContent.Children.Add(scroller);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "SQL-Änderung begründen",
            PrimaryButtonText = "Speichern",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
            Content = dialogContent,
            IsPrimaryButtonEnabled = false,
        };

        ApplyPrimaryDialogStyle(dialog);

        reasonBox.TextChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(reasonBox.Text);
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? reasonBox.Text?.Trim()
            : null;
    }

    private static void ApplyPrimaryDialogStyle(ContentDialog dialog)
    {
        var normal = ColorHelper.FromArgb(0xFF, 0x33, 0x99, 0xFF);
        var hover = ColorHelper.FromArgb(0xFF, 0x5A, 0xB3, 0xFF);
        var pressed = ColorHelper.FromArgb(0xFF, 0x1F, 0x7F, 0xD6);
        var disabled = ColorHelper.FromArgb(0xFF, 0xCC, 0xCC, 0xCC);

        dialog.Resources["AccentButtonBackground"] = new SolidColorBrush(normal);
        dialog.Resources["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(hover);
        dialog.Resources["AccentButtonBackgroundPressed"] = new SolidColorBrush(pressed);
        dialog.Resources["AccentButtonBackgroundDisabled"] = new SolidColorBrush(disabled);

        dialog.Resources["AccentButtonForeground"] = new SolidColorBrush(Colors.White);
        dialog.Resources["AccentButtonForegroundPointerOver"] = new SolidColorBrush(Colors.White);
        dialog.Resources["AccentButtonForegroundPressed"] = new SolidColorBrush(Colors.White);
        dialog.Resources["AccentButtonForegroundDisabled"] = new SolidColorBrush(Colors.White);

        dialog.Resources["AccentButtonBorderBrush"] = new SolidColorBrush(normal);
        dialog.Resources["AccentButtonBorderBrushPointerOver"] = new SolidColorBrush(hover);
        dialog.Resources["AccentButtonBorderBrushPressed"] = new SolidColorBrush(pressed);
        dialog.Resources["AccentButtonBorderBrushDisabled"] = new SolidColorBrush(disabled);
        dialog.PrimaryButtonStyle = App.Current.Resources["LightBluePrimaryDialogButton"] as Style;
    }

    private async void EditRelatedModules_Click(object sender, RoutedEventArgs e) => await ShowRelatedModulesDialogAsync();

    private async void RelatedModulesTextBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (!VM.IsReadOnlyMode)
            await ShowRelatedModulesDialogAsync();
    }

    private async void EditFlags_Click(object sender, RoutedEventArgs e) => await ShowFlagsDialogAsync();

    private async void FlagsTextBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (!VM.IsReadOnlyMode)
            await ShowFlagsDialogAsync();
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
            CloseButtonText = "Schliessen",
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            IsPrimaryButtonEnabled = true,

        };
        ApplyPrimaryDialogStyle(dialog);

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

    private async Task ShowFlagsDialogAsync()
    {
        var selectedFlags = new ObservableCollection<string>(VM.SelectedFlags);
        var selectedSet = new HashSet<string>(selectedFlags, StringComparer.OrdinalIgnoreCase);

        var listContainer = new StackPanel
        {
            Spacing = 4
        };

        var listScroll = new ScrollViewer
        {
            Height = 260,
            Content = listContainer,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        void ApplyFilter(string typed)
        {
            var matches = VM.AvailableFlags
                .Where(x => string.IsNullOrWhiteSpace(typed) || x.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(selectedSet.Contains)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            listContainer.Children.Clear();
            foreach (var flag in matches)
            {
                var checkBox = new CheckBox
                {
                    Content = flag,
                    IsChecked = selectedSet.Contains(flag)
                };

                checkBox.Checked += (_, _) =>
                {
                    selectedSet.Add(flag);
                    ApplyFilter(typed);
                };

                checkBox.Unchecked += (_, _) =>
                {
                    selectedSet.Remove(flag);
                    ApplyFilter(typed);
                };

                listContainer.Children.Add(checkBox);
            }
        }

        var search = new TextBox { PlaceholderText = "Flag suchen", Width = 420 };
        search.TextChanged += (_, _) =>
        {
            var typed = search.Text?.Trim() ?? string.Empty;
            ApplyFilter(typed);
        };

        var createButton = new Button { Content = "Neu erstellen", HorizontalAlignment = HorizontalAlignment.Left };
        createButton.Click += (_, _) =>
        {
            var newFlag = search.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newFlag))
                return;

            VM.CreateFlagCommand.Execute(newFlag);
            selectedSet.Add(newFlag);
            ApplyFilter(newFlag);
        };

        ApplyFilter(string.Empty);

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(search);
        content.Children.Add(createButton);
        content.Children.Add(new TextBlock { Text = "Flags auswählen (Mehrfachauswahl möglich)", Opacity = 0.75 });
        content.Children.Add(listScroll);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Flags verwalten",
            PrimaryButtonText = "Übernehmen",
            CloseButtonText = "Schliessen",
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            IsPrimaryButtonEnabled = true,
        };
        ApplyPrimaryDialogStyle(dialog);
        dialog.PrimaryButtonClick += (_, _) =>
        {
            selectedFlags.Clear();
            foreach (var flag in selectedSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                selectedFlags.Add(flag);

            VM.SelectedFlags.Clear();
            foreach (var flag in selectedFlags)
                VM.SelectedFlags.Add(flag);
            VM.FlagsText = string.Join(", ", VM.SelectedFlags);
        };

        await dialog.ShowAsync();
    }

    private async void HistoryItems_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ScriptHistoryItem item)
            return;

        var normalizedText = (item.Content ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        var sqlViewer = new TextBox
        {
            Text = normalizedText,
            IsReadOnly = true,
            IsSpellCheckEnabled = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        //bug with winUi3: without this, only the very first line of T-SQL will be correctly displayed in the ui
        sqlViewer.Text = normalizedText;

        var scroller = new ScrollViewer
        {
            Content = sqlViewer,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var reasonText = string.IsNullOrWhiteSpace(item.ChangeReason)
            ? "Kein Änderungsgrund vorhanden."
            : item.ChangeReason;

        var metaPanel = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8)
        };
        metaPanel.Children.Add(new TextBlock
        {
            Text = $"Gültig von {item.ValidFrom:G} bis {item.ValidTo:G}",
            Opacity = 0.75
        });
        metaPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.ChangedBy)
                ? "Geändert von: unbekannt"
                : $"Geändert von: {item.ChangedBy}",
            Opacity = 0.75
        });
        metaPanel.Children.Add(new TextBlock
        {
            Text = $"Änderungsgrund: {reasonText}",
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var dialogContent = new Grid { Width = 900, Height = 500, MinWidth = 720, MinHeight = 380 };
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        dialogContent.Children.Add(metaPanel);
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
            IsPrimaryButtonEnabled = true,
        };
        ApplyPrimaryDialogStyle(dialog);

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try { VM.RestoreHistoryVersionCommand.Execute(item); }
            catch (Exception ex) { VM.Error = ex.Message; }
        }
    }
}
