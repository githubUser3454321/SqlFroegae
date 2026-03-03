using SqlFroega.Application.Models;

namespace SqlFroega.Application.Services;

public static class SpotlightSearchCombiner
{
    public static IReadOnlyList<ScriptListItem> Combine(
        IReadOnlyList<IReadOnlyList<ScriptListItem>> groups,
        bool combineWithAnd,
        int skip,
        int take)
    {
        Dictionary<Guid, ScriptListItem>? accumulator = null;

        foreach (var group in groups)
        {
            var current = group.ToDictionary(x => x.Id, x => x);

            if (accumulator is null)
            {
                accumulator = current;
                continue;
            }

            if (combineWithAnd)
            {
                accumulator = accumulator
                    .Where(kvp => current.ContainsKey(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            else
            {
                foreach (var kvp in current)
                {
                    accumulator[kvp.Key] = kvp.Value;
                }
            }
        }

        var normalizedSkip = Math.Max(skip, 0);
        var normalizedTake = take <= 0 ? 200 : Math.Min(take, 500);

        return (accumulator ?? new Dictionary<Guid, ScriptListItem>())
            .Values
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToList();
    }
}
