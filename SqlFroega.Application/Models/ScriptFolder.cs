namespace SqlFroega.Application.Models;

public sealed record ScriptFolder(
    Guid Id,
    string Name,
    Guid? ParentId,
    int SortOrder,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record ScriptFolderTreeNode(
    Guid Id,
    string Name,
    Guid? ParentId,
    int SortOrder,
    IReadOnlyList<ScriptFolderTreeNode> Children);

public sealed record ScriptFolderUpsert(
    Guid? Id,
    string Name,
    Guid? ParentId,
    int SortOrder);
