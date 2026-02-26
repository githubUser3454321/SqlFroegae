using SqlFroega.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Application.Abstractions;

public interface IMappingRepository
{
    Task<IReadOnlyList<MappingRule>> GetRulesForCustomerAsync(Guid customerId, CancellationToken ct = default);

    Task<Guid> UpsertRuleAsync(MappingRule rule, CancellationToken ct = default);

    Task DeleteRuleAsync(Guid ruleId, CancellationToken ct = default);
}