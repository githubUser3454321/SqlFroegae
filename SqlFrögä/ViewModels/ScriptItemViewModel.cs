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
    private readonly ICustomerMappingRepository _mappingRepository;
    private readonly ISqlCustomerRenderService _renderService;
    private Guid _id;

    public ObservableCollection<ScriptHistoryItem> HistoryItems { get; } = new();
    public ObservableCollection<CustomerMappingItem> CustomerMappings { get; } = new();

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
    [ObservableProperty] private bool _isReadOnlyMode;

    [ObservableProperty] private string _selectedCustomerCode = "";
    [ObservableProperty] private string _mappingCustomerCode = "";
    [ObservableProperty] private string _mappingCustomerName = "";
    [ObservableProperty] private string _mappingSchemaName = "om";
    [ObservableProperty] private string _mappingObjectPrefix = "om_";
    [ObservableProperty] private string _mappingDatabaseUser = "om";

    public string HistoryCountText => HistoryItems.Count == 0 ? "No history entries" : $"{HistoryItems.Count} versions";

    public ScriptItemViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
        _mappingRepository = App.Services.GetRequiredService<ICustomerMappingRepository>();
        _renderService = App.Services.GetRequiredService<ISqlCustomerRenderService>();
    }

    public async Task LoadAsync(Guid id)
    {
        _id = id;
        await LoadMappingsAsync();

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
            IsReadOnlyMode = false;
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
                Title = "Deleted Script (History)";
                Name = "(deleted)";
                Key = "(history only)";
                Scope = 0;
                Module = "";
                Description = "Record was deleted. Read-only temporal history is shown.";
                TagsText = "";
                CustomerIdText = "";
                IsReadOnlyMode = true;

                await TryLoadHistoryAsync();
                Content = HistoryItems.FirstOrDefault()?.Content ?? string.Empty;
                Error = "Script was deleted. You can inspect history but not save this view.";
                return;
            }

            Title = "Edit Script";
            Name = detail.Name;
            Key = detail.Key;
            Content = NormalizeSqlContent(detail.Content);
            Module = detail.Module;
            Description = detail.Description;
            TagsText = string.Join(", ", detail.Tags ?? Array.Empty<string>());
            CustomerIdText = detail.CustomerId?.ToString() ?? "";
            IsReadOnlyMode = false;

            Scope = detail.ScopeLabel switch
            {
                "Global" => 0,
                "Customer" => 1,
                "Module" => 2,
                _ => 0
            };

            await TryLoadHistoryAsync();
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
    private async Task RefreshMappingsAsync() => await LoadMappingsAsync();

    [RelayCommand]
    private async Task UpsertMappingAsync()
    {
        if (string.IsNullOrWhiteSpace(MappingCustomerCode) || string.IsNullOrWhiteSpace(MappingCustomerName))
            throw new InvalidOperationException("Customer code and name are required for mapping.");

        var existing = CustomerMappings.FirstOrDefault(x => string.Equals(x.CustomerCode, MappingCustomerCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var item = new CustomerMappingItem(
            CustomerId: existing?.CustomerId ?? Guid.NewGuid(),
            CustomerCode: MappingCustomerCode.Trim(),
            CustomerName: MappingCustomerName.Trim(),
            SchemaName: string.IsNullOrWhiteSpace(MappingSchemaName) ? "om" : MappingSchemaName.Trim(),
            ObjectPrefix: string.IsNullOrWhiteSpace(MappingObjectPrefix) ? "om_" : MappingObjectPrefix.Trim(),
            DatabaseUser: string.IsNullOrWhiteSpace(MappingDatabaseUser) ? "om" : MappingDatabaseUser.Trim());

        await _mappingRepository.UpsertAsync(item);
        await LoadMappingsAsync();
    }

    [RelayCommand]
    private async Task CopyRenderedAsync()
    {
        var rendered = await BuildRenderedSqlAsync();
        if (string.IsNullOrWhiteSpace(rendered))
            return;

        var dp = new DataPackage();
        dp.SetText(rendered);
        Clipboard.SetContent(dp);
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
            await TryLoadHistoryAsync();
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
    private void RestoreHistoryVersion(ScriptHistoryItem? historyItem)
    {
        if (historyItem is null)
            return;

        if (IsReadOnlyMode)
            throw new InvalidOperationException("Deleted scripts can only be viewed in history mode.");

        Content = NormalizeSqlContent(historyItem.Content);
        Error = $"Restored snapshot from {historyItem.ValidFrom:G}. Save to persist this version.";
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
            if (IsReadOnlyMode)
                throw new InvalidOperationException("Deleted scripts can only be viewed in history mode.");
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

            var normalizedContent = NormalizeSqlContent(Content);
            if (customerId is null)
            {
                normalizedContent = await _renderService.NormalizeForStorageAsync(normalizedContent);
                Content = normalizedContent;
            }

            var dto = new ScriptUpsert(
                Id: _id == Guid.Empty ? null : _id,
                Name: Name.Trim(),
                Key: Key.Trim(),
                Content: normalizedContent,
                Scope: Scope,
                CustomerId: customerId,
                Module: string.IsNullOrWhiteSpace(Module) ? null : Module.Trim(),
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Tags: tags
            );

            var newId = await _repo.UpsertAsync(dto);
            _id = newId;
            Title = "Edit Script";

            await TryLoadHistoryAsync();
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

    private async Task<string> BuildRenderedSqlAsync()
    {
        var sql = NormalizeSqlContent(Content);
        if (string.IsNullOrWhiteSpace(SelectedCustomerCode))
            return sql;

        return await _renderService.RenderForCustomerAsync(sql, SelectedCustomerCode.Trim());
    }

    private async Task LoadMappingsAsync()
    {
        var mappings = await _mappingRepository.GetAllAsync();
        CustomerMappings.Clear();
        foreach (var mapping in mappings)
            CustomerMappings.Add(mapping);
    }

    private async Task LoadHistoryCoreAsync()
    {
        var items = await _repo.GetHistoryAsync(_id, take: 50);
        HistoryItems.Clear();
        foreach (var item in items)
        {
            item.Content = NormalizeSqlContent(item.Content);
            HistoryItems.Add(item);
        }

        OnPropertyChanged(nameof(HistoryCountText));
    }

    private async Task TryLoadHistoryAsync()
    {
        try
        {
            await LoadHistoryCoreAsync();
        }
        catch (Exception ex)
        {
            ClearHistory();
            Error = $"History could not be loaded ({ex.Message}). Script data is available and can still be edited.";
        }
    }

    private void ClearHistory()
    {
        HistoryItems.Clear();
        OnPropertyChanged(nameof(HistoryCountText));
    }

    private static string NormalizeSqlContent(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\r\\n", "\r\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal);
    }
}
