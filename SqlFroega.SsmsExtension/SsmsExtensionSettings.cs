using System;

namespace SqlFroega.SsmsExtension;

internal sealed class SsmsExtensionSettings
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5000";
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TenantContext { get; init; } = string.Empty;
    public int SearchTake { get; init; } = 40;

    public static SsmsExtensionSettings LoadFromEnvironment()
    {
        return new SsmsExtensionSettings
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("SQLFROEGA_API_BASEURL") ?? "http://localhost:5000",
            Username = Environment.GetEnvironmentVariable("SQLFROEGA_USERNAME") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("SQLFROEGA_PASSWORD") ?? string.Empty,
            TenantContext = Environment.GetEnvironmentVariable("SQLFROEGA_TENANT_CONTEXT") ?? string.Empty,
            SearchTake = ParseTake(Environment.GetEnvironmentVariable("SQLFROEGA_SEARCH_TAKE"))
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
}
