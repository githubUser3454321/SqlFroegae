using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptDetail(
    Guid Id,
    string Name,
    string Key,
    string Content,
    string ScopeLabel,
    string? Module,
    Guid? CustomerId,
    string? CustomerName,
    string? Description,
    IReadOnlyList<string> Tags
);