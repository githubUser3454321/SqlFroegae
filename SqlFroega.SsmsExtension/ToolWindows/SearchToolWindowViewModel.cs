using System;
using System.Collections.Generic;
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
    private FolderOptionItem? _selectedFolder;

    public SearchToolWindowViewModel(SqlFroegaApiClient apiClient, WorkspaceManager workspaceManager)
    {
        _apiClient = apiClient;
        _workspaceManager = workspaceManager;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SearchResultItem> Results { get; } = new();
    public ObservableCollection<FolderOptionItem> Folders { get; } = new();

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

    public FolderOptionItem? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (_selectedFolder == value)
            {
                return;
            }

            _selectedFolder = value;
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

    public async Task LoadFoldersAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        IsLoading = true;
        StatusMessage = "Lade Ordnerbaum...";

        try
        {
            var tree = await _apiClient.GetFolderTreeAsync(ct);
            var flattened = FlattenFolders(tree, null).OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

            Folders.Clear();
            foreach (var folder in flattened)
            {
                Folders.Add(folder);
            }

            SelectedFolder = Folders.FirstOrDefault();
            StatusMessage = $"{Folders.Count} Ordner geladen.";
        }
        catch (Exception ex)
        {
            Folders.Clear();
            SelectedFolder = null;
            ErrorMessage = ex.Message;
            StatusMessage = "Ordnerbaum konnte nicht geladen werden.";
        }
        finally
        {
            IsLoading = false;
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
            SetResults(rows);
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

    public async Task LoadFolderScriptsAsync(CancellationToken ct)
    {
        ErrorMessage = null;

        if (SelectedFolder is null)
        {
            StatusMessage = "Bitte zuerst einen Ordner auswählen.";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Lade Skripte aus Ordner {SelectedFolder.DisplayName}...";

        try
        {
            var rows = await _apiClient.GetScriptsByFolderAsync(SelectedFolder.Id, ct);
            SetResults(rows);
            StatusMessage = $"{Results.Count} Skripte aus Ordner geladen.";
        }
        catch (Exception ex)
        {
            Results.Clear();
            ErrorMessage = ex.Message;
            StatusMessage = "Folder Search fehlgeschlagen.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<IReadOnlyList<string>> OpenAllResultsAsync(bool openReadonly, CancellationToken ct)
    {
        ErrorMessage = null;

        if (Results.Count == 0)
        {
            StatusMessage = "Keine Treffer zum Öffnen vorhanden.";
            return Array.Empty<string>();
        }

        IsLoading = true;
        StatusMessage = $"Öffne {Results.Count} Skripte...";

        var openedPaths = new List<string>();
        try
        {
            foreach (var result in Results)
            {
                ct.ThrowIfCancellationRequested();
                var opened = await OpenSelectedAsync(result, openReadonly, ct);
                openedPaths.Add(opened.LocalPath);
            }

            StatusMessage = $"Bulk Read abgeschlossen: {openedPaths.Count} Skripte lokal geöffnet.";
            return openedPaths;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = $"Bulk Read abgebrochen nach {openedPaths.Count} Skripten.";
            return openedPaths;
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

    private void SetResults(IReadOnlyList<ScriptListItem> rows)
    {
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
    }

    private static IEnumerable<FolderOptionItem> FlattenFolders(IEnumerable<ScriptFolderTreeNode> nodes, string? prefix)
    {
        foreach (var node in nodes.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var label = string.IsNullOrWhiteSpace(prefix) ? node.Name : $"{prefix}/{node.Name}";
            yield return new FolderOptionItem(node.Id, label);

            if (node.Children is { Count: > 0 })
            {
                foreach (var child in FlattenFolders(node.Children, label))
                {
                    yield return child;
                }
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
