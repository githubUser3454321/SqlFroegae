
using System;

namespace SqlFroega.Domain;

public sealed record DbObjectRef(
    string Name,
    DbObjectType Type
);
