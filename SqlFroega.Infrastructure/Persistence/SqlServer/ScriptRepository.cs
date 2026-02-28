using Dapper;
using Microsoft.Extensions.Options;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Domain;
using SqlFroega.Infrastructure.Parsing;
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
    private readonly SqlObjectReferenceExtractor _referenceExtractor = new();
    private bool? _supportsSoftDelete;

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
        p.Add("@includeDeleted", filters.IncludeDeleted);

        var useSoftDelete = _opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct);

        var temporalInfo = (filters.IncludeDeleted || filters.SearchHistory)
            ? await TryGetTemporalInfoAsync(ct)
            : null;

        if (temporalInfo is not null)
            return await SearchFromTemporalAsync(queryText, filters, temporalInfo.Value, p, ct);

        var sb = new StringBuilder();
        sb.AppendLine("SELECT");
        sb.AppendLine("  s.Id,");
        sb.AppendLine("  s.Name,");
        sb.AppendLine("  s.ScriptKey AS [Key],");
        sb.AppendLine("  CASE s.Scope WHEN 0 THEN 'Global' WHEN 1 THEN 'Customer' WHEN 2 THEN 'Module' ELSE 'Unknown' END AS ScopeLabel,");
        sb.AppendLine("  s.Module AS MainModule,");
        sb.AppendLine("  COALESCE(s.RelatedModules, N'') AS RelatedModules,");
        if (_opt.JoinCustomers)
            sb.AppendLine("  c.Name AS CustomerName,");
        else
            sb.AppendLine("  CAST(NULL AS nvarchar(256)) AS CustomerName,");
        sb.AppendLine("  s.Description,");
        sb.AppendLine("  COALESCE(s.Tags, N'') AS Tags,");
        sb.AppendLine(useSoftDelete
            ? "  CAST(COALESCE(s.IsDeleted, 0) AS bit) AS IsDeleted"
            : "  CAST(0 AS bit) AS IsDeleted");
        sb.AppendLine($"FROM {_opt.ScriptsTable} s");
        if (_opt.JoinCustomers)
            sb.AppendLine($"LEFT JOIN {_opt.CustomersTable} c ON c.Id = s.CustomerId");
        sb.AppendLine("WHERE 1=1");

        if (useSoftDelete)
            sb.AppendLine("AND (COALESCE(s.IsDeleted, 0) = 0 OR @includeDeleted = 1)");

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
            sb.AppendLine("AND (s.Module = @module OR COALESCE(s.RelatedModules, N'') LIKE '%' + @module + '%')");
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
            if (_opt.UseFullTextSearch && string.IsNullOrWhiteSpace(filters.ReferencedObject))
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

        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
        {
            var objectTokens = BuildObjectSearchTokens(filters.ReferencedObject);
            if (objectTokens.Count == 0)
            {
                sb.AppendLine("AND 1 = 0");
            }
            else
            {
                sb.AppendLine($"AND EXISTS (SELECT 1 FROM {_opt.ScriptObjectRefsTable} r WHERE r.ScriptId = s.Id AND r.SearchToken IN @objectTokens)");
                p.Add("@objectTokens", objectTokens);
            }
        }

        sb.AppendLine("ORDER BY s.Name ASC");
        sb.AppendLine("OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
            await EnsureScriptRefsTableAsync(conn, ct);

        var rows = await conn.QueryAsync<ScriptListItemRow>(new CommandDefinition(sb.ToString(), p, cancellationToken: ct));

        return rows.Select(r => new ScriptListItem(
            r.Id,
            r.Name,
            r.Key,
            r.ScopeLabel,
            r.MainModule,
            ParseTags(r.RelatedModules),
            r.CustomerName,
            r.Description,
            ParseTags(r.Tags),
            r.IsDeleted
        )).ToList();
    }

    private async Task<IReadOnlyList<ScriptListItem>> SearchFromTemporalAsync(
        string? queryText,
        ScriptSearchFilters filters,
        TemporalInfo temporalInfo,
        DynamicParameters p,
        CancellationToken ct)
    {
        var fullTable = $"{QuoteIdentifier(temporalInfo.Schema)}.{QuoteIdentifier(temporalInfo.Table)}";
        var validFromColumn = QuoteIdentifier(temporalInfo.ValidFromColumn);
        var validToColumn = QuoteIdentifier(temporalInfo.ValidToColumn);

        p.Add("@openEndedValidTo", DateTime.MaxValue);

        var sb = new StringBuilder();
        sb.AppendLine("WITH version_rows AS (");
        sb.AppendLine("    SELECT");
        sb.AppendLine("      s.Id,");
        sb.AppendLine("      s.Name,");
        sb.AppendLine("      s.ScriptKey AS [Key],");
        sb.AppendLine("      s.Scope,");
        sb.AppendLine("      s.Module AS MainModule,");
        sb.AppendLine("      COALESCE(s.RelatedModules, N'') AS RelatedModules,");
        sb.AppendLine("      s.CustomerId,");
        sb.AppendLine("      s.Description,");
        sb.AppendLine("      COALESCE(s.Tags, N'') AS Tags,");
        sb.AppendLine($"      CAST(s.{validFromColumn} AS datetime2) AS ValidFrom,");
        sb.AppendLine($"      CAST(s.{validToColumn} AS datetime2) AS ValidTo,");
        sb.AppendLine("      ROW_NUMBER() OVER (PARTITION BY s.Id ORDER BY");
        sb.AppendLine($"          CAST(s.{validFromColumn} AS datetime2) DESC,");
        sb.AppendLine($"          CAST(s.{validToColumn} AS datetime2) DESC) AS rn");
        sb.AppendLine($"    FROM {fullTable} FOR SYSTEM_TIME ALL AS s");
        sb.AppendLine("    WHERE 1=1");

        if (filters.Scope is not null)
        {
            sb.AppendLine("      AND s.Scope = @scope");
            p.Add("@scope", filters.Scope.Value);
        }

        if (filters.CustomerId is not null)
        {
            sb.AppendLine("      AND s.CustomerId = @customerId");
            p.Add("@customerId", filters.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Module))
        {
            sb.AppendLine("      AND (s.Module = @module OR COALESCE(s.RelatedModules, N'') LIKE '%' + @module + '%')");
            p.Add("@module", filters.Module);
        }

        if (filters.Tags is { Count: > 0 })
        {
            for (int i = 0; i < filters.Tags.Count; i++)
            {
                var param = $"@tag{i}";
                sb.AppendLine($"      AND s.Tags LIKE '%' + {param} + '%'");
                p.Add(param, filters.Tags[i]);
            }
        }

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            queryText = queryText.Trim();
            p.Add("@q", queryText);

            if (_opt.UseFullTextSearch && string.IsNullOrWhiteSpace(filters.ReferencedObject))
            {
                sb.AppendLine("      AND (");
                sb.AppendLine("          CONTAINS(s.Content, @q) OR CONTAINS(s.Name, @q) OR CONTAINS(s.Description, @q)");
                if (filters.SearchHistory)
                {
                    sb.AppendLine("          OR EXISTS (");
                    sb.AppendLine($"              SELECT 1 FROM {fullTable} FOR SYSTEM_TIME ALL AS hs");
                    sb.AppendLine("              WHERE hs.Id = s.Id");
                    sb.AppendLine("                AND (CONTAINS(hs.Content, @q) OR CONTAINS(hs.Name, @q) OR CONTAINS(hs.Description, @q))");
                    sb.AppendLine("          )");
                }
                sb.AppendLine("      )");
            }
            else
            {
                sb.AppendLine("      AND (");
                sb.AppendLine("          s.Content LIKE '%' + @q + '%' OR s.Name LIKE '%' + @q + '%' OR s.Description LIKE '%' + @q + '%'");
                if (filters.SearchHistory)
                {
                    sb.AppendLine("          OR EXISTS (");
                    sb.AppendLine($"              SELECT 1 FROM {fullTable} FOR SYSTEM_TIME ALL AS hs");
                    sb.AppendLine("              WHERE hs.Id = s.Id");
                    sb.AppendLine("                AND (hs.Content LIKE '%' + @q + '%' OR hs.Name LIKE '%' + @q + '%' OR hs.Description LIKE '%' + @q + '%')");
                    sb.AppendLine("          )");
                }
                sb.AppendLine("      )");
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
        {
            var objectTokens = BuildObjectSearchTokens(filters.ReferencedObject);
            if (objectTokens.Count == 0)
            {
                sb.AppendLine("      AND 1 = 0");
            }
            else
            {
                sb.AppendLine($"      AND EXISTS (SELECT 1 FROM {_opt.ScriptObjectRefsTable} r WHERE r.ScriptId = s.Id AND r.SearchToken IN @objectTokens)");
                p.Add("@objectTokens", objectTokens);
            }
        }

        sb.AppendLine(")");
        sb.AppendLine("SELECT");
        sb.AppendLine("  vr.Id,");
        sb.AppendLine("  vr.Name,");
        sb.AppendLine("  vr.[Key],");
        sb.AppendLine("  CASE vr.Scope WHEN 0 THEN 'Global' WHEN 1 THEN 'Customer' WHEN 2 THEN 'Module' ELSE 'Unknown' END AS ScopeLabel,");
        sb.AppendLine("  vr.MainModule,");
        sb.AppendLine("  vr.RelatedModules,");
        if (_opt.JoinCustomers)
            sb.AppendLine("  c.Name AS CustomerName,");
        else
            sb.AppendLine("  CAST(NULL AS nvarchar(256)) AS CustomerName,");
        sb.AppendLine("  vr.Description,");
        sb.AppendLine("  vr.Tags,");
        sb.AppendLine("  CAST(CASE WHEN vr.ValidTo < @openEndedValidTo THEN 1 ELSE 0 END AS bit) AS IsDeleted");
        sb.AppendLine("FROM version_rows vr");
        if (_opt.JoinCustomers)
            sb.AppendLine($"LEFT JOIN {_opt.CustomersTable} c ON c.Id = vr.CustomerId");
        sb.AppendLine("WHERE vr.rn = 1");
        if (!filters.IncludeDeleted)
            sb.AppendLine("  AND vr.ValidTo >= @openEndedValidTo");
        sb.AppendLine("ORDER BY vr.Name ASC");
        sb.AppendLine("OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
            await EnsureScriptRefsTableAsync(conn, ct);

        var rows = await conn.QueryAsync<ScriptListItemRow>(new CommandDefinition(sb.ToString(), p, cancellationToken: ct));

        return rows.Select(r => new ScriptListItem(
            r.Id,
            r.Name,
            r.Key,
            r.ScopeLabel,
            r.MainModule,
            ParseTags(r.RelatedModules),
            r.CustomerName,
            r.Description,
            ParseTags(r.Tags),
            r.IsDeleted
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
        sql.AppendLine("  s.Module AS MainModule,");
        sql.AppendLine("  COALESCE(s.RelatedModules, N'') AS RelatedModules,");
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
        sql.AppendLine("WHERE s.Id = @id");
        if (_opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct))
            sql.AppendLine("  AND COALESCE(s.IsDeleted, 0) = 0;");
        else
            sql.AppendLine(";");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
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
            row.MainModule,
            ParseTags(row.RelatedModules),
            row.CustomerId,
            row.CustomerName,
            row.Description,
            ParseTags(row.Tags)
        );
    }

    public async Task<Guid> UpsertAsync(ScriptUpsert script, CancellationToken ct = default)
    {
        var useSoftDelete = _opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct);

        var sql = useSoftDelete
            ? $@"
DECLARE @resolvedId uniqueidentifier = ISNULL(@Id, NEWID());

MERGE {_opt.ScriptsTable} AS target
USING (SELECT 
        @resolvedId AS Id,
        @Name AS Name,
        @Key AS ScriptKey,
        @Content AS Content,
        @Scope AS Scope,
        @CustomerId AS CustomerId,
        @MainModule AS Module,
        @RelatedModules AS RelatedModules,
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
        RelatedModules = src.RelatedModules,
        Description = src.Description,
        Tags = src.Tags,
        IsDeleted = 0
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ScriptKey, Content, Scope, CustomerId, Module, RelatedModules, Description, Tags, IsDeleted)
    VALUES (src.Id, src.Name, src.ScriptKey, src.Content, src.Scope, src.CustomerId, src.Module, src.RelatedModules, src.Description, src.Tags, 0);

SELECT @resolvedId;
"
            : $@"
DECLARE @resolvedId uniqueidentifier = ISNULL(@Id, NEWID());

MERGE {_opt.ScriptsTable} AS target
USING (SELECT 
        @resolvedId AS Id,
        @Name AS Name,
        @Key AS ScriptKey,
        @Content AS Content,
        @Scope AS Scope,
        @CustomerId AS CustomerId,
        @MainModule AS Module,
        @RelatedModules AS RelatedModules,
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
        RelatedModules = src.RelatedModules,
        Description = src.Description,
        Tags = src.Tags
WHEN NOT MATCHED THEN
    INSERT (Id, Name, ScriptKey, Content, Scope, CustomerId, Module, RelatedModules, Description, Tags)
    VALUES (src.Id, src.Name, src.ScriptKey, src.Content, src.Scope, src.CustomerId, src.Module, src.RelatedModules, src.Description, src.Tags);

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
            MainModule = script.MainModule,
            RelatedModules = ToTagsStorage(script.RelatedModules),
            script.Description,
            Tags = tagsText
        };

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
        var id = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, args, cancellationToken: ct));

        var refs = _referenceExtractor.Extract(script.Content);
        await RebuildScriptReferencesAsync(conn, id, refs, ct);

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
    COALESCE({changedByExpr}, N'') AS ChangedBy,
    COALESCE(s.Content, N'') AS Content
