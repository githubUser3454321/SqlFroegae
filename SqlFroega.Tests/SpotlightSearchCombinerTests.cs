using SqlFroega.Application.Models;
using SqlFroega.Application.Services;

namespace SqlFroega.Tests;

public sealed class SpotlightSearchCombinerTests
{
    [Fact]
    public void Combine_WithOr_ReturnsUnionDistinctSorted()
    {
        var a = Script("A", 1);
        var b = Script("B", 2);
        var c = Script("C", 3);

        var result = SpotlightSearchCombiner.Combine(
            new List<IReadOnlyList<ScriptListItem>>
            {
                new[] { b, a },
                new[] { c, b }
            },
            combineWithAnd: false,
            skip: 0,
            take: 50);

        Assert.Equal(new[] { "A", "B", "C" }, result.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void Combine_WithAnd_ReturnsIntersection()
    {
        var a = Script("A", 1);
        var b = Script("B", 2);
        var c = Script("C", 3);

        var result = SpotlightSearchCombiner.Combine(
            new List<IReadOnlyList<ScriptListItem>>
            {
                new[] { a, b },
                new[] { b, c }
            },
            combineWithAnd: true,
            skip: 0,
            take: 50);

        var only = Assert.Single(result);
        Assert.Equal("B", only.Name);
    }

    [Theory]
    [InlineData(null, false, "private")]
    [InlineData("private", false, "private")]
    [InlineData("global", true, "global")]
    public void NormalizeForRequest_ReturnsExpected(string? raw, bool isAdmin, string expected)
    {
        var visibility = SearchProfileVisibility.NormalizeForRequest(raw, isAdmin);
        Assert.Equal(expected, visibility);
    }

    [Fact]
    public void NormalizeForRequest_GlobalWithoutAdmin_ReturnsNull()
    {
        var visibility = SearchProfileVisibility.NormalizeForRequest("global", isAdmin: false);
        Assert.Null(visibility);
    }


    [Fact]
    public void Combine_NormalizesPaging_WhenSkipOrTakeInvalid()
    {
        var a = Script("A", 1);
        var b = Script("B", 2);

        var result = SpotlightSearchCombiner.Combine(
            new List<IReadOnlyList<ScriptListItem>> { new[] { a, b } },
            combineWithAnd: false,
            skip: -5,
            take: 0);

        Assert.Equal(2, result.Count);
    }

    private static ScriptListItem Script(string name, int numberId)
    {
        return new ScriptListItem(
            Guid.NewGuid(),
            name,
            numberId,
            "Global",
            null,
            Array.Empty<string>(),
            null,
            null,
            Array.Empty<string>());
    }
}
