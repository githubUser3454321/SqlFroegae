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
        sb.AppendLine("  s.NumberId,");
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

        if (!string.IsNullOrWhiteSpace(filters.MainModule))
        {
            sb.AppendLine("AND s.Module = @mainModule");
            p.Add("@mainModule", filters.MainModule);
        }

        if (!string.IsNullOrWhiteSpace(filters.RelatedModule))
        {
            sb.AppendLine("AND COALESCE(s.RelatedModules, N'') LIKE '%' + @relatedModule + '%'");
            p.Add("@relatedModule", filters.RelatedModule);
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
                sb.AppendLine("AND (CONTAINS(s.Content, @q) OR CONTAINS(s.Name, @q) OR CONTAINS(s.Description, @q) OR s.Module LIKE '%' + @qlike + '%' OR COALESCE(s.RelatedModules, N'') LIKE '%' + @qlike + '%' OR COALESCE(s.Tags, N'') LIKE '%' + @qlike + '%')");
                p.Add("@q", queryText);
                p.Add("@qlike", queryText);
            }
            else
            {
                sb.AppendLine("AND (s.Content LIKE '%' + @q + '%' OR s.Name LIKE '%' + @q + '%' OR s.Description LIKE '%' + @q + '%' OR s.Module LIKE '%' + @q + '%' OR COALESCE(s.RelatedModules, N'') LIKE '%' + @q + '%' OR COALESCE(s.Tags, N'') LIKE '%' + @q + '%')");
                p.Add("@q", queryText);
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
        {
            var objectSearchText = NormalizeIdentifier(filters.ReferencedObject);
            if (string.IsNullOrWhiteSpace(objectSearchText))
            {
                sb.AppendLine("AND 1 = 0");
            }
            else
            {
                sb.AppendLine($"AND EXISTS (SELECT 1 FROM {_opt.ScriptObjectRefsTable} r WHERE r.ScriptId = s.Id AND LOWER(r.ObjectName) LIKE '%' + @objectSearchText + '%')");
                p.Add("@objectSearchText", objectSearchText);
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
            r.NumberId,
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
        sb.AppendLine("      s.NumberId,");
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

        if (!string.IsNullOrWhiteSpace(filters.MainModule))
        {
            sb.AppendLine("      AND s.Module = @mainModule");
            p.Add("@mainModule", filters.MainModule);
        }

        if (!string.IsNullOrWhiteSpace(filters.RelatedModule))
        {
            sb.AppendLine("      AND COALESCE(s.RelatedModules, N'') LIKE '%' + @relatedModule + '%'");
            p.Add("@relatedModule", filters.RelatedModule);
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
                sb.AppendLine("          CONTAINS(s.Content, @q) OR CONTAINS(s.Name, @q) OR CONTAINS(s.Description, @q) OR s.Module LIKE '%' + @q + '%' OR COALESCE(s.RelatedModules, N'') LIKE '%' + @q + '%' OR COALESCE(s.Tags, N'') LIKE '%' + @q + '%'");
                if (filters.SearchHistory)
                {
                    sb.AppendLine("          OR EXISTS (");
                    sb.AppendLine($"              SELECT 1 FROM {fullTable} FOR SYSTEM_TIME ALL AS hs");
                    sb.AppendLine("              WHERE hs.Id = s.Id");
                    sb.AppendLine("                AND (CONTAINS(hs.Content, @q) OR CONTAINS(hs.Name, @q) OR CONTAINS(hs.Description, @q) OR hs.Module LIKE '%' + @q + '%' OR COALESCE(hs.RelatedModules, N'') LIKE '%' + @q + '%' OR COALESCE(hs.Tags, N'') LIKE '%' + @q + '%')");
                    sb.AppendLine("          )");
                }
                sb.AppendLine("      )");
            }
            else
            {
                sb.AppendLine("      AND (");
                sb.AppendLine("          s.Content LIKE '%' + @q + '%' OR s.Name LIKE '%' + @q + '%' OR s.Description LIKE '%' + @q + '%' OR s.Module LIKE '%' + @q + '%' OR COALESCE(s.RelatedModules, N'') LIKE '%' + @q + '%' OR COALESCE(s.Tags, N'') LIKE '%' + @q + '%'");
                if (filters.SearchHistory)
                {
                    sb.AppendLine("          OR EXISTS (");
                    sb.AppendLine($"              SELECT 1 FROM {fullTable} FOR SYSTEM_TIME ALL AS hs");
                    sb.AppendLine("              WHERE hs.Id = s.Id");
                    sb.AppendLine("                AND (hs.Content LIKE '%' + @q + '%' OR hs.Name LIKE '%' + @q + '%' OR hs.Description LIKE '%' + @q + '%' OR hs.Module LIKE '%' + @q + '%' OR COALESCE(hs.RelatedModules, N'') LIKE '%' + @q + '%' OR COALESCE(hs.Tags, N'') LIKE '%' + @q + '%')");
                    sb.AppendLine("          )");
                }
                sb.AppendLine("      )");
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.ReferencedObject))
        {
            var objectSearchText = NormalizeIdentifier(filters.ReferencedObject);
            if (string.IsNullOrWhiteSpace(objectSearchText))
            {
                sb.AppendLine("      AND 1 = 0");
            }
            else
            {
                sb.AppendLine($"      AND EXISTS (SELECT 1 FROM {_opt.ScriptObjectRefsTable} r WHERE r.ScriptId = s.Id AND LOWER(r.ObjectName) LIKE '%' + @objectSearchText + '%')");
                p.Add("@objectSearchText", objectSearchText);
            }
        }

        sb.AppendLine(")");
        sb.AppendLine("SELECT");
        sb.AppendLine("  vr.Id,");
        sb.AppendLine("  vr.Name,");
        sb.AppendLine("  vr.NumberId,");
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
            r.NumberId,
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
        sql.AppendLine("  s.NumberId,");
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
            row.NumberId,
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

    public async Task<Guid?> GetIdByNumberIdAsync(int numberId, CancellationToken ct = default)
    {
        if (numberId <= 0)
            return null;

        var sql = new StringBuilder();
        sql.AppendLine("SELECT TOP 1 s.Id");
        sql.AppendLine($"FROM {_opt.ScriptsTable} s");
        sql.AppendLine("WHERE s.NumberId = @numberId");
        if (_opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct))
            sql.AppendLine("  AND COALESCE(s.IsDeleted, 0) = 0;");
        else
            sql.AppendLine(";");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql.ToString(), new { numberId }, cancellationToken: ct));
    }

    public async Task<ScriptEditAwareness?> RegisterViewAsync(Guid scriptId, string? username, CancellationToken ct = default)
    {
        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        DateTime? lastViewedAt = null;

        if (!string.IsNullOrWhiteSpace(username))
        {
            var normalizedUser = username.Trim();
            const string getSeenSql = "SELECT TOP 1 LastViewedAt FROM dbo.ScriptViewLog WHERE ScriptId = @scriptId AND Username = @username;";
            lastViewedAt = await conn.QuerySingleOrDefaultAsync<DateTime?>(
                new CommandDefinition(getSeenSql, new { scriptId, username = normalizedUser }, cancellationToken: ct));

            const string upsertSeenSql = @"
MERGE dbo.ScriptViewLog AS target
USING (SELECT @scriptId AS ScriptId, @username AS Username) src
ON target.ScriptId = src.ScriptId AND target.Username = src.Username
WHEN MATCHED THEN
    UPDATE SET LastViewedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ScriptId, Username, LastViewedAt)
    VALUES (src.ScriptId, src.Username, SYSUTCDATETIME());";

            await conn.ExecuteAsync(new CommandDefinition(upsertSeenSql, new { scriptId, username = normalizedUser }, cancellationToken: ct));
        }

        var snapshot = await TryGetLastUpdateSnapshotAsync(conn, scriptId, ct);
        return new ScriptEditAwareness(lastViewedAt, snapshot?.UpdatedAt, snapshot?.UpdatedBy);
    }

    public async Task<ScriptEditAwareness?> GetEditAwarenessAsync(Guid scriptId, string? username, CancellationToken ct = default)
    {
        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        DateTime? lastViewedAt = null;
        if (!string.IsNullOrWhiteSpace(username))
        {
            const string getSeenSql = "SELECT TOP 1 LastViewedAt FROM dbo.ScriptViewLog WHERE ScriptId = @scriptId AND Username = @username;";
            lastViewedAt = await conn.QuerySingleOrDefaultAsync<DateTime?>(
                new CommandDefinition(getSeenSql, new { scriptId, username = username.Trim() }, cancellationToken: ct));
        }

        var snapshot = await TryGetLastUpdateSnapshotAsync(conn, scriptId, ct);
        return new ScriptEditAwareness(lastViewedAt, snapshot?.UpdatedAt, snapshot?.UpdatedBy);
    }

    public async Task<ScriptLockResult> TryAcquireEditLockAsync(Guid scriptId, string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @existingOwner nvarchar(256);
SELECT @existingOwner = LockedBy
FROM dbo.RecordInUse WITH (UPDLOCK, HOLDLOCK)
WHERE ScriptId = @scriptId;

IF @existingOwner IS NULL
BEGIN
    INSERT INTO dbo.RecordInUse (ScriptId, LockedBy, LockedAt)
    VALUES (@scriptId, @username, SYSUTCDATETIME());
    COMMIT;
    SELECT CAST(1 AS bit) AS Acquired, CAST(NULL AS nvarchar(256)) AS LockedBy;
    RETURN;
END

IF @existingOwner = @username
BEGIN
    UPDATE dbo.RecordInUse
    SET LockedAt = SYSUTCDATETIME()
    WHERE ScriptId = @scriptId;

    COMMIT;
    SELECT CAST(1 AS bit) AS Acquired, CAST(NULL AS nvarchar(256)) AS LockedBy;
    RETURN;
END

COMMIT;
SELECT CAST(0 AS bit) AS Acquired, @existingOwner AS LockedBy;";

        var result = await conn.QuerySingleAsync<ScriptLockResult>(
            new CommandDefinition(sql, new { scriptId, username = username.Trim() }, cancellationToken: ct));

        return result;
    }

    public async Task ReleaseEditLockAsync(Guid scriptId, string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return;

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        const string sql = "DELETE FROM dbo.RecordInUse WHERE ScriptId = @scriptId AND LockedBy = @username;";
        await conn.ExecuteAsync(new CommandDefinition(sql, new { scriptId, username = username.Trim() }, cancellationToken: ct));
    }

    public async Task ClearEditLocksAsync(string? username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return;

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.RecordInUse WHERE LockedBy = @username;",
            new { username = username.Trim() },
            cancellationToken: ct));
    }

    public async Task<Guid> UpsertAsync(ScriptUpsert script, CancellationToken ct = default)
    {
        var useSoftDelete = _opt.EnableSoftDelete && await SupportsSoftDeleteAsync(ct);
        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);
        await ValidateModulesExistAsync(conn, script.MainModule, script.RelatedModules, ct);

        var auditColumns = await TryGetUpsertAuditColumnsAsync(conn, ct);
        var changedByIdentifier = auditColumns.ChangedByColumn is null ? null : QuoteIdentifier(auditColumns.ChangedByColumn);
        var reasonIdentifier = auditColumns.ChangeReasonColumn is null ? null : QuoteIdentifier(auditColumns.ChangeReasonColumn);
        var changedBySource = changedByIdentifier is null ? string.Empty : ",\n        @UpdatedBy AS UpdatedBy";
        var reasonSource = reasonIdentifier is null ? string.Empty : ",\n        @UpdateReason AS UpdateReason";
        var changedByUpdate = changedByIdentifier is null ? string.Empty : $",\n        {changedByIdentifier} = src.UpdatedBy";
        var reasonUpdate = reasonIdentifier is null ? string.Empty : $",\n        {reasonIdentifier} = src.UpdateReason";
        var changedByInsertColumn = changedByIdentifier is null ? string.Empty : $", {changedByIdentifier}";
        var reasonInsertColumn = reasonIdentifier is null ? string.Empty : $", {reasonIdentifier}";
        var changedByInsertValue = changedByIdentifier is null ? string.Empty : ", src.UpdatedBy";
        var reasonInsertValue = reasonIdentifier is null ? string.Empty : ", src.UpdateReason";

        var sql = useSoftDelete
            ? $@"
