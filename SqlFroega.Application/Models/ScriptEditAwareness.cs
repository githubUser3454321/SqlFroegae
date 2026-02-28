using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptEditAwareness(
    DateTime? LastViewedAt,
    DateTime? LastUpdatedAt,
    string? LastUpdatedBy
);
