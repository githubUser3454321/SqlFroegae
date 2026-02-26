using System;

namespace SqlFroega.Domain;

public sealed record Customer(
    Guid Id,
    string Name
);
