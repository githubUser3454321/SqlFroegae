using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace SqlFroega.ViewModels;

public partial class ScriptItemViewModel : ObservableObject
{
    private readonly IScriptRepository _repo;
    private Guid _id;

    public ObservableCollection<ScriptHistoryItem> HistoryItems { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private string _title = "Script";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _content = "";
    [ObservableProperty] private int _scope = 0; // 0 Global, 1 Customer, 2 Module
    [ObservableProperty] private string? _module;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _tagsText = "";
    [ObservableProperty] private string _customerIdText = "";

    public string HistoryCountText => HistoryItems.Count == 0 ? "No history entries" : $"{HistoryItems.Count} versions";

    public ScriptItemViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
    }

    public async Task LoadAsync(Guid id)
    {
        _id = id;

        if (id == Guid.Empty)
        {
            Title = "New Script";
            Name = "";
            Key = "";
            Content = "";
            Scope = 0;
            Module = "";
            Description = "";
            TagsText = "";
            CustomerIdText = "";
            Error = null;
            ClearHistory();
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var detail = await _repo.GetByIdAsync(id);
            if (detail is null)
            {
                Error = "Script not found.";
                ClearHistory();
                return;
            }

            Title = "Edit Script";
            Name = detail.Name;
            Key = detail.Key;
            Content = detail.Content;
            Module = detail.Module;
            Description = detail.Description;
            TagsText = string.Join(", ", detail.Tags ?? Array.Empty<string>());
            CustomerIdText = detail.CustomerId?.ToString() ?? "";

            Scope = detail.ScopeLabel switch
            {
                "Global" => 0,
                "Customer" => 1,
                "Module" => 2,
                _ => 0
            };

            await LoadHistoryCoreAsync();
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
    private async Task RefreshHistoryAsync()
    {
        if (_id == Guid.Empty)
        {
            ClearHistory();
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;
            await LoadHistoryCoreAsync();
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
    private void Copy()
    {
        if (string.IsNullOrWhiteSpace(Content))
            return;

        var dp = new DataPackage();
        dp.SetText(Content);
        Clipboard.SetContent(dp);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Name is required.");
            if (string.IsNullOrWhiteSpace(Key))
                throw new InvalidOperationException("Key is required.");
            if (string.IsNullOrWhiteSpace(Content))
                throw new InvalidOperationException("Content is required.");

            Guid? customerId = null;
            if (!string.IsNullOrWhiteSpace(CustomerIdText))
            {
                if (!Guid.TryParse(CustomerIdText.Trim(), out var parsed))
                    throw new InvalidOperationException("CustomerId is not a valid GUID.");
                customerId = parsed;
            }

            var tags = (TagsText ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dto = new ScriptUpsert(
                Id: _id == Guid.Empty ? null : _id,
                Name: Name.Trim(),
                Key: Key.Trim(),
                Content: Content,
                Scope: Scope,
                CustomerId: customerId,
                Module: string.IsNullOrWhiteSpace(Module) ? null : Module.Trim(),
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Tags: tags
            );

            var newId = await _repo.UpsertAsync(dto);
            _id = newId;
            Title = "Edit Script";

            await LoadHistoryCoreAsync();
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

    private async Task LoadHistoryCoreAsync()
    {
        var items = await _repo.GetHistoryAsync(_id, take: 50);
        HistoryItems.Clear();
        foreach (var item in items)
            HistoryItems.Add(item);

        OnPropertyChanged(nameof(HistoryCountText));
    }

    private void ClearHistory()
    {
        HistoryItems.Clear();
        OnPropertyChanged(nameof(HistoryCountText));
    }
}
