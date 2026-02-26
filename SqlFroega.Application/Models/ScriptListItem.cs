#nullable enable
using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptListItem(
    Guid Id,
    string Name,
    string Key,
    string ScopeLabel,
    string? Module,
    string? CustomerName,
    string? Description,
    IReadOnlyList<string> Tags
);
