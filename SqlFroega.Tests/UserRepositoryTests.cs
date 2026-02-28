using SqlFroega.Infrastructure.Persistence;
using Xunit;

namespace SqlFroega.Tests;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task AddAndLogin_Work_ForActiveUser()
    {
        var repo = new InMemoryUserRepository();

        var user = await repo.AddAsync("testuser", "secret", isAdmin: false);
        var byCredentials = await repo.FindActiveByCredentialsAsync("testuser", "secret");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotNull(byCredentials);
        Assert.False(byCredentials!.IsAdmin);
        Assert.NotEqual("secret", byCredentials.PasswordHash);
    }


    [Fact]
    public async Task Hashing_UsesSameDigestAsSqlNVarChar()
    {
        var repo = new InMemoryUserRepository();

        var user = await repo.AddAsync("hash-check", "admin123", isAdmin: false);

        Assert.Equal("9D39DD891B174041B3488557421FAE0F8D551E1F612725717D820BDBB111530F", user.PasswordHash);
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword()
    {
        var repo = new InMemoryUserRepository();

        await repo.AddAsync("testuser", "secret", isAdmin: false);
        var result = await repo.FindActiveByCredentialsAsync("testuser", "wrong");

        Assert.Null(result);
    }

    [Fact]
    public async Task Deactivate_RemovesUserFromLogin()
    {
        var repo = new InMemoryUserRepository();

        var user = await repo.AddAsync("inactive", "secret", isAdmin: true);
        var deactivated = await repo.DeactivateAsync(user.Id);
        var result = await repo.FindActiveByCredentialsAsync("inactive", "secret");

        Assert.True(deactivated);
        Assert.Null(result);
    }

    [Fact]
    public async Task Reactivate_RestoresUserLogin()
    {
        var repo = new InMemoryUserRepository();

        var user = await repo.AddAsync("inactive", "secret", isAdmin: true);
        await repo.DeactivateAsync(user.Id);

        var reactivated = await repo.ReactivateAsync(user.Id);
        var result = await repo.FindActiveByCredentialsAsync("inactive", "secret");

        Assert.True(reactivated);
        Assert.NotNull(result);
    }
}
