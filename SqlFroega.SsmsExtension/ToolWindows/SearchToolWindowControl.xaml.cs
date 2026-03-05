using System;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace SqlFroega.SsmsExtension.ToolWindows;

public partial class SearchToolWindowControl : UserControl
{
    private readonly SearchToolWindowViewModel _viewModel;
    private CancellationTokenSource? _searchCts;

    public SearchToolWindowControl()
    {
        InitializeComponent();

        var settings = SsmsExtensionSettings.LoadFromEnvironment();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.ApiBaseUrl)
        };

        var apiClient = new SqlFroegaApiClient(httpClient, settings);
        _viewModel = new SearchToolWindowViewModel(apiClient);
        DataContext = _viewModel;
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        await _viewModel.SearchAsync(_searchCts.Token);
    }
}
