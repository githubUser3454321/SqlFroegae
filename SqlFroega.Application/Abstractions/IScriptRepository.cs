using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Application.Abstractions;

public interface IScriptRepository
{
    Task<IReadOnlyList<ScriptListItem>> SearchAsync(
        string? queryText,
        ScriptSearchFilters filters,
        int take = 200,
        int skip = 0,
        CancellationToken ct = default);

    Task<ScriptDetail?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> UpsertAsync(ScriptUpsert script, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ScriptHistoryItem>> GetHistoryAsync(
        Guid id,
        int take = 50,
        CancellationToken ct = default);
}
