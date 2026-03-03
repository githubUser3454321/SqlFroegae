using Dapper;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class ScriptFolderRepository : IScriptFolderRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly SqlServerOptions _options;

    public ScriptFolderRepository(ISqlConnectionFactory connectionFactory, Microsoft.Extensions.Options.IOptions<SqlServerOptions> options)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ScriptFolderTreeNode>> GetTreeAsync(CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var rows = (await conn.QueryAsync<ScriptFolder>(new CommandDefinition(@"
SELECT Id, Name, ParentId, SortOrder, CreatedUtc, UpdatedUtc
FROM dbo.ScriptFolders
ORDER BY SortOrder ASC, Name ASC", cancellationToken: ct))).ToList();

        var byParent = rows
            .ToLookup(x => x.ParentId, x => x)
            .ToDictionary(
                g => g.Key ?? Guid.Empty,
                g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList());
        return BuildTree(byParent, null);
    }

    public async Task<ScriptFolder> UpsertAsync(ScriptFolderUpsert input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new InvalidOperationException("Folder-Name ist erforderlich.");
        }

        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var id = input.Id ?? Guid.NewGuid();
        var now = DateTime.UtcNow;
        var normalizedName = input.Name.Trim();

        if (input.ParentId == id)
        {
            throw new InvalidOperationException("Ein Ordner kann nicht sein eigener Parent sein.");
        }

        if (input.ParentId is not null)
        {
            var parentExists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.ScriptFolders WHERE Id = @parentId",
                new { parentId = input.ParentId.Value }, cancellationToken: ct));
            if (parentExists == 0)
            {
                throw new InvalidOperationException("Parent-Ordner wurde nicht gefunden.");
            }
        }

        var duplicate = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.ScriptFolders
WHERE Name = @normalizedName
  AND ((ParentId IS NULL AND @parentId IS NULL) OR ParentId = @parentId)
  AND Id <> @id", new { normalizedName, parentId = input.ParentId, id }, cancellationToken: ct));

        if (duplicate > 0)
        {
            throw new InvalidOperationException("Ein Ordner mit diesem Namen existiert bereits auf derselben Ebene.");
        }

        await conn.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.ScriptFolders AS target
USING (SELECT @Id AS Id) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET Name = @Name, ParentId = @ParentId, SortOrder = @SortOrder, UpdatedUtc = @UpdatedUtc
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ParentId, SortOrder, CreatedUtc, UpdatedUtc)
    VALUES (@Id, @Name, @ParentId, @SortOrder, @CreatedUtc, @UpdatedUtc);", new
        {
            Id = id,
            Name = normalizedName,
            ParentId = input.ParentId,
            SortOrder = input.SortOrder,
            CreatedUtc = now,
            UpdatedUtc = now
        }, cancellationToken: ct));

        await EnsureNoCyclesAsync(conn, id, ct);

        return await conn.QuerySingleAsync<ScriptFolder>(new CommandDefinition(@"
SELECT Id, Name, ParentId, SortOrder, CreatedUtc, UpdatedUtc
FROM dbo.ScriptFolders
WHERE Id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        var hasChildren = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.ScriptFolders WHERE ParentId = @id", new { id }, cancellationToken: ct));
        if (hasChildren > 0)
        {
            throw new InvalidOperationException("Ordner kann nicht gelöscht werden, solange Unterordner existieren.");
        }

        await conn.ExecuteAsync(new CommandDefinition($"UPDATE {_options.ScriptsTable} SET FolderId = NULL WHERE FolderId = @id", new { id }, cancellationToken: ct));
        var affected = await conn.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.ScriptFolders WHERE Id = @id", new { id }, cancellationToken: ct));
        return affected > 0;
    }

    private static async Task EnsureNoCyclesAsync(System.Data.Common.DbConnection conn, Guid startId, CancellationToken ct)
    {
        var cycle = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
;WITH cte AS (
    SELECT Id, ParentId, CAST(CONCAT('/', CONVERT(nvarchar(36), Id), '/') AS nvarchar(max)) AS Path
    FROM dbo.ScriptFolders
    WHERE Id = @startId
    UNION ALL
    SELECT f.Id, f.ParentId, CONCAT(cte.Path, CONVERT(nvarchar(36), f.Id), '/')
    FROM dbo.ScriptFolders f
    INNER JOIN cte ON f.Id = cte.ParentId
)
SELECT COUNT(1)
FROM cte
WHERE ParentId IS NOT NULL AND Path LIKE '%/' + CONVERT(nvarchar(36), ParentId) + '/%';", new { startId }, cancellationToken: ct));

        if (cycle > 0)
        {
            throw new InvalidOperationException("Zyklische Folder-Struktur ist nicht erlaubt.");
        }
    }

    private static List<ScriptFolderTreeNode> BuildTree(Dictionary<Guid, List<ScriptFolder>> byParent, Guid? parentId)
    {
        if (!byParent.TryGetValue(parentId ?? Guid.Empty, out var children))
        {
            return new List<ScriptFolderTreeNode>();
        }

        return children.Select(c => new ScriptFolderTreeNode(c.Id, c.Name, c.ParentId, c.SortOrder, BuildTree(byParent, c.Id))).ToList();
    }

    private Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        return conn.ExecuteAsync(new CommandDefinition($@"
IF OBJECT_ID('dbo.ScriptFolders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptFolders
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        ParentId uniqueidentifier NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ScriptFolders_SortOrder DEFAULT (0),
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_ScriptFolders_Parent FOREIGN KEY (ParentId) REFERENCES dbo.ScriptFolders(Id)
    );

    CREATE UNIQUE INDEX UX_ScriptFolders_Parent_Name ON dbo.ScriptFolders(ParentId, Name);
END;

IF COL_LENGTH('{_options.ScriptsTable}', 'FolderId') IS NULL
BEGIN
    ALTER TABLE {_options.ScriptsTable} ADD FolderId uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_SqlScripts_ScriptFolders_FolderId'
)
BEGIN
    ALTER TABLE {_options.ScriptsTable}
        WITH NOCHECK ADD CONSTRAINT FK_SqlScripts_ScriptFolders_FolderId
        FOREIGN KEY (FolderId) REFERENCES dbo.ScriptFolders(Id);
END;
", cancellationToken: ct));
    }
}
