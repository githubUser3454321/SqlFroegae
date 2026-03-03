using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptSearchFilters(
    int? Scope,              // null = alle; sonst 0/1/2
    Guid? CustomerId,
    string? Module,
    string? MainModule,
    string? RelatedModule,
    IReadOnlyList<string>? RelatedModules,
    IReadOnlyList<string>? Tags,
    string? ReferencedObject,
    IReadOnlyList<string>? ReferencedObjects,
    Guid? FolderId = null,
    Guid? CollectionId = null,
    bool IncludeDeleted = false,
    bool SearchHistory = false
);
