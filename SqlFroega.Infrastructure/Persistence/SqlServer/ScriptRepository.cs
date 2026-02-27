using Dapper;
using Microsoft.Extensions.Options;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class ScriptRepository : IScriptRepository
{
    private readonly ISqlConnectionFactory _connFactory;
    private readonly SqlServerOptions _opt;

    public ScriptRepository(ISqlConnectionFactory connFactory, IOptions<SqlServerOptions> options)
    {
        _connFactory = connFactory;
        _opt = options.Value;
    }

    public async Task<IReadOnlyList<ScriptListItem>> SearchAsync(
        string? queryText,
        ScriptSearchFilters filters,
        int take = 200,
        int skip = 0,
        CancellationToken ct = default)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        if (skip < 0) skip = 0;

        var p = new DynamicParameters();
        p.Add("@take", take);
        p.Add("@skip", skip);

        var sb = new StringBuilder();
        sb.AppendLine("SELECT");
        sb.AppendLine("  s.Id,");
        sb.AppendLine("  s.Name,");
        sb.AppendLine("  s.ScriptKey AS [Key],");
        sb.AppendLine("  CASE s.Scope WHEN 0 THEN 'Global' WHEN 1 THEN 'Customer' WHEN 2 THEN 'Module' ELSE 'Unknown' END AS ScopeLabel,");
        sb.AppendLine("  s.Module,");
        if (_opt.JoinCustomers)
            sb.AppendLine("  c.Name AS CustomerName,");
        else
            sb.AppendLine("  CAST(NULL AS nvarchar(256)) AS CustomerName,");
        sb.AppendLine("  s.Description,");
        sb.AppendLine("  COALESCE(s.Tags, N'') AS Tags");
        sb.AppendLine($"FROM {_opt.ScriptsTable} s");
        if (_opt.JoinCustomers)
            sb.AppendLine($"LEFT JOIN {_opt.CustomersTable} c ON c.Id = s.CustomerId");
        sb.AppendLine("WHERE 1=1");

        if (filters.Scope is not null)
        {
            sb.AppendLine("AND s.Scope = @scope");
            p.Add("@scope", filters.Scope.Value);
        }

        if (filters.CustomerId is not null)
        {
            sb.AppendLine("AND s.CustomerId = @customerId");
            p.Add("@customerId", filters.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Module))
        {
            sb.AppendLine("AND s.Module = @module");
            p.Add("@module", filters.Module);
        }

        if (filters.Tags is { Count: > 0 })
        {
            for (int i = 0; i < filters.Tags.Count; i++)
            {
                var param = $"@tag{i}";
                sb.AppendLine($"AND s.Tags LIKE '%' + {param} + '%'");
                p.Add(param, filters.Tags[i]);
            }
        }

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            queryText = queryText.Trim();
            if (_opt.UseFullTextSearch)
            {
                sb.AppendLine("AND (CONTAINS(s.Content, @q) OR CONTAINS(s.Name, @q) OR CONTAINS(s.Description, @q))");
                p.Add("@q", queryText);
            }
            else
            {
                sb.AppendLine("AND (s.Content LIKE '%' + @q + '%' OR s.Name LIKE '%' + @q + '%' OR s.Description LIKE '%' + @q + '%')");
                p.Add("@q", queryText);
            }
        }

        sb.AppendLine("ORDER BY s.Name ASC");
        sb.AppendLine("OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;");

        await using var conn = await _connFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ScriptListItemRow>(new CommandDefinition(sb.ToString(), p, cancellationToken: ct));

        return rows.Select(r => new ScriptListItem(
            r.Id,
            r.Name,
            r.Key,
            r.ScopeLabel,
            r.Module,
            r.CustomerName,
            r.Description,
            ParseTags(r.Tags)
        )).ToList();
    }

    public async Task<ScriptDetail?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql = new StringBuilder();
        sql.AppendLine("SELECT TOP 1");
        sql.AppendLine("  s.Id,");
        sql.AppendLine("  s.Name,");
        sql.AppendLine("  s.ScriptKey AS [Key],");
        sql.AppendLine("  s.Content,");
        sql.AppendLine("  CASE s.Scope WHEN 0 THEN 'Global' WHEN 1 THEN 'Customer' WHEN 2 THEN 'Module' ELSE 'Unknown' END AS ScopeLabel,");
        sql.AppendLine("  s.Module,");
        sql.AppendLine("  s.CustomerId,");
        if (_opt.JoinCustomers)
            sql.AppendLine("  c.Name AS CustomerName,");
        else
            sql.AppendLine("  CAST(NULL AS nvarchar(256)) AS CustomerName,");
        sql.AppendLine("  s.Description,");
        sql.AppendLine("  COALESCE(s.Tags, N'') AS Tags");
        sql.AppendLine($"FROM {_opt.ScriptsTable} s");
        if (_opt.JoinCustomers)
            sql.AppendLine($"LEFT JOIN {_opt.CustomersTable} c ON c.Id = s.CustomerId");
        sql.AppendLine("WHERE s.Id = @id;");

        await using var conn = await _connFactory.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<ScriptDetailRow>(
            new CommandDefinition(sql.ToString(), new { id }, cancellationToken: ct));

        if (row is null)
            return null;

        return new ScriptDetail(
            row.Id,
            row.Name,
            row.Key,
            row.Content,
            row.ScopeLabel,
            row.Module,
            row.CustomerId,
            row.CustomerName,
            row.Description,
            ParseTags(row.Tags)
        );
    }

    public async Task<Guid> UpsertAsync(ScriptUpsert script, CancellationToken ct = default)
    {
        var sql = $@"
DECLARE @resolvedId uniqueidentifier = ISNULL(@Id, NEWID());

MERGE {_opt.ScriptsTable} AS target
USING (SELECT 
        @resolvedId AS Id,
        @Name AS Name,
        @Key AS ScriptKey,
        @Content AS Content,
        @Scope AS Scope,
        @CustomerId AS CustomerId,
        @Module AS Module,
        @Description AS Description,
        @Tags AS Tags
) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET
        Name = src.Name,
        ScriptKey = src.ScriptKey,
        Content = src.Content,
        Scope = src.Scope,
        CustomerId = src.CustomerId,
        Module = src.Module,
        Description = src.Description,
        Tags = src.Tags
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ScriptKey, Content, Scope, CustomerId, Module, Description, Tags)
    VALUES (src.Id, src.Name, src.ScriptKey, src.Content, src.Scope, src.CustomerId, src.Module, src.Description, src.Tags);

