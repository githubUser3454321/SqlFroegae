using SqlFroega.Api.Auth;

namespace SqlFroega.Tests;

public class RefreshTokenStoreTests
{
    [Fact]
    public void Rotate_ReturnsNewToken_AndInvalidatesOldOne()
    {
        var store = new InMemoryRefreshTokenStore();
        var issued = store.Issue("alice", new[] { "scripts.read" }, TimeSpan.FromMinutes(30));

        var rotated = store.Rotate(issued.Token, TimeSpan.FromMinutes(30));

        Assert.NotNull(rotated);
        Assert.NotEqual(issued.Token, rotated!.RefreshToken);
        Assert.Null(store.Rotate(issued.Token, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Revoke_MakesTokenInvalid()
    {
        var store = new InMemoryRefreshTokenStore();
        var issued = store.Issue("bob", new[] { "scripts.write" }, TimeSpan.FromMinutes(30));

        store.Revoke(issued.Token);

        Assert.Null(store.Rotate(issued.Token, TimeSpan.FromMinutes(30)));
    }
}