DECLARE @resolvedId uniqueidentifier = ISNULL(@Id, NEWID());

MERGE {_opt.ScriptsTable} AS target
USING (SELECT 
        @resolvedId AS Id,
        @Name AS Name,
        @Content AS Content,
        @Scope AS Scope,
        @CustomerId AS CustomerId,
        @MainModule AS Module,
        @RelatedModules AS RelatedModules,
        @Description AS Description,
        @Tags AS Tags{changedBySource}{reasonSource}
) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET
        Name = src.Name,
        Content = src.Content,
        Scope = src.Scope,
        CustomerId = src.CustomerId,
        Module = src.Module,
        RelatedModules = src.RelatedModules,
        Description = src.Description,
        Tags = src.Tags,
        IsDeleted = 0{changedByUpdate}{reasonUpdate}
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Content, Scope, CustomerId, Module, RelatedModules, Description, Tags, IsDeleted{changedByInsertColumn}{reasonInsertColumn})
    VALUES (src.Id, src.Name, src.Content, src.Scope, src.CustomerId, src.Module, src.RelatedModules, src.Description, src.Tags, 0{changedByInsertValue}{reasonInsertValue});

SELECT @resolvedId;
"
            : $@"
DECLARE @resolvedId uniqueidentifier = ISNULL(@Id, NEWID());

