using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptDetail(
    Guid Id,
    string Name,
    int NumberId,
    string Content,
    string ScopeLabel,
    string? MainModule,
    IReadOnlyList<string> RelatedModules,
    Guid? CustomerId,
    string? CustomerName,
    string? Description,
    IReadOnlyList<string> Tags
);
