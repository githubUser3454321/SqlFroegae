namespace SqlFroega.Application.Services;

public static class SearchProfileVisibility
{
    public static string? NormalizeForRequest(string? raw, bool isAdmin)
    {
        if (string.Equals(raw?.Trim(), "global", StringComparison.OrdinalIgnoreCase))
        {
            return isAdmin ? "global" : null;
        }

        return "private";
    }

    public static string NormalizeForStorage(string? raw)
    {
        return string.Equals(raw?.Trim(), "global", StringComparison.OrdinalIgnoreCase)
            ? "global"
            : "private";
    }
}
