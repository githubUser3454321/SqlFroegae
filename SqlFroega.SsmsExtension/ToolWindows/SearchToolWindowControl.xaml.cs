using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

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
        var workspaceManager = new WorkspaceManager(settings);
        _viewModel = new SearchToolWindowViewModel(apiClient, workspaceManager);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }


    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _searchCts = RenewTokenSource(_searchCts);
        await _viewModel.LoadFoldersAsync(_searchCts.Token);
    }
    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        _searchCts = RenewTokenSource(_searchCts);
        await _viewModel.SearchAsync(_searchCts.Token);
    }

    private async void OnLoadFoldersClick(object sender, RoutedEventArgs e)
    {
        _searchCts = RenewTokenSource(_searchCts);
        await _viewModel.LoadFoldersAsync(_searchCts.Token);
    }

    private async void OnLoadFolderScriptsClick(object sender, RoutedEventArgs e)
    {
        _searchCts = RenewTokenSource(_searchCts);
        await _viewModel.LoadFolderScriptsAsync(_searchCts.Token);
    }

    private async void OnOpenAllClick(object sender, RoutedEventArgs e)
    {
        _searchCts = RenewTokenSource(_searchCts);
        var readonlyFlag = ReadonlyCheckBox.IsChecked ?? true;
        var openedPaths = await _viewModel.OpenAllResultsAsync(readonlyFlag, _searchCts.Token);

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;

        foreach (var path in openedPaths)
        {
            if (dte is not null)
            {
                dte.ItemOperations.OpenFile(path);
            }
            else
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        await OpenSelectedAsync();
    }

    private async void OnResultsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not SearchResultItem)
        {
            return;
        }

        await OpenSelectedAsync();
    }

    private async System.Threading.Tasks.Task OpenSelectedAsync()
    {
        if (ResultsGrid.SelectedItem is not SearchResultItem selected)
        {
            return;
        }

        _searchCts = RenewTokenSource(_searchCts);

        try
        {
            var openResult = await _viewModel.OpenSelectedAsync(selected, ReadonlyCheckBox.IsChecked ?? true, _searchCts.Token);
            var path = openResult.LocalPath;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            if (dte is not null)
            {
                dte.ItemOperations.OpenFile(path);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Fehlertext wird bereits im ViewModel gesetzt.
        }
    }

    private static CancellationTokenSource RenewTokenSource(CancellationTokenSource? source)
    {
        source?.Cancel();
        source?.Dispose();
        return new CancellationTokenSource();
    }
}