SELECT @resolvedId;
";

        var tagsText = ToTagsStorage(script.Tags);

        var args = new
        {
            Id = script.Id,
            script.Name,
            Key = script.Key,
            script.Content,
            script.Scope,
            script.CustomerId,
            script.Module,
            script.Description,
            Tags = tagsText
        };

        await using var conn = await _connFactory.OpenAsync(ct);
        var id = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, args, cancellationToken: ct));
        return id;
    }

    public async Task<IReadOnlyList<ScriptHistoryItem>> GetHistoryAsync(Guid id, int take = 50, CancellationToken ct = default)
    {
        if (take <= 0) take = 20;
        if (take > 200) take = 200;

        var (schemaName, tableName) = SplitSchemaAndTable(_opt.ScriptsTable);

        const string metadataSql = """
SELECT TOP 1
    CAST(t.temporal_type AS int) AS TemporalType,
    startCol.name AS ValidFromColumn,
    endCol.name AS ValidToColumn,
    CASE
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'ChangedBy') IS NOT NULL THEN 'ChangedBy'
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'ModifiedBy') IS NOT NULL THEN 'ModifiedBy'
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'UpdatedBy') IS NOT NULL THEN 'UpdatedBy'
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'LastModifiedBy') IS NOT NULL THEN 'LastModifiedBy'
        ELSE NULL
    END AS ChangedByColumn
FROM sys.tables t
INNER JOIN sys.schemas ss ON ss.schema_id = t.schema_id
LEFT JOIN sys.periods p ON p.object_id = t.object_id
LEFT JOIN sys.columns startCol ON startCol.object_id = t.object_id AND startCol.column_id = p.start_column_id
LEFT JOIN sys.columns endCol ON endCol.object_id = t.object_id AND endCol.column_id = p.end_column_id
WHERE ss.name = @schemaName AND t.name = @tableName;
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        var metadata = await conn.QuerySingleOrDefaultAsync<TemporalMetadataRow>(
            new CommandDefinition(metadataSql, new { schemaName, tableName }, cancellationToken: ct));

        if (metadata is null || metadata.TemporalType != 2)
            return Array.Empty<ScriptHistoryItem>();

        if (string.IsNullOrWhiteSpace(metadata.ValidFromColumn) || string.IsNullOrWhiteSpace(metadata.ValidToColumn))
            return Array.Empty<ScriptHistoryItem>();

        var fullTable = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        var validFromColumn = QuoteIdentifier(metadata.ValidFromColumn);
        var validToColumn = QuoteIdentifier(metadata.ValidToColumn);
        var changedByExpr = metadata.ChangedByColumn is null
            ? "N''"
            : $"CONVERT(nvarchar(256), s.{QuoteIdentifier(metadata.ChangedByColumn)})";

        var sql = $@"
SELECT TOP (@take)
    CAST(s.{validFromColumn} AS datetime2) AS ValidFrom,
    CAST(s.{validToColumn} AS datetime2) AS ValidTo,
    COALESCE({changedByExpr}, N'') AS ChangedBy
FROM {fullTable} FOR SYSTEM_TIME ALL AS s
WHERE s.Id = @id
ORDER BY s.{validFromColumn} DESC;";

        var rows = await conn.QueryAsync<ScriptHistoryRow>(
            new CommandDefinition(sql, new { id, take }, cancellationToken: ct));

        return rows.Select(r => new ScriptHistoryItem
        {
            ValidFrom = r.ValidFrom,
            ValidTo = r.ValidTo,
            ChangedBy = r.ChangedBy ?? string.Empty
        }).ToList();
    }

    private sealed record ScriptListItemRow(
        Guid Id,
        string Name,
        string Key,
        string ScopeLabel,
        string? Module,
        string? CustomerName,
        string? Description,
        string Tags
    );

    private sealed record ScriptDetailRow(
        Guid Id,
        string Name,
        string Key,
        string Content,
        string ScopeLabel,
        string? Module,
        Guid? CustomerId,
        string? CustomerName,
        string? Description,
        string Tags
    );

    private sealed record TemporalMetadataRow(
        int TemporalType,
        string? ValidFromColumn,
        string? ValidToColumn,
        string? ChangedByColumn
    );

    private sealed record ScriptHistoryRow(
        DateTime ValidFrom,
        DateTime ValidTo,
        string? ChangedBy
    );

    private static IReadOnlyList<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return Array.Empty<string>();

        tags = tags.Trim();

        if (tags.StartsWith("[") && tags.EndsWith("]"))
        {
            var inner = tags[1..^1].Trim();
            if (inner.Length == 0) return Array.Empty<string>();

            return inner.Split(',')
                .Select(x => x.Trim().Trim('"'))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ToTagsStorage(IReadOnlyList<string> tags)
    {
        if (tags is null || tags.Count == 0) return "[]";

        var cleaned = tags
            .Select(t => (t ?? "").Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Replace("\"", "\\\""));

        return "[\"" + string.Join("\",\"", cleaned) + "\"]";
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = $"DELETE FROM {_opt.ScriptsTable} WHERE Id = @id;";
        await using var conn = await _connFactory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    private static (string Schema, string Table) SplitSchemaAndTable(string rawTable)
    {
        var normalized = (rawTable ?? string.Empty)
            .Trim()
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("dbo", "SqlScripts"),
            1 => ("dbo", parts[0]),
            _ => (parts[^2], parts[^1])
        };
    }

    private static string QuoteIdentifier(string name)
        => $"[{(name ?? string.Empty).Replace("]", "]]")}]";
}
