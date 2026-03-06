using System;

namespace SqlFroega.SsmsExtension;

internal sealed class SsmsExtensionSettings
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5000";
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TenantContext { get; init; } = string.Empty;
    public int SearchTake { get; init; } = 40;
    public int BulkReadBatchSize { get; init; } = 8;
    public int HttpTimeoutSeconds { get; init; } = 30;
    public int HttpRetryCount { get; init; } = 2;
    public int HttpRetryDelayMs { get; init; } = 400;
    public int CircuitBreakerFailureThreshold { get; init; } = 5;
    public int CircuitBreakerBreakSeconds { get; init; } = 20;
    public string WorkspaceRoot { get; init; } = DefaultWorkspaceRoot();

    public static SsmsExtensionSettings LoadFromEnvironment()
    {
        return new SsmsExtensionSettings
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("SQLFROEGA_API_BASEURL") ?? "http://localhost:5000",
            Username = Environment.GetEnvironmentVariable("SQLFROEGA_USERNAME") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("SQLFROEGA_PASSWORD") ?? string.Empty,
            TenantContext = Environment.GetEnvironmentVariable("SQLFROEGA_TENANT_CONTEXT") ?? string.Empty,
            SearchTake = ParseTake(Environment.GetEnvironmentVariable("SQLFROEGA_SEARCH_TAKE")),
            BulkReadBatchSize = ParseBulkReadBatchSize(Environment.GetEnvironmentVariable("SQLFROEGA_BULKREAD_BATCHSIZE")),
            HttpTimeoutSeconds = ParseRange(Environment.GetEnvironmentVariable("SQLFROEGA_HTTP_TIMEOUT_SECONDS"), 5, 300, 30),
            HttpRetryCount = ParseRange(Environment.GetEnvironmentVariable("SQLFROEGA_HTTP_RETRY_COUNT"), 0, 5, 2),
            HttpRetryDelayMs = ParseRange(Environment.GetEnvironmentVariable("SQLFROEGA_HTTP_RETRY_DELAY_MS"), 100, 5000, 400),
            CircuitBreakerFailureThreshold = ParseRange(Environment.GetEnvironmentVariable("SQLFROEGA_HTTP_CB_FAILURE_THRESHOLD"), 1, 20, 5),
            CircuitBreakerBreakSeconds = ParseRange(Environment.GetEnvironmentVariable("SQLFROEGA_HTTP_CB_BREAK_SECONDS"), 5, 300, 20),
            WorkspaceRoot = ParseWorkspaceRoot(Environment.GetEnvironmentVariable("SQLFROEGA_WORKSPACE_ROOT"))
        };
    }

    private static int ParseTake(string? raw)
    {
        if (int.TryParse(raw, out var parsed) && parsed is > 0 and <= 200)
        {
            return parsed;
        }

        return 40;
    }

    private static string ParseWorkspaceRoot(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        return DefaultWorkspaceRoot();
    }

    private static int ParseBulkReadBatchSize(string? raw)
    {
        if (int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 50)
        {
            return parsed;
        }

        return 8;
    }

    private static int ParseRange(string? raw, int min, int max, int fallback)
    {
        if (int.TryParse(raw, out var parsed) && parsed >= min && parsed <= max)
        {
            return parsed;
        }

        return fallback;
    }

    private static string DefaultWorkspaceRoot()
    {
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlFroega", "SsmsWorkspace");
    }
}
