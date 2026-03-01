using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace SqlFroega.FlowLauncher;

public sealed class Main : IPlugin, IContextMenu, ISettingProvider, IPluginI18n
{
    private static readonly TimeSpan CopyOperationTimeout = TimeSpan.FromSeconds(15);

    private readonly PluginSettings _settings = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _searchCache = new(StringComparer.OrdinalIgnoreCase);

    private SqlFroegaApiClient? _api;
    private PluginInitContext? _context;
    private DateTimeOffset _lastApiSearchAt = DateTimeOffset.MinValue;

    public void Init(PluginInitContext context)
    {
        _context = context;
        context.API.LoadSettingJsonStorage<PluginSettings>()?.Let(loaded => CopySettings(loaded, _settings));

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(NormalizeBaseUrl(_settings.ApiBaseUrl)),
            Timeout = TimeSpan.FromSeconds(12)
        };

        _api = new SqlFroegaApiClient(httpClient, _settings);
    }

    public List<Result> Query(Query query)
    {
        if (_api is null)
        {
            return new List<Result> { BuildInfoResult("Plugin nicht initialisiert.", "Init fehlgeschlagen") };
        }

        var search = query.Search.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return new List<Result>
            {
                BuildInfoResult("SQL suchen (Tippen) · Enter kopiert Original SQL", "FlowLauncher SqlFroega"),
                BuildInfoResult("Für Render-SQL: Kontextmenü öffnen (Shift+Enter)", "Default Customer oder Mappings")
            };
        }

        if (TryGetFreshCached(search, out var cachedResults))
        {
            return cachedResults;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastApiSearchAt).TotalMilliseconds < 250 && TryGetAnyCached(out var fallbackResults))
        {
            return fallbackResults;
        }

        _lastApiSearchAt = now;

        try
        {
            var scripts = _api.SearchScriptsAsync(search, CancellationToken.None).GetAwaiter().GetResult();
            var results = scripts.Select(BuildScriptResult).ToList();
            _searchCache[search] = new CacheEntry(now, results);

            if (results.Count == 0)
            {
                return new List<Result> { BuildInfoResult("Keine Treffer.", $"Suche: {search}") };
            }

            return results;
        }
        catch (Exception ex)
        {
            return new List<Result> { BuildInfoResult("API-Fehler", ex.Message) };
        }
    }

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not ScriptListItem script || _api is null)
        {
            return new List<Result>();
        }

        return new List<Result>
        {
            new()
            {
                Title = "Copy SQL",
                SubTitle = "Original SQL in die Zwischenablage kopieren",
                Action = _ => QueueCopy(() => CopyOriginalSql(script.Id))
            },
            new()
            {
                Title = "Copy Rendered SQL",
                SubTitle = "Gerendertes SQL (mit CustomerCode)",
                Action = _ => QueueCopy(() => CopyRenderedSql(script.Id))
            }
        };
    }

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_settings, SaveSettings);
    }

    public string GetTranslatedPluginTitle() => "SqlFroega";

    public string GetTranslatedPluginDescription() => "Sucht Skripte über SqlFroega.Api und kopiert SQL/Rendered SQL.";

    private bool CopyOriginalSql(Guid scriptId)
    {
        try
        {
            using var cts = new CancellationTokenSource(CopyOperationTimeout);
            var detail = _api!.GetScriptDetailAsync(scriptId, cts.Token).GetAwaiter().GetResult();
            if (detail is null)
            {
                _context?.API.ShowMsg("SqlFroega", "Skript nicht gefunden.");
                return false;
            }

            ClipboardHelper.SetText(detail.Content);
            _context?.API.ShowMsg("SqlFroega", $"{detail.Name}: SQL kopiert");
            return true;
        }
        catch (Exception ex)
        {
            _context?.API.ShowMsg("SqlFroega", ex.Message);
            return false;
        }
    }

    private bool CopyRenderedSql(Guid scriptId)
    {
        try
        {
            using var cts = new CancellationTokenSource(CopyOperationTimeout);

            var detail = _api!.GetScriptDetailAsync(scriptId, cts.Token).GetAwaiter().GetResult();
            if (detail is null)
            {
                _context?.API.ShowMsg("SqlFroega", "Skript nicht gefunden.");
                return false;
            }

            var customerCode = ResolveCustomerCode(cts.Token);
            if (string.IsNullOrWhiteSpace(customerCode))
            {
                _context?.API.ShowMsg("SqlFroega", "Kein customerCode gesetzt (DefaultCustomerCode oder Mappings).\n");
                return false;
            }

            var rendered = _api.RenderSqlAsync(customerCode, detail.Content, cts.Token).GetAwaiter().GetResult();
            if (rendered is null)
            {
                _context?.API.ShowMsg("SqlFroega", "Rendern fehlgeschlagen.");
                return false;
            }

            ClipboardHelper.SetText(rendered.RenderedSql);
            _context?.API.ShowMsg("SqlFroega", $"{detail.Name}: Rendered SQL für {rendered.CustomerCode} kopiert");
            return true;
        }
        catch (Exception ex)
        {
            _context?.API.ShowMsg("SqlFroega", ex.Message);
            return false;
        }
    }

    private string ResolveCustomerCode(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_settings.DefaultCustomerCode))
        {
            return _settings.DefaultCustomerCode.Trim();
        }

        var mappings = _api!.GetCustomerMappingsAsync(ct).GetAwaiter().GetResult();
        return mappings.FirstOrDefault()?.CustomerCode ?? string.Empty;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var sanitized = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:5000" : baseUrl.Trim();
        return sanitized.EndsWith('/') ? sanitized : sanitized + "/";
    }

    private bool TryGetFreshCached(string key, out List<Result> results)
    {
        if (_searchCache.TryGetValue(key, out var entry)
            && (DateTimeOffset.UtcNow - entry.Timestamp).TotalSeconds <= Math.Clamp(_settings.SearchCacheSeconds, 30, 120))
        {
            results = entry.Results;
            return true;
        }

        results = new List<Result>();
        return false;
    }

    private bool TryGetAnyCached(out List<Result> results)
    {
        var entry = _searchCache.OrderByDescending(x => x.Value.Timestamp).FirstOrDefault().Value;
        if (entry is null)
        {
            results = new List<Result>();
            return false;
        }

        results = entry.Results;
        return true;
    }

    private Result BuildScriptResult(ScriptListItem script)
    {
        return new Result
        {
            Title = script.Name,
            SubTitle = $"#{script.NumberId} · {script.ScopeLabel} · {script.MainModule ?? "-"}",
            IcoPath = "Images/app.png",
            ContextData = script,
            Action = _ => QueueCopy(() => CopyOriginalSql(script.Id))
        };
    }

    private static Result BuildInfoResult(string title, string subtitle) => new() { Title = title, SubTitle = subtitle, IcoPath = "Images/app.png" };

    private void SaveSettings()
    {
        _context?.API.SaveSettingJsonStorage<PluginSettings>();
        _api = new SqlFroegaApiClient(new HttpClient
        {
            BaseAddress = new Uri(NormalizeBaseUrl(_settings.ApiBaseUrl)),
            Timeout = TimeSpan.FromSeconds(12)
        }, _settings);
    }

    private bool QueueCopy(Func<bool> copyAction)
    {
        _ = Task.Run(copyAction);
        return true;
    }

    private static void CopySettings(PluginSettings source, PluginSettings target)
    {
        target.ApiBaseUrl = source.ApiBaseUrl;
        target.Username = source.Username;
        target.Password = source.Password;
        target.DefaultTenantContext = source.DefaultTenantContext;
        target.DefaultCustomerCode = source.DefaultCustomerCode;
        target.SearchCacheSeconds = source.SearchCacheSeconds;
    }

    private sealed record CacheEntry(DateTimeOffset Timestamp, List<Result> Results);
}

internal static class FunctionalExtensions
{
    public static void Let<T>(this T? value, Action<T> action)
        where T : class
    {
        if (value is not null)
        {
            action(value);
        }
    }
}
