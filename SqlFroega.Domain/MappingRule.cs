using System;

namespace SqlFroega.Domain;

public sealed record MappingRule(
    Guid Id,
    Guid CustomerId,
    MappingRuleType RuleType,
    string From,
    string To
);