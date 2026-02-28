using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptUpsert(
    Guid? Id,
    string Name,
    string Key,
    string Content,
    int Scope,                // für DB/Transport erstmal int (0/1/2)
    Guid? CustomerId,
    string? MainModule,
    IReadOnlyList<string> RelatedModules,
    string? Description,
    IReadOnlyList<string> Tags
);
