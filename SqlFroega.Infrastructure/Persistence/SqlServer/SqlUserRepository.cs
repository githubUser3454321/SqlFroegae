using Dapper;
using Microsoft.Data.SqlClient;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connFactory;
    private readonly IHostIdentityProvider _hostIdentityProvider;

    public SqlUserRepository(ISqlConnectionFactory connFactory, IHostIdentityProvider hostIdentityProvider)
    {
        _connFactory = connFactory;
        _hostIdentityProvider = hostIdentityProvider;
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync()
    {
        await using var conn = await _connFactory.OpenAsync();

        var users = await conn.QueryAsync<UserAccount>(@"
SELECT Id, Username, PasswordHash, IsAdmin, IsActive
FROM dbo.Users
ORDER BY Username ASC");

        return users.ToList();
    }

    public async Task<UserAccount?> FindActiveByCredentialsAsync(string username, string password)
    {
        var login = (username ?? string.Empty).Trim();
        var providedHash = HashPassword(password);

        await using var conn = await _connFactory.OpenAsync();

        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Users");
        if (count == 0)
        {
            if (string.Equals(login, "admin", StringComparison.OrdinalIgnoreCase)
                && string.Equals(password, "admin", StringComparison.Ordinal))
            {
                return new UserAccount
                {
                    Id = Guid.Empty,
                    Username = "admin",
                    PasswordHash = HashPassword("admin"),
                    IsAdmin = true,
                    IsActive = true
                };
            }

            return null;
        }

        return await conn.QuerySingleOrDefaultAsync<UserAccount>(@"
SELECT TOP (1) Id, Username, PasswordHash, IsAdmin, IsActive
FROM dbo.Users
WHERE IsActive = 1
  AND Username = @login
  AND PasswordHash = @providedHash", new { login, providedHash });
    }

    public async Task<UserAccount?> FindActiveByRememberedDeviceAsync(string username)
    {
        var login = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var windowsUserName = _hostIdentityProvider.GetWindowsUserName();
        var computerName = _hostIdentityProvider.GetComputerName();

        await using var conn = await _connFactory.OpenAsync();

        return await conn.QuerySingleOrDefaultAsync<UserAccount>(@"
SELECT TOP (1) u.Id, u.Username, u.PasswordHash, u.IsAdmin, u.IsActive
FROM dbo.Users u
INNER JOIN dbo.AuthenticatedDevices d ON d.UserId = u.Id
WHERE u.IsActive = 1
  AND u.Username = @login
  AND d.WindowsUserName = @windowsUserName
  AND d.ComputerName = @computerName", new { login, windowsUserName, computerName });
    }

    public async Task RememberDeviceAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var windowsUserName = _hostIdentityProvider.GetWindowsUserName();
        var computerName = _hostIdentityProvider.GetComputerName();

        await using var conn = await _connFactory.OpenAsync();

        await conn.ExecuteAsync(@"
MERGE dbo.AuthenticatedDevices AS target
USING (SELECT @UserId AS UserId, @WindowsUserName AS WindowsUserName, @ComputerName AS ComputerName) AS source
ON target.UserId = source.UserId
   AND target.WindowsUserName = source.WindowsUserName
   AND target.ComputerName = source.ComputerName
WHEN MATCHED THEN
    UPDATE SET LastSeenUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, UserId, WindowsUserName, ComputerName, LastSeenUtc)
    VALUES (NEWID(), source.UserId, source.WindowsUserName, source.ComputerName, SYSUTCDATETIME());",
            new { UserId = userId, WindowsUserName = windowsUserName, ComputerName = computerName });
    }

    public async Task<UserAccount> AddAsync(string username, string password, bool isAdmin)
    {
        var item = new UserAccount
        {
            Id = Guid.NewGuid(),
            Username = (username ?? string.Empty).Trim(),
            PasswordHash = HashPassword(password),
            IsAdmin = isAdmin,
            IsActive = true
        };

        await using var conn = await _connFactory.OpenAsync();

        try
        {
            await conn.ExecuteAsync(@"
INSERT INTO dbo.Users (Id, Username, PasswordHash, IsAdmin, IsActive)
VALUES (@Id, @Username, @PasswordHash, @IsAdmin, @IsActive)", item);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException("Ein Benutzer mit diesem Namen existiert bereits.", ex);
        }

        return item;
    }

    public async Task<bool> DeactivateAsync(Guid userId)
    {
        await using var conn = await _connFactory.OpenAsync();

        var changed = await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET IsActive = 0
WHERE Id = @userId", new { userId });

        return changed > 0;
    }

    public async Task<bool> ReactivateAsync(Guid userId)
    {
        await using var conn = await _connFactory.OpenAsync();

        var changed = await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET IsActive = 1
WHERE Id = @userId", new { userId });

        return changed > 0;
    }

    private static string HashPassword(string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