MERGE {_opt.ScriptsTable} AS target
USING (SELECT 
        @resolvedId AS Id,
        @Name AS Name,
        @Content AS Content,
        @Scope AS Scope,
        @CustomerId AS CustomerId,
        @MainModule AS Module,
        @RelatedModules AS RelatedModules,
        @Description AS Description,
        @Tags AS Tags{changedBySource}{reasonSource}
) AS src
ON target.Id = src.Id
WHEN MATCHED THEN
    UPDATE SET
        Name = src.Name,
        Content = src.Content,
        Scope = src.Scope,
        CustomerId = src.CustomerId,
        Module = src.Module,
        RelatedModules = src.RelatedModules,
        Description = src.Description,
        Tags = src.Tags{changedByUpdate}{reasonUpdate}
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Content, Scope, CustomerId, Module, RelatedModules, Description, Tags{changedByInsertColumn}{reasonInsertColumn})
    VALUES (src.Id, src.Name, src.Content, src.Scope, src.CustomerId, src.Module, src.RelatedModules, src.Description, src.Tags{changedByInsertValue}{reasonInsertValue});

SELECT @resolvedId;
";

        var tagsText = ToTagsStorage(script.Tags);

        var args = new
        {
            Id = script.Id,
            script.Name,
            script.Content,
            script.Scope,
            script.CustomerId,
            MainModule = script.MainModule,
            RelatedModules = ToTagsStorage(script.RelatedModules),
            script.Description,
            Tags = tagsText,
            UpdatedBy = string.IsNullOrWhiteSpace(script.UpdatedBy) ? null : script.UpdatedBy.Trim(),
            UpdateReason = string.IsNullOrWhiteSpace(script.UpdateReason) ? null : script.UpdateReason.Trim()
        };

        var id = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, args, cancellationToken: ct));

        var refs = _referenceExtractor.Extract(script.Content);
        await RebuildScriptReferencesAsync(conn, id, refs, ct);

        return id;
    }

    private async Task<UpsertAuditColumns> TryGetUpsertAuditColumnsAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        var (schemaName, tableName) = SplitSchemaAndTable(_opt.ScriptsTable);

        const string sql = """
SELECT
    CASE
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'ChangedBy') IS NOT NULL THEN 'ChangedBy'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'ModifiedBy') IS NOT NULL THEN 'ModifiedBy'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'UpdatedBy') IS NOT NULL THEN 'UpdatedBy'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'LastModifiedBy') IS NOT NULL THEN 'LastModifiedBy'
        ELSE NULL
    END AS ChangedByColumn,
    CASE
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'ChangeReason') IS NOT NULL THEN 'ChangeReason'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'UpdateReason') IS NOT NULL THEN 'UpdateReason'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'ModifiedReason') IS NOT NULL THEN 'ModifiedReason'
        WHEN COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'Reason') IS NOT NULL THEN 'Reason'
        ELSE NULL
    END AS ChangeReasonColumn;
""";

        var row = await conn.QuerySingleOrDefaultAsync<UpsertAuditColumns>(
            new CommandDefinition(sql, new { schemaName, tableName }, cancellationToken: ct));

        return row ?? new UpsertAuditColumns(null, null);
    }


    private async Task ValidateModulesExistAsync(System.Data.Common.DbConnection conn, string? mainModule, IReadOnlyList<string>? relatedModules, CancellationToken ct)
    {
        var requestedModules = (relatedModules ?? Array.Empty<string>())
            .Append(mainModule ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedModules.Count == 0)
            return;

        var sql = $"SELECT LTRIM(RTRIM(Name)) FROM {_opt.ModulesTable} WHERE Name IN @modules";
        var existingModules = (await conn.QueryAsync<string>(new CommandDefinition(sql, new { modules = requestedModules }, cancellationToken: ct)))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = requestedModules.Where(x => !existingModules.Contains(x)).ToList();
        if (missing.Count == 0)
            return;

        throw new InvalidOperationException($"Unbekannte Module: {string.Join(", ", missing)}. Neue Module können nur in der Modulverwaltung angelegt werden.");
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
    END AS ChangedByColumn,
    CASE
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'UpdateReason') IS NOT NULL THEN 'UpdateReason'
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'ChangeReason') IS NOT NULL THEN 'ChangeReason'
        WHEN COL_LENGTH(QUOTENAME(ss.name) + '.' + QUOTENAME(t.name), 'Reason') IS NOT NULL THEN 'Reason'
        ELSE NULL
    END AS ChangeReasonColumn
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
        var changeReasonExpr = metadata.ChangeReasonColumn is null
            ? "N''"
            : $"CONVERT(nvarchar(max), s.{QuoteIdentifier(metadata.ChangeReasonColumn)})";

        var sql = $@"
