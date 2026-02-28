using System;

namespace SqlFroega.Application.Models;

public sealed class CustomerMappingItem
{
    public Guid CustomerId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string DatabaseUser { get; set; } = string.Empty;

    public string ObjectPrefix { get; set; } = string.Empty;
}
