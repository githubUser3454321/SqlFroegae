using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptSearchFilters(
    int? Scope,              // null = alle; sonst 0/1/2
    Guid? CustomerId,
    string? Module,
    string? MainModule,
    string? RelatedModule,
    IReadOnlyList<string>? Tags,
    string? ReferencedObject,
    bool IncludeDeleted = false,
    bool SearchHistory = false
);
