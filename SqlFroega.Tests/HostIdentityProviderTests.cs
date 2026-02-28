using SqlFroega.Infrastructure.Persistence;
using Xunit;

namespace SqlFroega.Tests;

public sealed class HostIdentityProviderTests
{
    [Fact]
    public void GetWindowsUserName_And_GetComputerName_ReturnValues()
    {
        var provider = new HostIdentityProvider();

        var windowsUserName = provider.GetWindowsUserName();
        var computerName = provider.GetComputerName();

        Assert.False(string.IsNullOrWhiteSpace(windowsUserName));
        Assert.False(string.IsNullOrWhiteSpace(computerName));
    }
}
