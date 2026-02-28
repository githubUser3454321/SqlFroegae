namespace SqlFroega.Application.Models;

public sealed record ScriptLockResult(
    bool Acquired,
    string? LockedBy
);
