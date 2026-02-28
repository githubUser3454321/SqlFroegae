using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System.Security.Cryptography;
using System.Text;

namespace SqlFroega.Infrastructure.Persistence;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<UserAccount> _users =
    [
        new UserAccount
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = HashPassword("admin"),
            IsAdmin = true,
            IsActive = true
        }
    ];

    private readonly object _sync = new();

    public Task<IReadOnlyList<UserAccount>> GetAllAsync()
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<UserAccount>>(_users
                .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .Select(CopyUser)
                .ToList());
        }
    }

    public Task<UserAccount?> FindActiveByCredentialsAsync(string username, string password)
    {
        var login = username.Trim();
        var providedHash = HashPassword(password);

        lock (_sync)
        {
            var user = _users.FirstOrDefault(x => x.IsActive
                && string.Equals(x.Username, login, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.PasswordHash, providedHash, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(user is null ? null : CopyUser(user));
        }
    }

    public Task<UserAccount> AddAsync(string username, string password, bool isAdmin)
    {
        var trimmedUsername = username.Trim();

        var item = new UserAccount
        {
            Id = Guid.NewGuid(),
            Username = trimmedUsername,
            PasswordHash = HashPassword(password),
            IsAdmin = isAdmin,
            IsActive = true
        };

        lock (_sync)
        {
            if (_users.Any(x => string.Equals(x.Username, item.Username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Ein Benutzer mit diesem Namen existiert bereits.");
            }

            _users.Add(item);
        }

        return Task.FromResult(CopyUser(item));
    }

    public Task<bool> DeactivateAsync(Guid userId)
    {
        lock (_sync)
        {
            var user = _users.FirstOrDefault(x => x.Id == userId);
            if (user is null)
            {
                return Task.FromResult(false);
            }

            user.IsActive = false;
            return Task.FromResult(true);
        }
    }

    private static UserAccount CopyUser(UserAccount source)
    {
        return new UserAccount
        {
            Id = source.Id,
            Username = source.Username,
            PasswordHash = source.PasswordHash,
            IsAdmin = source.IsAdmin,
            IsActive = source.IsActive
        };
    }

    private static string HashPassword(string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
