using System;
using System.Collections.Generic;

namespace SqlFroega.Domain;

public sealed record Script(
    Guid Id,
    string Name,
    int NumberId,
    string Content,
    ScriptScope Scope,
    Guid? CustomerId,
    string? MainModule,
    IReadOnlyList<string> RelatedModules,
    string? Description,
    IReadOnlyList<string> Tags
);
