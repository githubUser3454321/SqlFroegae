using SqlFroega.Api.Auth;

namespace SqlFroega.Tests;

public class RefreshTokenStoreTests
{
    [Fact]
    public async Task Rotate_ReturnsNewToken_AndInvalidatesOldOne()
    {
        var store = new InMemoryRefreshTokenStore();
        var issued = await store.IssueAsync("alice", new[] { "scripts.read" }, "acme", TimeSpan.FromMinutes(30));

        var rotated = await store.RotateAsync(issued.Token, TimeSpan.FromMinutes(30));

        Assert.NotNull(rotated);
        Assert.NotEqual(issued.Token, rotated!.RefreshToken);
        Assert.Equal("acme", rotated.TenantContext);
        Assert.Null(await store.RotateAsync(issued.Token, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public async Task Revoke_MakesTokenInvalid()
    {
        var store = new InMemoryRefreshTokenStore();
        var issued = await store.IssueAsync("bob", new[] { "scripts.write" }, null, TimeSpan.FromMinutes(30));

        await store.RevokeAsync(issued.Token);

        Assert.Null(await store.RotateAsync(issued.Token, TimeSpan.FromMinutes(30)));
    }
}
