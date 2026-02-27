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
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(sqlViewer, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(sqlViewer, ScrollBarVisibility.Auto);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"SQL snapshot from {item.ValidFrom:G}",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Content = new Grid
            {
                MinWidth = 720,
                MaxWidth = 900,
                MinHeight = 420,
                Children = { sqlViewer }
            }
        };

        await dialog.ShowAsync();
    }
}