FROM {fullTable} FOR SYSTEM_TIME ALL AS s
WHERE s.Id = @id
ORDER BY s.{validFromColumn} DESC;";

        var rows = await conn.QueryAsync<ScriptHistoryRow>(
            new CommandDefinition(sql, new { id, take }, cancellationToken: ct));

        return rows.Select(r => new ScriptHistoryItem
        {
            ValidFrom = r.ValidFrom,
            ValidTo = r.ValidTo,
            ChangedBy = r.ChangedBy ?? string.Empty,
            Content = r.Content ?? string.Empty
        }).ToList();
    }


    public async Task<ScriptMetadataCatalog> GetMetadataCatalogAsync(bool includeDeleted = false, CancellationToken ct = default)
    {
        var useSoftDelete = _opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct);

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        var sb = new StringBuilder();
        sb.AppendLine("SELECT DISTINCT Value FROM (");
        sb.AppendLine("    SELECT LTRIM(RTRIM(m.Name)) AS Value");
        sb.AppendLine($"    FROM {_opt.ModulesTable} m");
        sb.AppendLine("    UNION");
        sb.AppendLine("    SELECT LTRIM(RTRIM(s.Module)) AS Value");
        sb.AppendLine($"    FROM {_opt.ScriptsTable} s");
        sb.AppendLine("    WHERE s.Module IS NOT NULL");
        if (useSoftDelete)
            sb.AppendLine("      AND (COALESCE(s.IsDeleted, 0) = 0 OR @includeDeleted = 1)");
        sb.AppendLine(") src");
        sb.AppendLine("WHERE LTRIM(RTRIM(Value)) <> N''");
        sb.AppendLine("ORDER BY Value ASC;");

        sb.AppendLine("SELECT DISTINCT LTRIM(RTRIM(split.value)) AS Value");
        sb.AppendLine($"FROM {_opt.ScriptsTable} s");
        sb.AppendLine("CROSS APPLY STRING_SPLIT(REPLACE(REPLACE(REPLACE(COALESCE(s.RelatedModules, N''), '[', N''), ']', N''), CHAR(34), N''), ',') split");
        sb.AppendLine("WHERE LTRIM(RTRIM(split.value)) <> N''");
        if (useSoftDelete)
            sb.AppendLine("  AND (COALESCE(s.IsDeleted, 0) = 0 OR @includeDeleted = 1)");
        sb.AppendLine("ORDER BY Value ASC;");

        sb.AppendLine("SELECT DISTINCT LTRIM(RTRIM(split.value)) AS Value");
        sb.AppendLine($"FROM {_opt.ScriptsTable} s");
        sb.AppendLine("CROSS APPLY STRING_SPLIT(REPLACE(REPLACE(REPLACE(COALESCE(s.Tags, N''), '[', N''), ']', N''), CHAR(34), N''), ',') split");
        sb.AppendLine("WHERE LTRIM(RTRIM(split.value)) <> N''");
        if (useSoftDelete)
            sb.AppendLine("  AND (COALESCE(s.IsDeleted, 0) = 0 OR @includeDeleted = 1)");
        sb.AppendLine("ORDER BY Value ASC;");

        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            sb.ToString(),
            new { includeDeleted },
            cancellationToken: ct));

        var managedModules = (await multi.ReadAsync<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var relatedModules = (await multi.ReadAsync<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var tags = (await multi.ReadAsync<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new ScriptMetadataCatalog(managedModules, relatedModules, tags);
    }

    public async Task<IReadOnlyList<string>> GetManagedModulesAsync(CancellationToken ct = default)
    {
        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        var sql = $"SELECT LTRIM(RTRIM(m.Name)) FROM {_opt.ModulesTable} m WHERE LTRIM(RTRIM(m.Name)) <> N'' ORDER BY m.Name";
        var modules = await conn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
        return modules.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task AddModuleAsync(string moduleName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new InvalidOperationException("Module name is required.");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        var sql = $@"IF NOT EXISTS (SELECT 1 FROM {_opt.ModulesTable} WHERE Name = @moduleName)
INSERT INTO {_opt.ModulesTable} (Name) VALUES (@moduleName);";
        await conn.ExecuteAsync(new CommandDefinition(sql, new { moduleName = moduleName.Trim() }, cancellationToken: ct));
    }

    public async Task RemoveModuleAsync(string moduleName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            return;

        var normalized = moduleName.Trim();
        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        var scriptsSql = $@"
UPDATE {_opt.ScriptsTable}
SET Module = CASE WHEN Module = @moduleName THEN NULL ELSE Module END,
    RelatedModules = CASE
        WHEN COALESCE(RelatedModules, N'') = N'' THEN RelatedModules
        ELSE REPLACE(REPLACE(REPLACE(COALESCE(RelatedModules, N''), CONCAT('"', @moduleName, '"'), N''), ',,', ','), '[,', '[')
    END
WHERE Module = @moduleName OR COALESCE(RelatedModules, N'') LIKE '%' + @moduleName + '%';";

        await conn.ExecuteAsync(new CommandDefinition(scriptsSql, new { moduleName = normalized }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition($"DELETE FROM {_opt.ModulesTable} WHERE Name = @moduleName", new { moduleName = normalized }, cancellationToken: ct));
    }

    private sealed record ScriptListItemRow(
        Guid Id,
        string Name,
        string Key,
        string ScopeLabel,
        string? MainModule,
        string RelatedModules,
        string? CustomerName,
        string? Description,
        string Tags,
        bool IsDeleted
    );

    private sealed record ScriptDetailRow(
        Guid Id,
        string Name,
        string Key,
        string Content,
        string ScopeLabel,
        string? MainModule,
        string RelatedModules,
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

    private readonly record struct TemporalInfo(
        string Schema,
        string Table,
        string ValidFromColumn,
        string ValidToColumn
    );

    private sealed record ScriptHistoryRow(
        DateTime ValidFrom,
        DateTime ValidTo,
        string? ChangedBy,
        string? Content
    );

    private sealed record ScriptReferenceRow(
        Guid ScriptId,
        string ObjectName,
        DbObjectType ObjectType
    );

    public async Task<IReadOnlyList<ScriptReferenceItem>> FindByReferencedObjectAsync(string objectName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return Array.Empty<ScriptReferenceItem>();

        var objectTokens = BuildObjectSearchTokens(objectName);

        const string sql = @"
SELECT r.ScriptId,
       r.ObjectName,
       r.ObjectType
FROM {0} r
WHERE r.ObjectName = @objectName
   OR r.SearchToken IN @objectTokens
ORDER BY r.CreatedAt DESC;";

        await using var conn = await _connFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ScriptReferenceRow>(
            new CommandDefinition(string.Format(sql, _opt.ScriptObjectRefsTable), new { objectName, objectTokens }, cancellationToken: ct));

        return rows
            .DistinctBy(x => new { x.ScriptId, x.ObjectName, x.ObjectType })
            .Select(x => new ScriptReferenceItem(x.ScriptId, x.ObjectName, x.ObjectType))
            .ToList();
    }

    private async Task RebuildScriptReferencesAsync(System.Data.Common.DbConnection conn, Guid scriptId, IReadOnlyList<DbObjectRef> refs, CancellationToken ct)
    {
        await EnsureScriptRefsTableAsync(conn, ct);

        var deleteSql = $"DELETE FROM {_opt.ScriptObjectRefsTable} WHERE ScriptId = @scriptId;";
        await conn.ExecuteAsync(new CommandDefinition(deleteSql, new { scriptId }, cancellationToken: ct));

        if (refs.Count == 0)
            return;

        var insertSql = $@"
INSERT INTO {_opt.ScriptObjectRefsTable}(ScriptId, ObjectName, SearchToken, ObjectType, CreatedAt)
VALUES(@ScriptId, @ObjectName, @SearchToken, @ObjectType, SYSUTCDATETIME());";

        var rows = refs
            .SelectMany(x => BuildReferenceSearchTokens(x.Name)
                .Select(token => new
                {
                    ScriptId = scriptId,
                    ObjectName = x.Name,
                    SearchToken = token,
                    ObjectType = (int)x.Type
                }))
            .DistinctBy(x => new { x.ScriptId, x.ObjectName, x.SearchToken, x.ObjectType });

        await conn.ExecuteAsync(new CommandDefinition(insertSql, rows, cancellationToken: ct));
    }

    private async Task EnsureScriptRefsTableAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        var (schemaName, tableName) = SplitSchemaAndTable(_opt.ScriptObjectRefsTable);

        const string sql = @"
IF OBJECT_ID(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'U') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(
        ScriptId uniqueidentifier NOT NULL,
        ObjectName nvarchar(512) NOT NULL,
        SearchToken nvarchar(512) NOT NULL,
        ObjectType int NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ScriptObjectRefs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ScriptObjectRefs_SearchToken ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(SearchToken, ScriptId);
    CREATE INDEX IX_ScriptObjectRefs_ObjectName ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(ObjectName);';
    EXEC(@sql);
END

IF COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'SearchToken') IS NULL
BEGIN
    DECLARE @sqlAddColumn nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' ADD SearchToken nvarchar(512) NULL;';
    EXEC(@sqlAddColumn);

    DECLARE @sqlBackfill nvarchar(max) = N'UPDATE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' SET SearchToken = ObjectName WHERE SearchToken IS NULL;';
    EXEC(@sqlBackfill);

    DECLARE @sqlAlterColumn nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' ALTER COLUMN SearchToken nvarchar(512) NOT NULL;';
    EXEC(@sqlAlterColumn);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON t.object_id = i.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @schemaName
      AND t.name = @tableName
      AND i.name = 'IX_ScriptObjectRefs_SearchToken'
)
BEGIN
    DECLARE @sqlCreateSearchTokenIndex nvarchar(max) = N'CREATE INDEX IX_ScriptObjectRefs_SearchToken ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(SearchToken, ScriptId);';
    EXEC(@sqlCreateSearchTokenIndex);
END";

        await conn.ExecuteAsync(new CommandDefinition(sql, new { schemaName, tableName }, cancellationToken: ct));
    }

    private static IReadOnlyList<string> BuildObjectSearchTokens(string rawObjectText)
    {
        var raw = NormalizeIdentifier(rawObjectText);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Array.Empty<string>();

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parts.Length == 1)
        {
            var objectName = parts[0];
            if (IsSqlKeyword(objectName))
                return Array.Empty<string>();

            tokens.Add(objectName);
            var simplified = SimplifyTableName(objectName);
            if (!string.Equals(objectName, simplified, StringComparison.OrdinalIgnoreCase))
                tokens.Add(simplified);

            return tokens.ToList();
        }

        if (parts.Length == 2)
        {
            var first = parts[0];
            var second = parts[1];

            if (IsSqlKeyword(first) || IsSqlKeyword(second))
                return Array.Empty<string>();

            tokens.Add($"{first}.{second}");

            var simplifiedTable = SimplifyTableName(first);
            if (!string.Equals(simplifiedTable, first, StringComparison.OrdinalIgnoreCase))
                tokens.Add($"{simplifiedTable}.{second}");

            return tokens.ToList();
        }

        var schema = parts[^3];
        var table = parts[^2];
        var obj = parts[^1];

        if (IsSqlKeyword(schema) || IsSqlKeyword(table) || IsSqlKeyword(obj))
            return Array.Empty<string>();

        tokens.Add($"{schema}.{table}.{obj}");

        return tokens.ToList();
    }

    private static IReadOnlyList<string> BuildReferenceSearchTokens(string objectName)
    {
        var normalized = NormalizeIdentifier(objectName);
        if (string.IsNullOrWhiteSpace(normalized))
            return Array.Empty<string>();

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Array.Empty<string>();

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalized };

        if (parts.Length == 1)
        {
            if (!IsSqlKeyword(parts[0]))
                tokens.Add(parts[0]);

            return tokens.ToList();
        }

        if (parts.Length == 2)
        {
            var table = parts[1];
            tokens.Add(table);

            var simplified = SimplifyTableName(table);
            if (!string.Equals(simplified, table, StringComparison.OrdinalIgnoreCase))
                tokens.Add(simplified);

            return tokens.ToList();
        }

        var schema = parts[^3];
        var tableName = parts[^2];
        var objectSegment = parts[^1];

        tokens.Add(objectSegment);
        tokens.Add($"{tableName}.{objectSegment}");

        var simplifiedTableName = SimplifyTableName(tableName);
        if (!string.Equals(simplifiedTableName, tableName, StringComparison.OrdinalIgnoreCase))
            tokens.Add($"{simplifiedTableName}.{objectSegment}");

        tokens.Add(tableName);
        tokens.Add($"{schema}.{tableName}");

        return tokens.Where(t => !IsSqlKeyword(t)).ToList();
    }

    private static string NormalizeIdentifier(string? raw)
        => (raw ?? string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Trim()
            .ToLowerInvariant();

    private static string SimplifyTableName(string tableName)
    {
        var normalized = NormalizeIdentifier(tableName);
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('_'))
            return normalized;

        var idx = normalized.IndexOf('_');
        if (idx < 0 || idx == normalized.Length - 1)
            return normalized;

        return normalized[(idx + 1)..];
    }

    private static bool IsSqlKeyword(string input)
    {
        var normalized = NormalizeIdentifier(input);
        return normalized.Length > 0 && SqlKeywords.Contains(normalized);
    }

    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "join", "inner", "left", "right", "full", "cross", "on",
        "and", "or", "not", "is", "null", "insert", "update", "delete", "merge", "into",
        "create", "alter", "drop", "table", "view", "function", "procedure", "exec", "execute",
        "with", "as", "group", "by", "order", "having", "distinct", "top", "case", "when",
        "then", "else", "end", "union", "all", "exists", "in", "like", "between", "use"
    };

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
        await using var conn = await _connFactory.OpenAsync(ct);

        if (_opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct))
        {
            var softDeleteSql = $"UPDATE {_opt.ScriptsTable} SET IsDeleted = 1 WHERE Id = @id;";
            await conn.ExecuteAsync(new CommandDefinition(softDeleteSql, new { id }, cancellationToken: ct));
            return;
        }

        var hardDeleteSql = $"DELETE FROM {_opt.ScriptsTable} WHERE Id = @id;";
        await conn.ExecuteAsync(new CommandDefinition(hardDeleteSql, new { id }, cancellationToken: ct));
    }

    private async Task<bool> SupportsSoftDeleteAsync(CancellationToken ct)
    {
        if (_supportsSoftDelete.HasValue)
            return _supportsSoftDelete.Value;

        var (schemaName, tableName) = SplitSchemaAndTable(_opt.ScriptsTable);

        const string sql = """
SELECT CASE
         WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'IsDeleted') IS NULL THEN CAST(0 AS bit)
         ELSE CAST(1 AS bit)
       END;
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        var result = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { schemaName, tableName }, cancellationToken: ct));

        _supportsSoftDelete = result;
        return result;
    }

    private async Task EnsureModuleSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        var sql = $@"
IF OBJECT_ID('{_opt.ModulesTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {_opt.ModulesTable}
    (
        Name nvarchar(128) NOT NULL PRIMARY KEY
    );
END;

IF COL_LENGTH('{_opt.ScriptsTable}', 'RelatedModules') IS NULL
BEGIN
    ALTER TABLE {_opt.ScriptsTable} ADD RelatedModules nvarchar(max) NULL;
END;";

        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
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

    private async Task<TemporalInfo?> TryGetTemporalInfoAsync(CancellationToken ct)
    {
        var (schemaName, tableName) = SplitSchemaAndTable(_opt.ScriptsTable);

        const string metadataSql = """
SELECT TOP 1
    CAST(t.temporal_type AS int) AS TemporalType,
    startCol.name AS ValidFromColumn,
    endCol.name AS ValidToColumn,
    CAST(NULL AS nvarchar(256)) AS ChangedByColumn
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
            return null;

        if (string.IsNullOrWhiteSpace(metadata.ValidFromColumn) || string.IsNullOrWhiteSpace(metadata.ValidToColumn))
            return null;

        return new TemporalInfo(schemaName, tableName, metadata.ValidFromColumn, metadata.ValidToColumn);
    }
}
