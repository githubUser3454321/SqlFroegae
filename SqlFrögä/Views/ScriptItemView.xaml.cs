using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

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

        var original = item.Content ?? string.Empty;


        // Test 1: CR-only -> CRLF
        var normalized1 = original.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");



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
        sqlViewer.Text = normalized1;
        // ScrollViewer explizit
        var scroller = new ScrollViewer
        {
            Content = sqlViewer,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

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

        Grid.SetRow(scroller, 1);
        dialogContent.Children.Add(scroller);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"SQL snapshot from {item.ValidFrom:G}",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.None,   // <— wichtig
            Content = dialogContent,
        };

        await dialog.ShowAsync();
    }
}
