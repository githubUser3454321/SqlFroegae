#nullable enable
using System.Collections.Generic;
using System;

namespace SqlFroega.Application.Models;

public sealed class ScriptListItem
{
    public ScriptListItem(Guid id, string name, string key, string scopeLabel, string module, string customerName, string description, IReadOnlyList<string> readOnlyList)
    {
        Id = id;
        Name = name;
        Key = key;
        ScopeLabel = scopeLabel;
        Module = module;
        CustomerName = customerName;
        Description = description;
        Tags = readOnlyList;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public string ScopeLabel { get; set; } = "";
    public string? Module { get; set; }
    public string? CustomerName { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}