SELECT TOP (@take)
    CAST(s.{validFromColumn} AS datetime2) AS ValidFrom,
    CAST(s.{validToColumn} AS datetime2) AS ValidTo,
    COALESCE({changedByExpr}, N'') AS ChangedBy,
    COALESCE({changeReasonExpr}, N'') AS ChangeReason,
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
            ChangeReason = r.ChangeReason ?? string.Empty,
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

    public async Task RenameModuleAsync(string currentModuleName, string newModuleName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentModuleName))
            throw new InvalidOperationException("Current module name is required.");

        if (string.IsNullOrWhiteSpace(newModuleName))
            throw new InvalidOperationException("New module name is required.");

        var current = currentModuleName.Trim();
        var next = newModuleName.Trim();
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
            return;

        await using var conn = await _connFactory.OpenAsync(ct);
        await EnsureModuleSchemaAsync(conn, ct);

        var existingNew = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM {_opt.ModulesTable} WHERE Name = @name",
            new { name = next },
            cancellationToken: ct));

        if (existingNew > 0)
            throw new InvalidOperationException($"Modul '{next}' existiert bereits.");

        var existingCurrent = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM {_opt.ModulesTable} WHERE Name = @name",
            new { name = current },
            cancellationToken: ct));

        if (existingCurrent == 0)
            throw new InvalidOperationException($"Modul '{current}' wurde nicht gefunden.");

        var scriptsSql = $@"
