using SqlFroega.Application.Services;
using Xunit;

namespace SqlFroega.Tests;

public sealed class SearchProfileVisibilityTests
{
    [Theory]
    [InlineData(null, false, "private")]
    [InlineData("private", false, "private")]
    [InlineData("global", true, "global")]
    [InlineData("GLOBAL", true, "global")]
    public void NormalizeForRequest_ValidCases(string? raw, bool isAdmin, string expected)
    {
        var value = SearchProfileVisibility.NormalizeForRequest(raw, isAdmin);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void NormalizeForRequest_GlobalWithoutAdmin_ReturnsNull()
    {
        var value = SearchProfileVisibility.NormalizeForRequest("global", false);
        Assert.Null(value);
    }

    [Theory]
    [InlineData(null, "private")]
    [InlineData("private", "private")]
    [InlineData("global", "global")]
    [InlineData("foo", "private")]
    public void NormalizeForStorage_Normalizes(string? raw, string expected)
    {
        var value = SearchProfileVisibility.NormalizeForStorage(raw);
        Assert.Equal(expected, value);
    }
}
