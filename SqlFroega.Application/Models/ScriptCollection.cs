namespace SqlFroega.Application.Models;

public sealed record ScriptCollection(
    Guid Id,
    string Name,
    Guid? ParentId,
    string OwnerScope,
    int SortOrder,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record ScriptCollectionUpsert(
    Guid? Id,
    string Name,
    Guid? ParentId,
    string OwnerScope,
    int SortOrder);
