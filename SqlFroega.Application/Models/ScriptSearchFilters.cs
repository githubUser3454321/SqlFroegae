using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptSearchFilters(
    int? Scope,              // null = alle; sonst 0/1/2
    Guid? CustomerId,
    string? Module,
    IReadOnlyList<string>? Tags
);