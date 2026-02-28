using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SqlFroega.Api.Auth;

internal interface IRefreshTokenStore
{
    RefreshTokenIssueResult Issue(string username, IReadOnlyList<string> scopes, TimeSpan lifetime);
    RefreshTokenValidationResult? Rotate(string refreshToken, TimeSpan lifetime);
    void Revoke(string refreshToken);
}

internal sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new(StringComparer.Ordinal);

    public RefreshTokenIssueResult Issue(string username, IReadOnlyList<string> scopes, TimeSpan lifetime)
    {
        var token = CreateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime);

        _tokens[token] = new RefreshTokenEntry(username, scopes, expiresAtUtc);

        return new RefreshTokenIssueResult(token, expiresAtUtc);
    }

    public RefreshTokenValidationResult? Rotate(string refreshToken, TimeSpan lifetime)
    {
        if (!_tokens.TryRemove(refreshToken, out var existing))
        {
            return null;
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        var next = Issue(existing.Username, existing.Scopes, lifetime);
        return new RefreshTokenValidationResult(existing.Username, existing.Scopes, next.Token, next.ExpiresAtUtc);
    }

    public void Revoke(string refreshToken)
    {
        _tokens.TryRemove(refreshToken, out _);
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

internal sealed record RefreshTokenEntry(string Username, IReadOnlyList<string> Scopes, DateTime ExpiresAtUtc);

internal sealed record RefreshTokenIssueResult(string Token, DateTime ExpiresAtUtc);

internal sealed record RefreshTokenValidationResult(string Username, IReadOnlyList<string> Scopes, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
