using SqlFroega.Application.Models;

namespace SqlFroega.Application.Abstractions;

public interface IScriptFolderRepository
{
    Task<IReadOnlyList<ScriptFolderTreeNode>> GetTreeAsync(CancellationToken ct = default);
    Task<ScriptFolder> UpsertAsync(ScriptFolderUpsert input, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
