using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Application.Abstractions;

public interface ICustomerMappingRepository
{
    Task<IReadOnlyList<CustomerMappingItem>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerMappingItem?> GetByCodeAsync(string customerCode, CancellationToken ct = default);
    Task UpsertAsync(CustomerMappingItem mapping, CancellationToken ct = default);
    Task DeleteAsync(Guid customerId, CancellationToken ct = default);
}
