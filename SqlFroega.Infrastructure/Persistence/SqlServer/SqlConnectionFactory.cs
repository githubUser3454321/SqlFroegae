using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly SqlServerOptions _options;

    public SqlConnectionFactory(IOptions<SqlServerOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new ArgumentException("SqlServerOptions.ConnectionString is required.");
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_options.ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}