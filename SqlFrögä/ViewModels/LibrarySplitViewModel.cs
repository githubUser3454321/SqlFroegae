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
    private Frame? _detailFrame;

    public ObservableCollection<ScriptListItem> Results { get; } = new();
    public ObservableCollection<string> AvailableModules { get; } = new();
    public ObservableCollection<string> AvailableTags { get; } = new();

    [ObservableProperty] private string _queryText = "";
    [ObservableProperty] private ScriptListItem? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private int _scopeFilterIndex;
    [ObservableProperty] private string _customerIdFilterText = "";
    [ObservableProperty] private string _moduleFilterText = "";
    [ObservableProperty] private string _tagsFilterText = "";
    [ObservableProperty] private bool _includeDeleted;
    [ObservableProperty] private bool _searchInHistory;

    public string ResultsCountText => Results.Count == 0 ? "No results" : $"{Results.Count} results";

    public LibrarySplitViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
    }

    public void AttachDetailFrame(Frame frame) => _detailFrame = frame;

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            Guid? customerId = null;
            if (!string.IsNullOrWhiteSpace(CustomerIdFilterText))
            {
                if (!Guid.TryParse(CustomerIdFilterText.Trim(), out var parsed))
                    throw new InvalidOperationException("CustomerId filter is not a valid GUID.");
                customerId = parsed;
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
                Module: string.IsNullOrWhiteSpace(ModuleFilterText) ? null : ModuleFilterText.Trim(),
                Tags: tags,
                IncludeDeleted: IncludeDeleted,
                SearchHistory: SearchInHistory
            );

            var items = await _repo.SearchAsync(QueryText, filters, take: 200, skip: 0);

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

        ModuleFilterText = module.Trim();
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

        AvailableModules.Clear();
        foreach (var module in metadata.Modules)
            AvailableModules.Add(module);

        AvailableTags.Clear();
        foreach (var tag in metadata.Tags)
            AvailableTags.Add(tag);
    }

    private void NavigateDetail(Guid id)
    {
        if (_detailFrame is null) return;
        _detailFrame.Navigate(typeof(ScriptItemView), id);
    }
}