UPDATE {_opt.ScriptsTable}
SET Module = CASE WHEN Module = @current THEN @next ELSE Module END,
    RelatedModules = CASE
        WHEN COALESCE(RelatedModules, N'') = N'' THEN RelatedModules
        ELSE REPLACE(COALESCE(RelatedModules, N''), CONCAT('""', @current, '""'), CONCAT('""', @next, '""'))
    END
WHERE Module = @current OR COALESCE(RelatedModules, N'') LIKE '%' + @current + '%';";

        await conn.ExecuteAsync(new CommandDefinition(scriptsSql, new { current, next }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition($"UPDATE {_opt.ModulesTable} SET Name = @next WHERE Name = @current", new { current, next }, cancellationToken: ct));
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
        ELSE REPLACE(REPLACE(REPLACE(COALESCE(RelatedModules, N''), CONCAT('""', @moduleName, '""'), N''), ',,', ','), '[,', '[')
    END
WHERE Module = @moduleName OR COALESCE(RelatedModules, N'') LIKE '%' + @moduleName + '%';"; // string litteral bug fixxed by relacing <CONCAT('"', @moduleName, '"')> with <CONCAT('""', @moduleName, '""')>

        await conn.ExecuteAsync(new CommandDefinition(scriptsSql, new { moduleName = normalized }, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition($"DELETE FROM {_opt.ModulesTable} WHERE Name = @moduleName", new { moduleName = normalized }, cancellationToken: ct));
    }

    private sealed record ScriptListItemRow(
        Guid Id,
        string Name,
        int NumberId,
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
        int NumberId,
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
        string? ChangedByColumn,
        string? ChangeReasonColumn
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
        string? ChangeReason,
        string? Content
    );

    private sealed record ScriptReferenceRow(
        Guid ScriptId,
        string ObjectName,
        DbObjectType ObjectType
    );

    private sealed record UpsertAuditColumns(
        string? ChangedByColumn,
        string? ChangeReasonColumn
    );

    private sealed record LastUpdateSnapshot(
        DateTime UpdatedAt,
        string? UpdatedBy
    );

    public async Task<IReadOnlyList<ScriptReferenceItem>> FindByReferencedObjectAsync(string objectName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return Array.Empty<ScriptReferenceItem>();

        var objectSearchText = NormalizeIdentifier(objectName);
        if (string.IsNullOrWhiteSpace(objectSearchText))
            return Array.Empty<ScriptReferenceItem>();

        const string sql = @"
SELECT r.ScriptId,
       r.ObjectName,
       r.ObjectType
FROM {0} r
WHERE LOWER(r.ObjectName) LIKE '%' + @objectSearchText + '%';";

        await using var conn = await _connFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ScriptReferenceRow>(
            new CommandDefinition(string.Format(sql, _opt.ScriptObjectRefsTable), new { objectSearchText }, cancellationToken: ct));

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
INSERT INTO {_opt.ScriptObjectRefsTable}(ScriptId, ObjectName, ObjectType)
VALUES(@ScriptId, @ObjectName, @ObjectType);";

        var rows = refs
            .Select(x => new
            {
                ScriptId = scriptId,
                ObjectName = NormalizeIdentifier(x.Name),
                ObjectType = (int)x.Type
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ObjectName))
            .DistinctBy(x => new { x.ScriptId, x.ObjectName, x.ObjectType });

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
        ObjectType int NOT NULL
    );
    CREATE UNIQUE INDEX UX_ScriptObjectRefs_ScriptId_ObjectName_ObjectType ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(ScriptId, ObjectName, ObjectType);
    CREATE INDEX IX_ScriptObjectRefs_ObjectName ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(ObjectName);';
    EXEC(@sql);
END

IF COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'SearchToken') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes i
        INNER JOIN sys.tables t ON t.object_id = i.object_id
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @schemaName
          AND t.name = @tableName
          AND i.name = 'IX_ScriptObjectRefs_SearchToken'
    )
    BEGIN
        DECLARE @sqlDropSearchTokenIndex nvarchar(max) = N'DROP INDEX IX_ScriptObjectRefs_SearchToken ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N';';
        EXEC(@sqlDropSearchTokenIndex);
    END

    DECLARE @sqlDropSearchTokenColumn nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' DROP COLUMN SearchToken;';
    EXEC(@sqlDropSearchTokenColumn);
