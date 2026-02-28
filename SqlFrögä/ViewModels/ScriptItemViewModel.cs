using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
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
    private string _loadedNormalizedContent = string.Empty;
    private ScriptEditAwareness? _editAwareness;
    private bool _hasEditAwarenessWarning;

    public ObservableCollection<ScriptHistoryItem> HistoryItems { get; } = new();
    public ObservableCollection<CustomerMappingItem> CustomerMappings { get; } = new();
    public ObservableCollection<string> AvailableModules { get; } = new();
    public ObservableCollection<string> SelectedRelatedModules { get; } = new();
    public ObservableCollection<string> AvailableFlags { get; } = new();
    public ObservableCollection<string> SelectedFlags { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [ObservableProperty] private string _title = "Script";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _content = "";
    [ObservableProperty] private int _scope = 0; // 0 Global, 1 Customer, 2 Module
    [ObservableProperty] private string? _mainModule;
    [ObservableProperty] private string _relatedModulesText = "";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string _flagsText = "";
    [ObservableProperty] private bool _isReadOnlyMode;
    [ObservableProperty] private bool _isEditUnlocked;

    [ObservableProperty] private string _selectedCustomerCode = "";
    [ObservableProperty] private string _scriptCustomerCode = "";
    [ObservableProperty] private bool _replaceDatabaseUserAndPrefix = true;

    public event Func<string, Task>? WarningRequested;

    public bool IsEditingEnabled => !IsReadOnlyMode && (_id == Guid.Empty || IsEditUnlocked);

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
        await LoadModulesAsync();
        await LoadFlagsAsync();

        if (id == Guid.Empty)
        {
            Title = "New Script";
            Name = "";
            Key = "";
            Content = "";
            Scope = 0;
            MainModule = "";
            SetRelatedModules(Array.Empty<string>());
            Description = "";
            SetFlags(Array.Empty<string>());
            ScriptCustomerCode = "";
            IsReadOnlyMode = false;
            IsEditUnlocked = true;
            ReplaceDatabaseUserAndPrefix = true;
            Error = null;
            _editAwareness = null;
            _hasEditAwarenessWarning = false;
            ClearHistory();
            _loadedNormalizedContent = NormalizeSqlContent(Content);
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
                MainModule = "";
                SetRelatedModules(Array.Empty<string>());
                Description = "Record was deleted. Read-only temporal history is shown.";
                SetFlags(Array.Empty<string>());
                ScriptCustomerCode = "";
                IsReadOnlyMode = true;
                ReplaceDatabaseUserAndPrefix = false;

                await TryLoadHistoryAsync();
                Content = HistoryItems.FirstOrDefault()?.Content ?? string.Empty;
                _loadedNormalizedContent = NormalizeSqlContent(Content);
                Error = "Script was deleted. You can inspect history but not save this view.";
                return;
            }

            Title = "Edit Script";
            Name = detail.Name;
            Key = detail.Key;
            Content = NormalizeSqlContent(detail.Content);
            _loadedNormalizedContent = NormalizeSqlContent(detail.Content);
            MainModule = detail.MainModule;
            SetRelatedModules(detail.RelatedModules ?? Array.Empty<string>());
            Description = detail.Description;
            SetFlags(detail.Tags ?? Array.Empty<string>());
            IsReadOnlyMode = false;
            IsEditUnlocked = false;
            ReplaceDatabaseUserAndPrefix = true;
            _editAwareness = await _repo.RegisterViewAsync(id, App.CurrentUser?.Username);
            _hasEditAwarenessWarning = false;

            ScriptCustomerCode = "";
            if (detail.CustomerId.HasValue)
            {
                var mapping = CustomerMappings.FirstOrDefault(x => x.CustomerId == detail.CustomerId.Value);
                ScriptCustomerCode = mapping?.CustomerCode ?? string.Empty;
            }

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
    private void AddRelatedModule(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            return;

        var normalized = moduleName.Trim();
        EnsureModuleExists(normalized);

        if (SelectedRelatedModules.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedRelatedModules.Add(normalized);
        UpdateRelatedModulesText();
    }

    [RelayCommand]
    private void RemoveRelatedModule(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            return;

        var existing = SelectedRelatedModules.FirstOrDefault(x => string.Equals(x, moduleName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;

        SelectedRelatedModules.Remove(existing);
        UpdateRelatedModulesText();
    }

    [RelayCommand]
    private void AddFlag(string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return;

        var normalized = flag.Trim();
        EnsureFlagExists(normalized);

        if (SelectedFlags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedFlags.Add(normalized);
        UpdateFlagsText();
    }

    [RelayCommand]
    private void CreateFlag(string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return;

        var normalized = flag.Trim();

        if (!AvailableFlags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            AvailableFlags.Add(normalized);

        if (!SelectedFlags.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            SelectedFlags.Add(normalized);

        UpdateFlagsText();
    }

    [RelayCommand]
    private void RemoveFlag(string? flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return;

        var existing = SelectedFlags.FirstOrDefault(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;

        SelectedFlags.Remove(existing);
        UpdateFlagsText();
    }

    [RelayCommand]
    private async Task CopyRenderedAsync()
    {
        try
        {
            var rendered = await GetRenderedCopyTextAsync();
            if (string.IsNullOrWhiteSpace(rendered))
                return;

            var dp = new DataPackage();
            dp.SetText(rendered);
            Clipboard.SetContent(dp);
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
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
        var text = GetCopyText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }

    public string? GetCopyText()
        => string.IsNullOrWhiteSpace(Content) ? null : Content;

    public async Task<string?> GetRenderedCopyTextAsync()
    {
        try
        {
            var rendered = await BuildRenderedSqlAsync();
            Error = null;
            return string.IsNullOrWhiteSpace(rendered) ? null : rendered;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return null;
        }
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
        => await SaveWithMetadataAsync(null);

    [RelayCommand]
    private async Task AcquireEditLockAsync()
    {
        try
        {
            if (_id == Guid.Empty || IsReadOnlyMode)
            {
                IsEditUnlocked = true;
                return;
            }

            var username = App.CurrentUser?.Username?.Trim();
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Kein angemeldeter Benutzer gefunden.");

            var lockResult = await _repo.TryAcquireEditLockAsync(_id, username);
            if (!lockResult.Acquired)
                throw new InvalidOperationException($"Dieser Datensatz wird aktuell von '{lockResult.LockedBy ?? "einem anderen Anwender"}' bearbeitet.");

            var awarenessBeforeReload = _editAwareness;
            await ReloadCurrentRecordAsync();
            _editAwareness = awarenessBeforeReload ?? await _repo.GetEditAwarenessAsync(_id, username);
            Error = null;
            IsEditUnlocked = true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public async Task ReleaseEditLockAsync()
    {
        if (_id == Guid.Empty || !IsEditUnlocked)
            return;

        var username = App.CurrentUser?.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            return;

        await _repo.ReleaseEditLockAsync(_id, username);
        IsEditUnlocked = false;
    }

    private async Task ReloadCurrentRecordAsync()
    {
        if (_id == Guid.Empty)
            return;

        var currentId = _id;
        await LoadAsync(currentId);
    }

    public bool RequiresSqlContentChangeReason()
    {
        if (_id == Guid.Empty || IsReadOnlyMode)
            return false;

        var currentNormalized = NormalizeSqlContent(Content);
        return !string.Equals(_loadedNormalizedContent, currentNormalized, StringComparison.Ordinal);
    }

    public async Task SaveWithMetadataAsync(string? updateReason)
    {
        try
        {
            IsBusy = true;
            Error = null;

            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Name is required.");
            if (IsReadOnlyMode)
                throw new InvalidOperationException("Deleted scripts can only be viewed in history mode.");
            if (_id != Guid.Empty && !IsEditUnlocked)
                throw new InvalidOperationException("Bitte zuerst den Bearbeiten-Button (Unlock) klicken.");
            if (string.IsNullOrWhiteSpace(Key))
                throw new InvalidOperationException("Key is required.");
            if (string.IsNullOrWhiteSpace(Content))
                throw new InvalidOperationException("Content is required.");
            if (RequiresSqlContentChangeReason() && string.IsNullOrWhiteSpace(updateReason))
                throw new InvalidOperationException("Bitte einen Grund für die SQL-Änderung angeben.");

            if (!string.IsNullOrWhiteSpace(MainModule))
                EnsureModuleExists(MainModule.Trim());

            foreach (var relatedModule in SelectedRelatedModules)
                EnsureModuleExists(relatedModule);

            Guid? customerId = null;
            if (!string.IsNullOrWhiteSpace(ScriptCustomerCode))
            {
                var normalizedCode = ScriptCustomerCode.Trim();
                var mapping = CustomerMappings.FirstOrDefault(x => string.Equals(x.CustomerCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
                if (mapping is null)
                    throw new InvalidOperationException($"Kundenkürzel '{normalizedCode}' wurde nicht gefunden.");

                customerId = mapping.CustomerId;
            }

            var tags = SelectedFlags
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedContent = NormalizeSqlContent(Content);
            if (ReplaceDatabaseUserAndPrefix)
            {
                try
                {
                    normalizedContent = await _renderService.NormalizeForStorageAsync(normalizedContent);
                    Content = normalizedContent;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Automatic replacement has been disabled", StringComparison.OrdinalIgnoreCase))
                {
                    ReplaceDatabaseUserAndPrefix = false;
                    Error = "Mehrere unterschiedliche Mapping-Paare im Script erkannt. Automatisches Ersetzen wurde deaktiviert.";
                }
            }

            var dto = new ScriptUpsert(
                Id: _id == Guid.Empty ? null : _id,
                Name: Name.Trim(),
                Key: Key.Trim(),
                Content: normalizedContent,
                Scope: Scope,
                CustomerId: customerId,
                MainModule: string.IsNullOrWhiteSpace(MainModule) ? null : MainModule.Trim(),
                RelatedModules: SelectedRelatedModules.ToList(),
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Tags: tags,
                UpdatedBy: App.CurrentUser?.Username?.Trim(),
                UpdateReason: string.IsNullOrWhiteSpace(updateReason) ? null : updateReason.Trim()
            );

            var wasNewScript = _id == Guid.Empty;
            var newId = await _repo.UpsertAsync(dto);
            _id = newId;
            Title = "Edit Script";
            _loadedNormalizedContent = normalizedContent;

            if (wasNewScript)
            {
                var username = App.CurrentUser?.Username?.Trim();
                if (string.IsNullOrWhiteSpace(username))
                    throw new InvalidOperationException("Kein angemeldeter Benutzer gefunden.");

                var lockResult = await _repo.TryAcquireEditLockAsync(newId, username);
                if (!lockResult.Acquired)
                {
                    IsEditUnlocked = false;
                    throw new InvalidOperationException($"Bearbeitungssperre konnte nach dem Anlegen nicht gesetzt werden, da '{lockResult.LockedBy ?? "ein anderer Anwender"}' den Datensatz hält.");
                }

                IsEditUnlocked = true;
                _editAwareness = await _repo.RegisterViewAsync(newId, username);
                _hasEditAwarenessWarning = false;
            }

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

        var normalizedCode = SelectedCustomerCode.Trim();
        var matchingMappingsCount = CustomerMappings
            .Count(x => string.Equals(x.CustomerCode, normalizedCode, StringComparison.OrdinalIgnoreCase));

        if (matchingMappingsCount > 1)
            throw new InvalidOperationException($"Multiple mappings found for customer code '{normalizedCode}'. Please clean up duplicate customer mappings before rendering.");

        return await _renderService.RenderForCustomerAsync(sql, normalizedCode);
    }

    private async Task LoadMappingsAsync()
    {
        var mappings = await _mappingRepository.GetAllAsync();
        CustomerMappings.Clear();
        foreach (var mapping in mappings)
            CustomerMappings.Add(mapping);
    }

    private async Task LoadModulesAsync()
    {
        var modules = await _repo.GetManagedModulesAsync();
        AvailableModules.Clear();
        foreach (var module in modules)
            AvailableModules.Add(module);
    }

    private async Task LoadFlagsAsync()
    {
        var metadata = await _repo.GetMetadataCatalogAsync();
        AvailableFlags.Clear();
        foreach (var flag in metadata.Tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            AvailableFlags.Add(flag);
        }
    }

    private void SetRelatedModules(IEnumerable<string> modules)
    {
        SelectedRelatedModules.Clear();

        foreach (var module in modules
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SelectedRelatedModules.Add(module);
        }

        UpdateRelatedModulesText();
    }

    private void UpdateRelatedModulesText()
    {
        RelatedModulesText = string.Join(", ", SelectedRelatedModules);
    }

    private void SetFlags(IEnumerable<string> flags)
    {
        SelectedFlags.Clear();

        foreach (var flag in flags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!AvailableFlags.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase)))
                AvailableFlags.Add(flag);

            SelectedFlags.Add(flag);
        }

        UpdateFlagsText();
    }

    private void UpdateFlagsText()
    {
        FlagsText = string.Join(", ", SelectedFlags.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    private void EnsureModuleExists(string moduleName)
    {
        if (AvailableModules.Any(x => string.Equals(x, moduleName, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new InvalidOperationException($"Modul '{moduleName}' ist nicht in der Modulverwaltung vorhanden.");
    }

    private void EnsureFlagExists(string flag)
    {
        if (AvailableFlags.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new InvalidOperationException($"Flag '{flag}' ist nicht vorhanden. Bitte zuerst 'Neu erstellen' verwenden.");
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

    partial void OnIsReadOnlyModeChanged(bool value)
        => OnPropertyChanged(nameof(IsEditingEnabled));

    partial void OnIsEditUnlockedChanged(bool value)
        => OnPropertyChanged(nameof(IsEditingEnabled));

    partial void OnNameChanged(string value) => _ = EnsureEditAwarenessWarningAsync();
    partial void OnKeyChanged(string value) => _ = EnsureEditAwarenessWarningAsync();
    partial void OnContentChanged(string value) => _ = EnsureEditAwarenessWarningAsync();
    partial void OnDescriptionChanged(string? value) => _ = EnsureEditAwarenessWarningAsync();
    partial void OnMainModuleChanged(string? value) => _ = EnsureEditAwarenessWarningAsync();

    private async Task EnsureEditAwarenessWarningAsync()
    {
        if (_hasEditAwarenessWarning || !IsEditUnlocked || _id == Guid.Empty)
            return;

        var awareness = _editAwareness;
        if (awareness is null)
            awareness = await _repo.GetEditAwarenessAsync(_id, App.CurrentUser?.Username);

        _editAwareness = awareness;
        if (awareness?.LastViewedAt is null || awareness.LastUpdatedAt is null)
            return;

        var username = App.CurrentUser?.Username?.Trim() ?? string.Empty;
        if (string.Equals(awareness.LastUpdatedBy?.Trim(), username, StringComparison.OrdinalIgnoreCase))
            return;

        if (awareness.LastUpdatedAt <= awareness.LastViewedAt)
            return;

        var viewedAge = DateTime.UtcNow - awareness.LastViewedAt.Value;
        if (viewedAge > TimeSpan.FromDays(10))
            return;

        _hasEditAwarenessWarning = true;

        var days = Math.Max(0, (int)Math.Floor(viewedAge.TotalDays));
        var message = $"Achtung: Seit deinem letzten Öffnen vor {days} Tagen wurde der Datensatz von '{awareness.LastUpdatedBy ?? "einem anderen Anwender"}' aktualisiert. " +
                      "Bitte prüfe die aktuelle Version, damit du keine Änderungen überschreibst und dein SQL-Script auf der neuesten Basis aufsetzt.";

        if (WarningRequested is not null)
            await WarningRequested.Invoke(message);
    }
}
