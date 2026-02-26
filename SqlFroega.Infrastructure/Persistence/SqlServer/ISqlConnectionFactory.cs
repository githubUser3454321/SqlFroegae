using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken ct = default);
}
