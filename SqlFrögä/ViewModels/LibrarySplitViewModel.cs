using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IScriptRepository _repo;
    private readonly ICustomerMappingRepository _customerMappingRepository;
    private Frame? _detailFrame;

    public ObservableCollection<ScriptListItem> Results { get; } = new();
    public ObservableCollection<string> AvailableMainModules { get; } = new();
    public ObservableCollection<string> AvailableRelatedModules { get; } = new();
    public ObservableCollection<string> FilteredMainModules { get; } = new();
    public ObservableCollection<string> FilteredRelatedModules { get; } = new();
    public ObservableCollection<string> AvailableTags { get; } = new();
    public ObservableCollection<string> FilteredTags { get; } = new();

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


    public string ResultsCountText => Results.Count == 0 ? "No results" : $"{Results.Count} results";

    public LibrarySplitViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
        _customerMappingRepository = App.Services.GetRequiredService<ICustomerMappingRepository>();
    }

    public void AttachDetailFrame(Frame frame) => _detailFrame = frame;

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var customerId = await ResolveCustomerFilterAsync();

            var searchText = QueryText;

            if (!string.IsNullOrWhiteSpace(QueryText)
                && string.IsNullOrWhiteSpace(CustomerCodeFilterText))
            {
                var mappedCustomer = await _customerMappingRepository.GetByCodeAsync(QueryText.Trim());
                if (mappedCustomer is not null)
                {
                    customerId = mappedCustomer.CustomerId;
                    searchText = null;
                }
            }

            IReadOnlyList<string>? tags = null;
            if (!string.IsNullOrWhiteSpace(TagsFilterText))
            {
                tags = TagsFilterText
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var filters = new ScriptSearchFilters(
                Scope: ScopeFilterIndex switch
                {
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    _ => null
                },
                CustomerId: customerId,
                Module: null,
                MainModule: string.IsNullOrWhiteSpace(MainModuleFilterText) ? null : MainModuleFilterText.Trim(),
                RelatedModule: string.IsNullOrWhiteSpace(RelatedModuleFilterText) ? null : RelatedModuleFilterText.Trim(),
                Tags: tags,
                ReferencedObject: string.IsNullOrWhiteSpace(ObjectFilterText) ? null : ObjectFilterText.Trim(),
                IncludeDeleted: IncludeDeleted,
                SearchHistory: SearchInHistory
            );

            var items = await _repo.SearchAsync(searchText, filters, take: 200, skip: 0);

            Results.Clear();
            foreach (var it in items)
                Results.Add(it);

            OnPropertyChanged(nameof(ResultsCountText));

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

    partial void OnSelectedChanged(ScriptListItem? value)
    {
        if (value is null) return;
        NavigateDetail(value.Id);
    }


    [RelayCommand]
    private void OpenCustomerMappingAdmin()
    {
        if (_detailFrame is null) return;
        Selected = null;
        _detailFrame.Navigate(typeof(CustomerMappingAdminView));
    }

    [RelayCommand]
    private void OpenModuleAdmin()
    {
        if (_detailFrame is null) return;
        Selected = null;
        _detailFrame.Navigate(typeof(ModuleAdminView));
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

        RelatedModuleFilterText = module.Trim();
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
    {
        var codeInput = CustomerCodeFilterText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(codeInput))
        {
            var codeMapping = await _customerMappingRepository.GetByCodeAsync(codeInput);
            if (codeMapping is null)
                throw new InvalidOperationException($"Kundenkürzel '{codeInput}' wurde nicht gefunden.");

            return codeMapping.CustomerId;
        }

        return await TryResolveCustomerIdFromQueryTextAsync();
    }

    private async Task<Guid?> TryResolveCustomerIdFromQueryTextAsync()
    {
        var query = QueryText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var mapping = await _customerMappingRepository.GetByCodeAsync(query);
        return mapping?.CustomerId;
    }

    private void NavigateDetail(Guid id)
    {
        if (_detailFrame is null) return;
        _detailFrame.Navigate(typeof(ScriptItemView), id);
    }
}
