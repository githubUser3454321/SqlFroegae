using SqlFroega.Application.Models;

namespace SqlFroega.Application.Abstractions;

public interface ISavedViewRepository
{
    Task<IReadOnlyList<SavedView>> GetVisibleAsync(string username, bool includeAll, CancellationToken ct = default);
    Task<SavedView> UpsertAsync(SavedViewUpsert input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string username, bool canDeleteAll, CancellationToken ct = default);
}
