using SqlFroega.Domain;
using System;

namespace SqlFroega.Application.Models;

public sealed record ScriptReferenceItem(
    Guid ScriptId,
    string ObjectName,
    DbObjectType ObjectType
);
