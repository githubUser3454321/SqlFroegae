using System;
using System.Collections.Generic;

namespace SqlFroega.Domain;

public sealed record Script(
    Guid Id,
    string Name,
    string Key, // "Path/Key": eindeutiger Key, z.B. "/global/billing/Rebuild.sql"
    string Content,
    ScriptScope Scope,
    Guid? CustomerId,
    string? Module,
    string? Description,
    IReadOnlyList<string> Tags
);