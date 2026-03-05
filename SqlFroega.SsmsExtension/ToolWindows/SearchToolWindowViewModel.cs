using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.SsmsExtension.ToolWindows;

internal sealed class SearchToolWindowViewModel : INotifyPropertyChanged
{
    private readonly SqlFroegaApiClient _apiClient;
    private string _searchTerm = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;

    public SearchToolWindowViewModel(SqlFroegaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SearchResultItem> Results { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (_searchTerm == value)
            {
                return;
            }

            _searchTerm = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task SearchAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Results.Clear();
            return;
        }

        IsLoading = true;
        try
        {
            var rows = await _apiClient.SearchScriptsAsync(SearchTerm.Trim(), ct);
            var mapped = rows.Select(x => new SearchResultItem(
                x.Name,
                x.NumberId,
                x.ScopeLabel,
                x.MainModule ?? string.Empty,
                x.Description ?? string.Empty));

            Results.Clear();
            foreach (var row in mapped)
            {
                Results.Add(row);
            }
        }
        catch (Exception ex)
        {
            Results.Clear();
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
