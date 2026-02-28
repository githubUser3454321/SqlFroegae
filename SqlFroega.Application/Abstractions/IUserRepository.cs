using SqlFroega.Application.Models;

namespace SqlFroega.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync();
    Task<UserAccount?> FindActiveByCredentialsAsync(string username, string password);
    Task<UserAccount> AddAsync(string username, string password, bool isAdmin);
    Task<bool> DeactivateAsync(Guid userId);
}
