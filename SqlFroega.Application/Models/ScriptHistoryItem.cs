using System;

namespace SqlFroega.Application.Models;

public sealed class ScriptHistoryItem
{
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
