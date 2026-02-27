using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;

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
        {
            await VM.LoadAsync(id);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private async void HistoryItems_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ScriptHistoryItem item)
            return;

        var sqlViewer = new TextBox
        {
            Text = item.Content ?? string.Empty,
            IsReadOnly = true,
            IsSpellCheckEnabled = false,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(sqlViewer, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(sqlViewer, ScrollBarVisibility.Auto);

        var dialogContent = new Grid
        {
            Width = 900,
            Height = 500,
            MinWidth = 720,
            MinHeight = 380
        };

        dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        dialogContent.Children.Add(new TextBlock
        {
            Text = $"Gültig von {item.ValidFrom:G} bis {item.ValidTo:G}",
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 8)
        });

        Grid.SetRow(sqlViewer, 1);
        dialogContent.Children.Add(sqlViewer);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"SQL snapshot from {item.ValidFrom:G}",
            CloseButtonText = "Close",
            CloseButtonStyle = Resources["HistoryDialogCloseButtonStyle"] as Style,
            DefaultButton = ContentDialogButton.Close,
            Content = dialogContent
        };

        await dialog.ShowAsync();
    }
}
