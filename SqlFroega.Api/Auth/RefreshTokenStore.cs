using System.Collections.Concurrent;
using System.Security.Cryptography;
using Dapper;
using SqlFroega.Infrastructure.Persistence.SqlServer;

namespace SqlFroega.Api.Auth;

internal interface IRefreshTokenStore
{
    Task<RefreshTokenIssueResult> IssueAsync(string username, IReadOnlyList<string> scopes, string? tenantContext, TimeSpan lifetime, CancellationToken ct = default);
    Task<RefreshTokenValidationResult?> RotateAsync(string refreshToken, TimeSpan lifetime, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

internal sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new(StringComparer.Ordinal);

    public Task<RefreshTokenIssueResult> IssueAsync(string username, IReadOnlyList<string> scopes, string? tenantContext, TimeSpan lifetime, CancellationToken ct = default)
    {
        var token = CreateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime);

        _tokens[token] = new RefreshTokenEntry(token, username, string.Join(',', scopes), tenantContext, expiresAtUtc);

        return Task.FromResult(new RefreshTokenIssueResult(token, expiresAtUtc));
    }

    public async Task<RefreshTokenValidationResult?> RotateAsync(string refreshToken, TimeSpan lifetime, CancellationToken ct = default)
    {
        if (!_tokens.TryRemove(refreshToken, out var existing))
        {
            return null;
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        var scopes = ParseScopes(existing.ScopesCsv);
        var next = await IssueAsync(existing.Username, scopes, existing.TenantContext, lifetime, ct);
        return new RefreshTokenValidationResult(existing.Username, scopes, existing.TenantContext, next.Token, next.ExpiresAtUtc);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        _tokens.TryRemove(refreshToken, out _);
        return Task.CompletedTask;
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        return scopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

internal sealed class SqlRefreshTokenStore : IRefreshTokenStore
{
    private const string TableName = "dbo.ApiRefreshTokens";
    private readonly ISqlConnectionFactory _connectionFactory;

    public SqlRefreshTokenStore(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RefreshTokenIssueResult> IssueAsync(string username, IReadOnlyList<string> scopes, string? tenantContext, TimeSpan lifetime, CancellationToken ct = default)
    {
        var token = CreateToken();
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime);
        var scopesCsv = string.Join(',', scopes);

        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = """
INSERT INTO dbo.ApiRefreshTokens (Token, Username, ScopesCsv, TenantContext, ExpiresAtUtc, CreatedAtUtc)
VALUES (@Token, @Username, @ScopesCsv, @TenantContext, @ExpiresAtUtc, SYSUTCDATETIME());
""";

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Token = token,
            Username = username,
            ScopesCsv = scopesCsv,
            TenantContext = tenantContext,
            ExpiresAtUtc = expiresAtUtc
        }, cancellationToken: ct));

        return new RefreshTokenIssueResult(token, expiresAtUtc);
    }

    public async Task<RefreshTokenValidationResult?> RotateAsync(string refreshToken, TimeSpan lifetime, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        const string loadSql = """
SELECT TOP 1 Token, Username, ScopesCsv, TenantContext, ExpiresAtUtc
FROM dbo.ApiRefreshTokens
WHERE Token = @Token;
""";

        var existing = await conn.QuerySingleOrDefaultAsync<RefreshTokenEntry>(new CommandDefinition(loadSql, new { Token = refreshToken }, tx, cancellationToken: ct));
        if (existing is null || existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        const string deleteSql = "DELETE FROM dbo.ApiRefreshTokens WHERE Token = @Token;";
        await conn.ExecuteAsync(new CommandDefinition(deleteSql, new { Token = refreshToken }, tx, cancellationToken: ct));

        var scopes = ParseScopes(existing.ScopesCsv);
        var nextToken = CreateToken();
        var nextExpiresAtUtc = DateTime.UtcNow.Add(lifetime);

        const string insertSql = """
INSERT INTO dbo.ApiRefreshTokens (Token, Username, ScopesCsv, TenantContext, ExpiresAtUtc, CreatedAtUtc)
VALUES (@Token, @Username, @ScopesCsv, @TenantContext, @ExpiresAtUtc, SYSUTCDATETIME());
""";

        await conn.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Token = nextToken,
            Username = existing.Username,
            ScopesCsv = existing.ScopesCsv,
            TenantContext = existing.TenantContext,
            ExpiresAtUtc = nextExpiresAtUtc
        }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new RefreshTokenValidationResult(existing.Username, scopes, existing.TenantContext, nextToken, nextExpiresAtUtc);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = "DELETE FROM dbo.ApiRefreshTokens WHERE Token = @Token;";
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Token = refreshToken }, cancellationToken: ct));
    }

    private static async Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.ApiRefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiRefreshTokens
    (
        Token nvarchar(256) NOT NULL PRIMARY KEY,
        Username nvarchar(256) NOT NULL,
        ScopesCsv nvarchar(1024) NOT NULL,
        TenantContext nvarchar(128) NULL,
        ExpiresAtUtc datetime2 NOT NULL,
        CreatedAtUtc datetime2 NOT NULL
    );

    CREATE INDEX IX_ApiRefreshTokens_ExpiresAtUtc ON dbo.ApiRefreshTokens(ExpiresAtUtc);
END
""";

        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));

        const string alterSql = """
IF COL_LENGTH('dbo.ApiRefreshTokens', 'TenantContext') IS NULL
BEGIN
    ALTER TABLE dbo.ApiRefreshTokens ADD TenantContext nvarchar(128) NULL;
END
""";
        await conn.ExecuteAsync(new CommandDefinition(alterSql, cancellationToken: ct));

        const string cleanupSql = "DELETE FROM dbo.ApiRefreshTokens WHERE ExpiresAtUtc < DATEADD(day, -2, SYSUTCDATETIME());";
        await conn.ExecuteAsync(new CommandDefinition(cleanupSql, cancellationToken: ct));
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        return scopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

internal sealed record RefreshTokenEntry(string Token, string Username, string ScopesCsv, string? TenantContext, DateTime ExpiresAtUtc);

internal sealed record RefreshTokenIssueResult(string Token, DateTime ExpiresAtUtc);

internal sealed record RefreshTokenValidationResult(string Username, IReadOnlyList<string> Scopes, string? TenantContext, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
