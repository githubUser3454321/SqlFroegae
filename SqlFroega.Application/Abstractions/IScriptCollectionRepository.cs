using SqlFroega.Application.Models;

namespace SqlFroega.Application.Abstractions;

public interface IScriptCollectionRepository
{
    Task<IReadOnlyList<ScriptCollection>> GetAllAsync(CancellationToken ct = default);
    Task<ScriptCollection> UpsertAsync(ScriptCollectionUpsert input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task AssignScriptCollectionsAsync(Guid scriptId, IReadOnlyList<Guid> collectionIds, Guid? primaryCollectionId, CancellationToken ct = default);
}
