using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class LibrarySplitViewModel : ObservableObject
{
    private const int DefaultPageSize = 50;
    private const int MinimumPageSize = 1;
    private const double EstimatedResultItemHeight = 96d;
    private readonly IScriptRepository _repo;
    private readonly ICustomerMappingRepository _customerMappingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserWorkspaceStateStore _workspaceStateStore;
    private readonly IScriptFolderRepository _folderRepository;
    private readonly IScriptCollectionRepository _collectionRepository;
    private Frame? _detailFrame;
    private ScriptSearchFilters? _activeFilters;
    private string? _activeSearchText;
    private int _pageSize = DefaultPageSize;
    public UserWorkspaceState? LastLoadedWorkspaceState { get; private set; }
    public Guid? CurrentDetailScriptId { get; private set; }

    public ObservableCollection<ScriptListItem> Results { get; } = new();
    public ObservableCollection<string> AvailableMainModules { get; } = new();
    public ObservableCollection<string> AvailableRelatedModules { get; } = new();
    public ObservableCollection<string> FilteredMainModules { get; } = new();
    public ObservableCollection<string> FilteredRelatedModules { get; } = new();
    public ObservableCollection<string> AvailableTags { get; } = new();
    public ObservableCollection<string> FilteredTags { get; } = new();
    public ObservableCollection<FolderTreeOption> AvailableFolders { get; } = new();
    public ObservableCollection<ScriptCollection> AvailableCollections { get; } = new();

    [ObservableProperty] private string _queryText = "";
    [ObservableProperty] private ScriptListItem? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private int _scopeFilterIndex;
    [ObservableProperty] private string _mainModuleFilterText = "";
    [ObservableProperty] private string _relatedModuleFilterText = "";
    [ObservableProperty] private string _customerCodeFilterText = "";
    [ObservableProperty] private string _tagsFilterText = "";
    [ObservableProperty] private string _objectFilterText = "";
    [ObservableProperty] private string _moduleCatalogSearchText = "";
    [ObservableProperty] private string _tagCatalogSearchText = "";
    [ObservableProperty] private bool _includeDeleted;
    [ObservableProperty] private bool _searchInHistory;
    [ObservableProperty] private bool _isAdvancedSearchExpanded;
    [ObservableProperty] private bool _displayFolderStructure;
    [ObservableProperty] private FolderTreeOption? _selectedFolder;
    [ObservableProperty] private ScriptCollection? _selectedCollection;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private bool _hasNextPage;
    [ObservableProperty] private Visibility _paginationVisibility = Visibility.Collapsed;


    public string ResultsCountText =>
        Results.Count == 0
            ? $"Keine Ergebnisse (Seite {CurrentPage})"
            : $"{Results.Count} Ergebnisse (Seite {CurrentPage})";
    public bool CanGoToPreviousPage => CurrentPage > 1;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public Visibility AdminButtonVisibility => App.CurrentUser?.IsAdmin == true
        ? Visibility.Visible
        : Visibility.Collapsed;


    public LibrarySplitViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
        _customerMappingRepository = App.Services.GetRequiredService<ICustomerMappingRepository>();
        _userRepository = App.Services.GetRequiredService<IUserRepository>();
        _workspaceStateStore = App.Services.GetRequiredService<IUserWorkspaceStateStore>();
        _folderRepository = App.Services.GetRequiredService<IScriptFolderRepository>();
        _collectionRepository = App.Services.GetRequiredService<IScriptCollectionRepository>();
    }

    public void AttachDetailFrame(Frame frame) => _detailFrame = frame;

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            await BuildAndStoreActiveSearchContextAsync();
            await LoadPageAsync(1);

            await RefreshMetadataCatalogAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSelectedChanged(ScriptListItem? value)
    {
        if (value is null) return;
        NavigateDetail(value.Id);
    }


    [RelayCommand]
    private void OpenCustomerMappingAdmin()
    {
        if (App.CurrentUser?.IsAdmin != true)
        {
            Error = "Keine Berechtigung für Admin-Bereich.";
            return;
        }

        if (_detailFrame is null) return;
        Selected = null;
        CurrentDetailScriptId = null;
        _detailFrame.Navigate(typeof(CustomerMappingAdminView));
    }

    [RelayCommand]
    private void OpenModuleAdmin()
    {
        if (App.CurrentUser?.IsAdmin != true)
        {
            Error = "Keine Berechtigung für Admin-Bereich.";
            return;
        }

        if (_detailFrame is null) return;
        Selected = null;
        CurrentDetailScriptId = null;
        _detailFrame.Navigate(typeof(ModuleAdminView));
    }

    [RelayCommand]
    private void OpenUserManagementAdmin()
    {
        if (App.CurrentUser?.IsAdmin != true)
        {
            Error = "Keine Berechtigung für Admin-Bereich.";
            return;
        }

        if (_detailFrame is null) return;
        Selected = null;
        CurrentDetailScriptId = null;
        _detailFrame.Navigate(typeof(UserManagementAdminView));
    }


    [RelayCommand]
    private async Task LogoutAndCloseAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            await _userRepository.ClearRememberedDeviceAsync();
            App.CurrentUser = null;
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        NavigateDetail(Guid.Empty);
    }

    [RelayCommand]
    private void Edit(Guid id)
    {
        NavigateDetail(id);
    }

    [RelayCommand]
    private async Task DeleteAsync(Guid id)
    {
        try
        {
            IsBusy = true;
            Error = null;

            await _repo.DeleteAsync(id);

            if (_detailFrame?.Content is ScriptItemView)
            {
                NavigateDetail(Guid.Empty);
            }

            await SearchAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private async Task PreviousPageAsync()
    {
        if (!CanGoToPreviousPage)
            return;

        await LoadPageAsync(CurrentPage - 1);
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private async Task NextPageAsync()
    {
        if (!HasNextPage)
            return;

        await LoadPageAsync(CurrentPage + 1);
    }


    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;
            await RefreshMetadataCatalogAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectFolder(FolderTreeOption? folder)
    {
        SelectedFolder = folder;
    }

    [RelayCommand]
    private void ClearFolder()
    {
        SelectedFolder = null;
    }

    [RelayCommand]
    private void ApplyModuleFilter(string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return;

        MainModuleFilterText = module.Trim();
    }

    [RelayCommand]
    private void ApplyRelatedModuleFilter(string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return;

        RelatedModuleFilterText = AppendFilterToken(RelatedModuleFilterText, module);
    }

    [RelayCommand]
    private void ToggleTagFilter(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        var selected = (TagsFilterText ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = selected.FirstOrDefault(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            selected.Add(tag.Trim());
        else
            selected.Remove(existing);

        TagsFilterText = string.Join(", ", selected);
    }

    private async Task RefreshMetadataCatalogAsync()
    {
        var metadata = await _repo.GetMetadataCatalogAsync(IncludeDeleted);

        AvailableMainModules.Clear();
        foreach (var module in metadata.Modules)
            AvailableMainModules.Add(module);

        AvailableRelatedModules.Clear();
        foreach (var module in metadata.RelatedModules)
            AvailableRelatedModules.Add(module);

        AvailableTags.Clear();
        foreach (var tag in metadata.Tags)
            AvailableTags.Add(tag);

        ApplyCatalogFilters();
        await RefreshFolderAndCollectionOptionsAsync();
    }

    private async Task RefreshFolderAndCollectionOptionsAsync()
    {
        var folderTree = await _folderRepository.GetTreeAsync();
        var flattenedFolders = new List<FolderTreeOption>();
        foreach (var root in folderTree.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            FlattenFolder(root, flattenedFolders, 0);

        AvailableFolders.Clear();
        foreach (var folder in flattenedFolders)
            AvailableFolders.Add(folder);

        SelectedFolder = SelectedFolder is null
            ? null
            : AvailableFolders.FirstOrDefault(x => x.Id == SelectedFolder.Id);

        var collections = await _collectionRepository.GetAllAsync();
        var sortedCollections = collections
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AvailableCollections.Clear();
        foreach (var collection in sortedCollections)
            AvailableCollections.Add(collection);

        SelectedCollection = SelectedCollection is null
            ? null
            : AvailableCollections.FirstOrDefault(x => x.Id == SelectedCollection.Id);
    }

    public async Task<IReadOnlyList<ScriptFolderTreeNode>> GetFolderTreeAsync()
    {
        var tree = await _folderRepository.GetTreeAsync();
        return tree
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void FlattenFolder(ScriptFolderTreeNode node, ICollection<FolderTreeOption> target, int level)
    {
        target.Add(new FolderTreeOption(node.Id, level, node.Name));

        foreach (var child in node.Children.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            FlattenFolder(child, target, level + 1);
    }

    partial void OnModuleCatalogSearchTextChanged(string value)
    {
        ApplyCatalogFilters();
    }

    partial void OnTagCatalogSearchTextChanged(string value)
    {
        ApplyCatalogFilters();
    }

    private void ApplyCatalogFilters()
    {
        ApplyCatalogFilter(AvailableMainModules, FilteredMainModules, ModuleCatalogSearchText);
        ApplyCatalogFilter(AvailableRelatedModules, FilteredRelatedModules, ModuleCatalogSearchText);
        ApplyCatalogFilter(AvailableTags, FilteredTags, TagCatalogSearchText);
    }

    private static void ApplyCatalogFilter(IEnumerable<string> source, ObservableCollection<string> target, string? filter)
    {
        var typed = filter?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(typed))
        {
            target.Clear();
            return;
        }

        var matches = source
            .Where(x => x.Contains(typed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        target.Clear();
        foreach (var value in matches)
            target.Add(value);
    }

    private async Task<Guid?> ResolveCustomerFilterAsync()
        => await ResolveCustomerFilterAsync(CustomerCodeFilterText, QueryText);

    private async Task<Guid?> ResolveCustomerFilterAsync(string? customerCodeFilterText, string? queryText)
    {
        var codeInput = customerCodeFilterText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(codeInput))
        {
            var codeMapping = await _customerMappingRepository.GetByCodeAsync(codeInput);
            if (codeMapping is null)
                throw new InvalidOperationException($"Kundenkürzel '{codeInput}' wurde nicht gefunden.");

            return codeMapping.CustomerId;
        }

        return await TryResolveCustomerIdFromQueryTextAsync(queryText);
    }

    private async Task<Guid?> TryResolveCustomerIdFromQueryTextAsync()
        => await TryResolveCustomerIdFromQueryTextAsync(QueryText);

    private async Task<Guid?> TryResolveCustomerIdFromQueryTextAsync(string? queryText)
    {
        var query = queryText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var mapping = await _customerMappingRepository.GetByCodeAsync(query);
        return mapping?.CustomerId;
    }


    public async Task OpenByNumberIdAsync(int numberId)
    {
        if (numberId <= 0)
            return;

        try
        {
            IsBusy = true;
            Error = null;

            var scriptId = await _repo.GetIdByNumberIdAsync(numberId);
            if (scriptId is null)
            {
                Error = $"Kein Script mit Key #{numberId} gefunden.";
                return;
            }

            NavigateDetail(scriptId.Value);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NavigateDetail(Guid id)
    {
        if (_detailFrame is null) return;
        CurrentDetailScriptId = id == Guid.Empty ? null : id;
        _detailFrame.Navigate(typeof(ScriptItemView), id);
    }

    public async Task SearchWithSpotlightGroupsAsync(IReadOnlyList<SpotlightFilterGroup> groups, bool combineWithAnd)
    {
        if (groups is null || groups.Count == 0)
        {
            Error = "Keine Spotlight-Regelgruppen übergeben.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var activeGroups = groups.Where(HasAnyGroupFilter).ToList();
            if (activeGroups.Count == 0)
            {
                Error = "Mindestens eine Regelgruppe benötigt einen Filterwert.";
                return;
            }

            var groupedResults = new List<IReadOnlyList<ScriptListItem>>(activeGroups.Count);
            foreach (var group in activeGroups)
            {
                var customerId = await ResolveCustomerFilterAsync(group.CustomerCodeFilterText, group.QueryText);
                var searchText = ResolveSearchText(group.QueryText, group.CustomerCodeFilterText, customerId);
                var filters = BuildFilters(
                    group.ScopeFilterIndex,
                    group.MainModuleFilterText,
                    group.RelatedModuleFilterText,
                    group.TagsFilterText,
                    group.ObjectFilterText,
                    group.IncludeDeleted,
                    group.SearchInHistory,
                    group.FolderMustMatchExactly,
                    group.FolderId,
                    group.CollectionId,
                    customerId);

                groupedResults.Add(await _repo.SearchAsync(searchText, filters, take: 5000, skip: 0));
            }

            var combined = CombineSpotlightGroups(groupedResults, combineWithAnd);

            Results.Clear();
            foreach (var item in combined)
                Results.Add(item);

            _activeSearchText = null;
            _activeFilters = null;
            CurrentPage = 1;
            HasNextPage = false;
            PaginationVisibility = Visibility.Collapsed;

            OnPropertyChanged(nameof(ResultsCountText));
            OnPropertyChanged(nameof(CanGoToPreviousPage));

            await RefreshMetadataCatalogAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadPageAsync(int page)
    {
        if (_activeFilters is null)
            return;

        var skip = Math.Max(0, page - 1) * _pageSize;
        var items = await _repo.SearchAsync(_activeSearchText, _activeFilters, take: _pageSize, skip: skip);

        Results.Clear();
        foreach (var it in items)
            Results.Add(it);

        CurrentPage = page;
        HasNextPage = items.Count == _pageSize;
        PaginationVisibility = CurrentPage > 1 || HasNextPage ? Visibility.Visible : Visibility.Collapsed;

        OnPropertyChanged(nameof(ResultsCountText));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(ResultsCountText));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasNextPageChanged(bool value)
    {
        NextPageCommand.NotifyCanExecuteChanged();
    }

    public async Task UpdateViewportHeightAsync(double height)
    {
        if (height <= 0)
            return;

        var calculatedPageSize = Math.Max(MinimumPageSize, (int)Math.Floor(height / EstimatedResultItemHeight));
        if (calculatedPageSize == _pageSize)
            return;

        var previousPageSize = _pageSize;
        _pageSize = calculatedPageSize;

        if (_activeFilters is null)
            return;

        var absoluteIndex = Math.Max(0, CurrentPage - 1) * previousPageSize;
        var targetPage = (absoluteIndex / _pageSize) + 1;
        await LoadPageAsync(targetPage);
    }

    public async Task RestoreWorkspaceStateForCurrentUserAsync()
    {
        if (App.CurrentUser is null)
            return;

        var state = await _workspaceStateStore.LoadAsync(App.CurrentUser.Id);
        if (state is null)
            return;

        LastLoadedWorkspaceState = state;

        QueryText = state.QueryText;
        ScopeFilterIndex = state.ScopeFilterIndex;
        MainModuleFilterText = state.MainModuleFilterText;
        RelatedModuleFilterText = state.RelatedModuleFilterText;
        CustomerCodeFilterText = state.CustomerCodeFilterText;
        TagsFilterText = state.TagsFilterText;
        ObjectFilterText = state.ObjectFilterText;
        ModuleCatalogSearchText = state.ModuleCatalogSearchText;
        TagCatalogSearchText = state.TagCatalogSearchText;
        IncludeDeleted = state.IncludeDeleted;
        SearchInHistory = state.SearchInHistory;
        IsAdvancedSearchExpanded = state.IsAdvancedSearchExpanded;

        if (state.HadExecutedSearch)
        {
            try
            {
                await BuildAndStoreActiveSearchContextAsync();
                await LoadPageAsync(Math.Max(1, state.CurrentPage));
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }

    public async Task SaveWorkspaceStateForCurrentUserAsync(WorkspaceDetailTarget detailTarget, Guid? detailScriptId)
    {
        if (App.CurrentUser is null)
            return;

        var state = new UserWorkspaceState(
            QueryText: QueryText,
            ScopeFilterIndex: ScopeFilterIndex,
            MainModuleFilterText: MainModuleFilterText,
            RelatedModuleFilterText: RelatedModuleFilterText,
            CustomerCodeFilterText: CustomerCodeFilterText,
            TagsFilterText: TagsFilterText,
            ObjectFilterText: ObjectFilterText,
            ModuleCatalogSearchText: ModuleCatalogSearchText,
            TagCatalogSearchText: TagCatalogSearchText,
            IncludeDeleted: IncludeDeleted,
            SearchInHistory: SearchInHistory,
            IsAdvancedSearchExpanded: IsAdvancedSearchExpanded,
            CurrentPage: CurrentPage,
            HadExecutedSearch: _activeFilters is not null,
            DetailTarget: detailTarget,
            DetailScriptId: detailScriptId);

        await _workspaceStateStore.SaveAsync(App.CurrentUser.Id, state);
    }

    private async Task BuildAndStoreActiveSearchContextAsync()
    {
        var customerId = await ResolveCustomerFilterAsync();
        _activeSearchText = ResolveSearchText(QueryText, CustomerCodeFilterText, customerId);
        _activeFilters = BuildFilters(
            ScopeFilterIndex,
            MainModuleFilterText,
            RelatedModuleFilterText,
            TagsFilterText,
            ObjectFilterText,
            IncludeDeleted,
            SearchInHistory,
            folderMustMatchExactly: false,
            SelectedFolder?.Id,
            SelectedCollection?.Id,
            customerId);
    }

    private static string? ResolveSearchText(string? queryText, string? customerCodeFilterText, Guid? customerId)
    {
        if (!string.IsNullOrWhiteSpace(queryText)
            && string.IsNullOrWhiteSpace(customerCodeFilterText)
            && customerId is not null)
        {
            return null;
        }

        return queryText;
    }


    private static bool HasAnyGroupFilter(SpotlightFilterGroup group)
        => !string.IsNullOrWhiteSpace(group.QueryText)
            || group.ScopeFilterIndex > 0
            || !string.IsNullOrWhiteSpace(group.MainModuleFilterText)
            || !string.IsNullOrWhiteSpace(group.RelatedModuleFilterText)
            || !string.IsNullOrWhiteSpace(group.CustomerCodeFilterText)
            || !string.IsNullOrWhiteSpace(group.TagsFilterText)
            || !string.IsNullOrWhiteSpace(group.ObjectFilterText)
            || group.IncludeDeleted
            || group.SearchInHistory
            || group.FolderId is not null
            || group.CollectionId is not null;

    private static ScriptSearchFilters BuildFilters(
        int scopeFilterIndex,
        string? mainModuleFilterText,
        string? relatedModuleFilterText,
        string? tagsFilterText,
        string? objectFilterText,
        bool includeDeleted,
        bool searchInHistory,
        bool folderMustMatchExactly,
        Guid? selectedFolderId,
        Guid? selectedCollectionId,
        Guid? customerId)
    {
        var relatedModules = ParseCsvFilter(relatedModuleFilterText);
        var tags = ParseCsvFilter(tagsFilterText);
        var referencedObjects = ParseCsvFilter(objectFilterText);

        return new ScriptSearchFilters(
            Scope: scopeFilterIndex switch
            {
                1 => 0,
                2 => 1,
                3 => 2,
                _ => null
            },
            CustomerId: customerId,
            Module: null,
            MainModule: string.IsNullOrWhiteSpace(mainModuleFilterText) ? null : mainModuleFilterText.Trim(),
            RelatedModule: relatedModules?.FirstOrDefault(),
            RelatedModules: relatedModules,
            Tags: tags,
            ReferencedObject: referencedObjects?.FirstOrDefault(),
            ReferencedObjects: referencedObjects,
            FolderId: selectedFolderId,
            CollectionId: selectedCollectionId,
            IncludeDeleted: includeDeleted,
            SearchHistory: searchInHistory,
            FolderMustMatchExactly: folderMustMatchExactly);
    }

    private static IReadOnlyList<ScriptListItem> CombineSpotlightGroups(IReadOnlyList<IReadOnlyList<ScriptListItem>> groupedResults, bool combineWithAnd)
    {
        if (groupedResults.Count == 0)
            return Array.Empty<ScriptListItem>();

        var combined = groupedResults[0].ToDictionary(x => x.Id, x => x);
        for (var i = 1; i < groupedResults.Count; i++)
        {
            var group = groupedResults[i];
            if (combineWithAnd)
            {
                var ids = group.Select(x => x.Id).ToHashSet();
                foreach (var id in combined.Keys.ToList())
                {
                    if (!ids.Contains(id))
                        combined.Remove(id);
                }

                continue;
            }

            foreach (var item in group)
                combined.TryAdd(item.Id, item);
        }

        return combined.Values.OrderBy(x => x.NumberId).ToList();
    }

    private static IReadOnlyList<string>? ParseCsvFilter(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string AppendFilterToken(string? currentValue, string token)
    {
        var values = ParseCsvFilter(currentValue)?.ToList() ?? new List<string>();
        if (!values.Contains(token, StringComparer.OrdinalIgnoreCase))
            values.Add(token.Trim());

        return string.Join(", ", values);
    }
}

public sealed record FolderTreeOption
{
    public FolderTreeOption()
    {
        Name = string.Empty;
    }

    public FolderTreeOption(Guid id, int level, string name)
    {
        Id = id;
        Level = level;
        Name = name;
    }

    public Guid Id { get; set; }
    public int Level { get; set; }
    public string Name { get; set; }

    public string DisplayName => $"{new string('•', Math.Max(1, Level + 1))} {Name}";
}


public sealed record SpotlightFilterGroup(
    string? QueryText,
    int ScopeFilterIndex,
    string? MainModuleFilterText,
    string? RelatedModuleFilterText,
    string? CustomerCodeFilterText,
    string? TagsFilterText,
    string? ObjectFilterText,
    bool IncludeDeleted,
    bool SearchInHistory,
    bool FolderMustMatchExactly,
    Guid? FolderId,
    Guid? CollectionId);