END

IF COL_LENGTH(QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName), 'CreatedAt') IS NOT NULL
BEGIN
    DECLARE @sqlDropCreatedAtDefault nvarchar(max) = N'';

    SELECT TOP 1 @sqlDropCreatedAtDefault = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @schemaName
      AND t.name = @tableName
      AND c.name = 'CreatedAt';

    IF @sqlDropCreatedAtDefault <> N''
        EXEC(@sqlDropCreatedAtDefault);

    DECLARE @sqlDropCreatedAtColumn nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' DROP COLUMN CreatedAt;';
    EXEC(@sqlDropCreatedAtColumn);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON t.object_id = i.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @schemaName
      AND t.name = @tableName
      AND i.name = 'UX_ScriptObjectRefs_ScriptId_ObjectName_ObjectType'
)
BEGIN
    DECLARE @sqlDeduplicate nvarchar(max) = N';WITH dedupe AS (
        SELECT ROW_NUMBER() OVER (PARTITION BY ScriptId, ObjectName, ObjectType ORDER BY (SELECT NULL)) AS rn
        FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'
    )
    DELETE FROM dedupe WHERE rn > 1;';
    EXEC(@sqlDeduplicate);

    DECLARE @sqlCreateUniqueIndex nvarchar(max) = N'CREATE UNIQUE INDEX UX_ScriptObjectRefs_ScriptId_ObjectName_ObjectType ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(ScriptId, ObjectName, ObjectType);';
    EXEC(@sqlCreateUniqueIndex);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.tables t ON t.object_id = i.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @schemaName
      AND t.name = @tableName
      AND i.name = 'IX_ScriptObjectRefs_ObjectName'
)
BEGIN
    DECLARE @sqlCreateObjectNameIndex nvarchar(max) = N'CREATE INDEX IX_ScriptObjectRefs_ObjectName ON ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N'(ObjectName);';
    EXEC(@sqlCreateObjectNameIndex);
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
END;

