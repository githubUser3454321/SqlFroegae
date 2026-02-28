using SqlFroega.Application.Abstractions;

namespace SqlFroega.Infrastructure.Persistence;

public sealed class HostIdentityProvider : IHostIdentityProvider
{
    public string GetWindowsUserName()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("USERNAME"),
            Environment.UserName,
            Environment.GetEnvironmentVariable("USER"),
            Environment.GetEnvironmentVariable("LOGNAME")
        };

        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "offline-user";
    }

    public string GetComputerName()
    {
        var candidates = new[]
        {
            Environment.MachineName,
            Environment.GetEnvironmentVariable("COMPUTERNAME"),
            Environment.GetEnvironmentVariable("HOSTNAME")
        };

        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "offline-machine";
    }
}
