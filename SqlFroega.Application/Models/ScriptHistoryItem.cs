using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptHistoryItem(
    DateTime ValidFrom,
    DateTime ValidTo,
    string ChangedBy
);