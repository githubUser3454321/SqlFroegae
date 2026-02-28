using System;

namespace SqlFroega.Application.Models;

public sealed record CustomerMappingItem(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string SchemaName,
    string ObjectPrefix,
    string DatabaseUser
);
