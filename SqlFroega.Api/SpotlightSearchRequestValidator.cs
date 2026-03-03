namespace SqlFroega.Api;

internal static class SpotlightSearchRequestValidator
{
    private const int MaxTake = 500;

    public static Dictionary<string, string[]> Validate(SpotlightSearchRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Groups is null || request.Groups.Count == 0)
        {
            errors["groups"] = ["Mindestens eine Regelgruppe ist erforderlich."];
            return errors;
        }

        if (!string.IsNullOrWhiteSpace(request.GroupOperator)
            && !IsAndOperator(request.GroupOperator)
            && !IsOrOperator(request.GroupOperator))
        {
            errors["groupOperator"] = ["GroupOperator muss 'AND' oder 'OR' sein."];
        }

        if (request.Take <= 0 || request.Take > MaxTake)
        {
            errors["take"] = [$"Take muss zwischen 1 und {MaxTake} liegen."];
        }

        if (request.Skip < 0)
        {
            errors["skip"] = ["Skip darf nicht negativ sein."];
        }

        for (var i = 0; i < request.Groups.Count; i++)
        {
            var group = request.Groups[i];
            var prefix = $"groups[{i}]";

            if (group.Scope is < 0 or > 2)
            {
                errors[$"{prefix}.scope"] = ["Scope muss 0 (Global), 1 (Customer) oder 2 (Module) sein."];
            }

            if (!HasAnyFilter(group))
            {
                errors[$"{prefix}"] = ["Regelgruppe ist unvollständig: mindestens ein Suchkriterium ist erforderlich."];
            }
        }

        return errors;
    }

    private static bool HasAnyFilter(SpotlightRuleGroupRequest group)
    {
        return !string.IsNullOrWhiteSpace(group.Query)
               || group.Scope.HasValue
               || group.CustomerId.HasValue
               || !string.IsNullOrWhiteSpace(group.Module)
               || !string.IsNullOrWhiteSpace(group.MainModule)
               || !string.IsNullOrWhiteSpace(group.RelatedModule)
               || !string.IsNullOrWhiteSpace(group.RelatedModules)
               || !string.IsNullOrWhiteSpace(group.Tags)
               || !string.IsNullOrWhiteSpace(group.ReferencedObject)
               || !string.IsNullOrWhiteSpace(group.ReferencedObjects)
               || group.FolderId.HasValue
               || group.CollectionId.HasValue
               || group.IncludeDeleted
               || group.SearchHistory;
    }

    private static bool IsAndOperator(string value) => string.Equals(value, "AND", StringComparison.OrdinalIgnoreCase);
    private static bool IsOrOperator(string value) => string.Equals(value, "OR", StringComparison.OrdinalIgnoreCase);
}
