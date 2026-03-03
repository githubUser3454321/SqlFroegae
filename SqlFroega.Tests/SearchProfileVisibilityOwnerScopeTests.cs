using SqlFroega.Application.Services;

namespace SqlFroega.Tests;

public sealed class SearchProfileVisibilityOwnerScopeTests
{
    [Fact]
    public void NormalizeForRequest_CanBeUsedForCollectionOwnerScope()
    {
        var adminGlobal = SearchProfileVisibility.NormalizeForRequest("global", isAdmin: true);
        var userGlobal = SearchProfileVisibility.NormalizeForRequest("global", isAdmin: false);
        var defaultPrivate = SearchProfileVisibility.NormalizeForRequest(null, isAdmin: false);

        Assert.Equal("global", adminGlobal);
        Assert.Null(userGlobal);
        Assert.Equal("private", defaultPrivate);
    }
}