IF COL_LENGTH('{_opt.ScriptsTable}', 'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE {_opt.ScriptsTable} ADD UpdatedBy nvarchar(256) NULL;
END;

IF COL_LENGTH('{_opt.ScriptsTable}', 'UpdateReason') IS NULL
BEGIN
    ALTER TABLE {_opt.ScriptsTable} ADD UpdateReason nvarchar(max) NULL;
END;


IF COL_LENGTH('{_opt.ScriptsTable}', 'NumberId') IS NULL
BEGIN
    DECLARE @sqlAddNumberId nvarchar(max) = N'ALTER TABLE {_opt.ScriptsTable} ADD NumberId int IDENTITY(1,1) NOT NULL;';
    EXEC(@sqlAddNumberId);
END;

IF COL_LENGTH('{_opt.ScriptsTable}', 'ScriptKey') IS NOT NULL
BEGIN
    DECLARE @sqlMakeScriptKeyNullable nvarchar(max) = N'ALTER TABLE {_opt.ScriptsTable} ALTER COLUMN ScriptKey nvarchar(512) NULL;';
    EXEC(@sqlMakeScriptKeyNullable);
END;

IF OBJECT_ID('dbo.ScriptViewLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptViewLog
    (
        ScriptId uniqueidentifier NOT NULL,
        Username nvarchar(256) NOT NULL,
        LastViewedAt datetime2 NOT NULL,
        CONSTRAINT PK_ScriptViewLog PRIMARY KEY (ScriptId, Username)
    );
END;

IF OBJECT_ID('dbo.RecordInUse', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecordInUse
    (
        ScriptId uniqueidentifier NOT NULL PRIMARY KEY,
        LockedBy nvarchar(256) NOT NULL,
        LockedAt datetime2 NOT NULL
    );
END;";
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        }
        catch
        {
            throw new Exception("Database coruppted, drop everything and recreate the database.");
        }
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
    CAST(NULL AS nvarchar(256)) AS ChangedByColumn,
    CAST(NULL AS nvarchar(256)) AS ChangeReasonColumn
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

    private async Task<LastUpdateSnapshot?> TryGetLastUpdateSnapshotAsync(System.Data.Common.DbConnection conn, Guid scriptId, CancellationToken ct)
    {
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
    END AS ChangedByColumn,
    CAST(NULL AS nvarchar(256)) AS ChangeReasonColumn
FROM sys.tables t
INNER JOIN sys.schemas ss ON ss.schema_id = t.schema_id
LEFT JOIN sys.periods p ON p.object_id = t.object_id
LEFT JOIN sys.columns startCol ON startCol.object_id = t.object_id AND startCol.column_id = p.start_column_id
LEFT JOIN sys.columns endCol ON endCol.object_id = t.object_id AND endCol.column_id = p.end_column_id
WHERE ss.name = @schemaName AND t.name = @tableName;
""";

        var metadata = await conn.QuerySingleOrDefaultAsync<TemporalMetadataRow>(
            new CommandDefinition(metadataSql, new { schemaName, tableName }, cancellationToken: ct));

        if (metadata is null || metadata.TemporalType != 2 || string.IsNullOrWhiteSpace(metadata.ValidFromColumn))
            return null;

        var fullTable = $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
        var validFromColumn = QuoteIdentifier(metadata.ValidFromColumn);
        var changedByExpr = metadata.ChangedByColumn is null
            ? "CAST(NULL AS nvarchar(256))"
            : $"CONVERT(nvarchar(256), s.{QuoteIdentifier(metadata.ChangedByColumn)})";

        var sql = $@"
SELECT TOP 1
    CAST(s.{validFromColumn} AS datetime2) AS UpdatedAt,
    {changedByExpr} AS UpdatedBy
FROM {fullTable} s
WHERE s.Id = @scriptId;";

        return await conn.QuerySingleOrDefaultAsync<LastUpdateSnapshot>(
            new CommandDefinition(sql, new { scriptId }, cancellationToken: ct));
    }
}
