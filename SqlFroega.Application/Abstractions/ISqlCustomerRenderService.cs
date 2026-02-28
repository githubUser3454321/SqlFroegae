using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Application.Abstractions;

public interface ISqlCustomerRenderService
{
    Task<string> NormalizeForStorageAsync(string sql, CancellationToken ct = default);
    Task<string> RenderForCustomerAsync(string sql, string customerCode, CancellationToken ct = default);
}
