using System;

namespace SqlFroega.SsmsExtension.ToolWindows;

internal sealed record SearchResultItem(
    Guid Id,
    string Name,
    int NumberId,
    string Scope,
    string MainModule,
    string Description);
