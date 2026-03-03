using SqlFroega.Application.Models;

namespace SqlFroega.Application.Abstractions;

public interface ISearchProfileRepository
{
    Task<IReadOnlyList<SearchProfile>> GetVisibleAsync(string username, bool includeAll, CancellationToken ct = default);
    Task<SearchProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SearchProfile> UpsertAsync(SearchProfileUpsert input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string username, bool canDeleteAll, CancellationToken ct = default);
}
