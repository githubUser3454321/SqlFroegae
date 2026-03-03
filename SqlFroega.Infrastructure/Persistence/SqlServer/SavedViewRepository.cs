using Dapper;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Application.Services;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class SavedViewRepository : ISavedViewRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SavedViewRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SavedView>> GetVisibleAsync(string username, bool includeAll, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var normalizedUsername = (username ?? string.Empty).Trim();
        var sql = includeAll
            ? @"SELECT Id, Name, OwnerUsername, Visibility, DefinitionJson, CreatedUtc, UpdatedUtc
FROM dbo.SavedViews
ORDER BY UpdatedUtc DESC, Name ASC"
            : @"SELECT Id, Name, OwnerUsername, Visibility, DefinitionJson, CreatedUtc, UpdatedUtc
FROM dbo.SavedViews
WHERE Visibility = 'global' OR OwnerUsername = @normalizedUsername
ORDER BY UpdatedUtc DESC, Name ASC";

        var rows = await conn.QueryAsync<SavedView>(new CommandDefinition(sql, new { normalizedUsername }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<SavedView> UpsertAsync(SavedViewUpsert input, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var now = DateTime.UtcNow;
        var id = input.Id ?? Guid.NewGuid();
        var visibility = SearchProfileVisibility.NormalizeForStorage(input.Visibility);

        await conn.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.SavedViews AS target
USING (SELECT @Id AS Id) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET
        Name = @Name,
        Visibility = @Visibility,
        DefinitionJson = @DefinitionJson,
        UpdatedUtc = @UpdatedUtc
WHEN NOT MATCHED THEN
    INSERT (Id, Name, OwnerUsername, Visibility, DefinitionJson, CreatedUtc, UpdatedUtc)
    VALUES (@Id, @Name, @OwnerUsername, @Visibility, @DefinitionJson, @CreatedUtc, @UpdatedUtc);",
            new
            {
                Id = id,
                Name = input.Name.Trim(),
                OwnerUsername = input.OwnerUsername.Trim(),
                Visibility = visibility,
                DefinitionJson = input.DefinitionJson,
                CreatedUtc = now,
                UpdatedUtc = now
            },
            cancellationToken: ct));

        return await conn.QuerySingleAsync<SavedView>(new CommandDefinition(@"
SELECT Id, Name, OwnerUsername, Visibility, DefinitionJson, CreatedUtc, UpdatedUtc
FROM dbo.SavedViews
WHERE Id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, string username, bool canDeleteAll, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var normalizedUsername = (username ?? string.Empty).Trim();
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
DELETE FROM dbo.SavedViews
WHERE Id = @id
  AND (@canDeleteAll = 1 OR OwnerUsername = @normalizedUsername)",
            new { id, canDeleteAll, normalizedUsername },
            cancellationToken: ct));

        return affected > 0;
    }

    private static Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        return conn.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID('dbo.SavedViews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SavedViews
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        OwnerUsername nvarchar(128) NOT NULL,
        Visibility nvarchar(16) NOT NULL,
        DefinitionJson nvarchar(max) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL
    );

    CREATE INDEX IX_SavedViews_OwnerVisibility ON dbo.SavedViews(OwnerUsername, Visibility);
    CREATE INDEX IX_SavedViews_UpdatedUtc ON dbo.SavedViews(UpdatedUtc DESC);
END;", cancellationToken: ct));
    }
}
