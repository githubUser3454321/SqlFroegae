using Dapper;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class ScriptCollectionRepository : IScriptCollectionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ScriptCollectionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ScriptCollection>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var rows = await conn.QueryAsync<ScriptCollection>(new CommandDefinition(@"
SELECT Id, Name, ParentId, OwnerScope, SortOrder, CreatedUtc, UpdatedUtc
FROM dbo.ScriptCollections
ORDER BY SortOrder ASC, Name ASC", cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<ScriptCollection> UpsertAsync(ScriptCollectionUpsert input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new InvalidOperationException("Collection-Name ist erforderlich.");
        }

        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var id = input.Id ?? Guid.NewGuid();
        var now = DateTime.UtcNow;
        var normalizedName = input.Name.Trim();
        var ownerScope = string.Equals(input.OwnerScope?.Trim(), "global", StringComparison.OrdinalIgnoreCase)
            ? "global"
            : "private";

        await conn.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.ScriptCollections AS target
USING (SELECT @Id AS Id) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET Name = @Name, ParentId = @ParentId, OwnerScope = @OwnerScope, SortOrder = @SortOrder, UpdatedUtc = @UpdatedUtc
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ParentId, OwnerScope, SortOrder, CreatedUtc, UpdatedUtc)
    VALUES (@Id, @Name, @ParentId, @OwnerScope, @SortOrder, @CreatedUtc, @UpdatedUtc);", new
        {
            Id = id,
            Name = normalizedName,
            ParentId = input.ParentId,
            OwnerScope = ownerScope,
            SortOrder = input.SortOrder,
            CreatedUtc = now,
            UpdatedUtc = now
        }, cancellationToken: ct));

        return await conn.QuerySingleAsync<ScriptCollection>(new CommandDefinition(@"
SELECT Id, Name, ParentId, OwnerScope, SortOrder, CreatedUtc, UpdatedUtc
FROM dbo.ScriptCollections
WHERE Id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        await conn.ExecuteAsync(new CommandDefinition("UPDATE dbo.ScriptCollections SET ParentId = NULL WHERE ParentId = @id", new { id }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.ScriptCollectionMap WHERE CollectionId = @id", new { id }, cancellationToken: ct));
        var affected = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.ScriptCollections WHERE Id = @id", new { id }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task AssignScriptCollectionsAsync(Guid scriptId, IReadOnlyList<Guid> collectionIds, Guid? primaryCollectionId, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var distinctIds = (collectionIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (primaryCollectionId is not null && !distinctIds.Contains(primaryCollectionId.Value))
        {
            throw new InvalidOperationException("PrimaryCollectionId muss in collectionIds enthalten sein.");
        }

        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.ScriptCollectionMap WHERE ScriptId = @scriptId", new { scriptId }, cancellationToken: ct));

        foreach (var collectionId in distinctIds)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.ScriptCollectionMap (ScriptId, CollectionId, IsPrimary)
VALUES (@ScriptId, @CollectionId, @IsPrimary)", new
            {
                ScriptId = scriptId,
                CollectionId = collectionId,
                IsPrimary = primaryCollectionId == collectionId
            }, cancellationToken: ct));
        }
    }

    private static Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        return conn.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID('dbo.ScriptCollections', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptCollections
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        ParentId uniqueidentifier NULL,
        OwnerScope nvarchar(16) NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ScriptCollections_SortOrder DEFAULT (0),
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_ScriptCollections_Parent FOREIGN KEY (ParentId) REFERENCES dbo.ScriptCollections(Id)
    );

    CREATE INDEX IX_ScriptCollections_Parent ON dbo.ScriptCollections(ParentId, SortOrder, Name);
END;

IF OBJECT_ID('dbo.ScriptCollectionMap', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptCollectionMap
    (
        ScriptId uniqueidentifier NOT NULL,
        CollectionId uniqueidentifier NOT NULL,
        IsPrimary bit NOT NULL CONSTRAINT DF_ScriptCollectionMap_IsPrimary DEFAULT (0),
        CONSTRAINT PK_ScriptCollectionMap PRIMARY KEY (ScriptId, CollectionId),
        CONSTRAINT FK_ScriptCollectionMap_Collection FOREIGN KEY (CollectionId) REFERENCES dbo.ScriptCollections(Id)
    );

    CREATE INDEX IX_ScriptCollectionMap_Collection ON dbo.ScriptCollectionMap(CollectionId, ScriptId);
END;", cancellationToken: ct));
    }
}
