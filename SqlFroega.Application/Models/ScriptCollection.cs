namespace SqlFroega.Application.Models;

public sealed record ScriptCollection
{
    public ScriptCollection()
    {
        Name = string.Empty;
        OwnerScope = string.Empty;
    }

    public ScriptCollection(
        Guid id,
        string name,
        Guid? parentId,
        string ownerScope,
        int sortOrder,
        DateTime createdUtc,
        DateTime updatedUtc)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        OwnerScope = ownerScope;
        SortOrder = sortOrder;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string OwnerScope { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed record ScriptCollectionUpsert(
    Guid? Id,
    string Name,
    Guid? ParentId,
    string OwnerScope,
    int SortOrder);
