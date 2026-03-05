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
    private readonly WorkspaceManager _workspaceManager;
    private string _searchTerm = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;
    private string _statusMessage = "Bereit";

    public SearchToolWindowViewModel(SqlFroegaApiClient apiClient, WorkspaceManager workspaceManager)
    {
        _apiClient = apiClient;
        _workspaceManager = workspaceManager;
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task SearchAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        StatusMessage = "Suche läuft...";

        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            Results.Clear();
            StatusMessage = "Bitte Suchbegriff eingeben.";
            return;
        }

        IsLoading = true;
        try
        {
            var rows = await _apiClient.SearchScriptsAsync(SearchTerm.Trim(), ct);
            var mapped = rows.Select(x => new SearchResultItem(
                x.Id,
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

            StatusMessage = $"{Results.Count} Treffer geladen.";
        }
        catch (Exception ex)
        {
            Results.Clear();
            ErrorMessage = ex.Message;
            StatusMessage = "Suche fehlgeschlagen.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<WorkspaceOpenResult> OpenSelectedAsync(SearchResultItem selected, bool openReadonly, CancellationToken ct)
    {
        ErrorMessage = null;
        IsLoading = true;
        StatusMessage = "Lade Script-Detail...";

        try
        {
            var detail = await _apiClient.GetScriptDetailAsync(selected.Id, ct);
            var openResult = _workspaceManager.SaveScript(detail, openReadonly);
            StatusMessage = $"{selected.Name} geöffnet ({openResult.OpenMode}) → {openResult.LocalPath}";
            return openResult;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Öffnen fehlgeschlagen.";
            throw;
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
