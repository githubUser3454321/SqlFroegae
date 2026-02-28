using Dapper;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class CustomerMappingRepository : ICustomerMappingRepository
{
    private readonly ISqlConnectionFactory _connFactory;
    private bool _tableEnsured;

    public CustomerMappingRepository(ISqlConnectionFactory connFactory)
    {
        _connFactory = connFactory;
    }

    public async Task<IReadOnlyList<CustomerMappingItem>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        const string sql = """
SELECT CustomerId, CustomerCode, CustomerName, SchemaName, ObjectPrefix, DatabaseUser
FROM dbo.CustomerMappings
ORDER BY CustomerCode;
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<CustomerMappingItem>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<CustomerMappingItem?> GetByCodeAsync(string customerCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            return null;

        await EnsureTableAsync(ct);

        const string sql = """
SELECT TOP (1) CustomerId, CustomerCode, CustomerName, SchemaName, ObjectPrefix, DatabaseUser
FROM dbo.CustomerMappings
WHERE CustomerCode = @customerCode;
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CustomerMappingItem>(
            new CommandDefinition(sql, new { customerCode = customerCode.Trim() }, cancellationToken: ct));
    }

    public async Task UpsertAsync(CustomerMappingItem mapping, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        const string sql = """
MERGE dbo.CustomerMappings AS target
USING (SELECT
    @CustomerId AS CustomerId,
    @CustomerCode AS CustomerCode,
    @CustomerName AS CustomerName,
    @SchemaName AS SchemaName,
    @ObjectPrefix AS ObjectPrefix,
    @DatabaseUser AS DatabaseUser) AS source
ON target.CustomerId = source.CustomerId
WHEN MATCHED THEN
    UPDATE SET
        CustomerCode = source.CustomerCode,
        CustomerName = source.CustomerName,
        SchemaName = source.SchemaName,
        ObjectPrefix = source.ObjectPrefix,
        DatabaseUser = source.DatabaseUser
WHEN NOT MATCHED THEN
    INSERT (CustomerId, CustomerCode, CustomerName, SchemaName, ObjectPrefix, DatabaseUser)
    VALUES (source.CustomerId, source.CustomerCode, source.CustomerName, source.SchemaName, source.ObjectPrefix, source.DatabaseUser);
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, mapping, cancellationToken: ct));
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableEnsured)
            return;

        const string sql = """
IF OBJECT_ID('dbo.CustomerMappings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerMappings(
        CustomerId uniqueidentifier NOT NULL PRIMARY KEY,
        CustomerCode nvarchar(32) NOT NULL,
        CustomerName nvarchar(256) NOT NULL,
        SchemaName nvarchar(128) NOT NULL,
        ObjectPrefix nvarchar(128) NOT NULL,
        DatabaseUser nvarchar(256) NOT NULL
    );

    CREATE UNIQUE INDEX UX_CustomerMappings_CustomerCode ON dbo.CustomerMappings(CustomerCode);
END
""";

        await using var conn = await _connFactory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        _tableEnsured = true;
    }
}
