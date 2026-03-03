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

    public static bool CanEditProfile(string profileOwnerUsername, string? requestingUsername, bool isAdmin)
    {
        if (isAdmin)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(profileOwnerUsername) || string.IsNullOrWhiteSpace(requestingUsername))
        {
            return false;
        }

        return string.Equals(profileOwnerUsername.Trim(), requestingUsername.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
