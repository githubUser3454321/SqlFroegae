#nullable enable
using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed class ScriptListItem
{
    public ScriptListItem(
        Guid id,
        string name,
        string key,
        string scopeLabel,
        string? mainModule,
        IReadOnlyList<string> relatedModules,
        string? customerName,
        string? description,
        IReadOnlyList<string> readOnlyList,
        bool isDeleted = false)
    {
        Id = id;
        Name = name;
        Key = key;
        ScopeLabel = scopeLabel;
        MainModule = mainModule;
        RelatedModules = relatedModules;
        CustomerName = customerName;
        Description = description;
        Tags = readOnlyList;
        IsDeleted = isDeleted;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public string ScopeLabel { get; set; } = "";
    public string? MainModule { get; set; }
    public IReadOnlyList<string> RelatedModules { get; set; } = Array.Empty<string>();
    public string? CustomerName { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public bool IsDeleted { get; set; }
    public string DeletedLabel => IsDeleted ? "Deleted (history only)" : string.Empty;
}
