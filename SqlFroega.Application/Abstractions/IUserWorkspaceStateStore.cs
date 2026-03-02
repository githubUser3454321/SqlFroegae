using SqlFroega.Application.Models;
using System;
using System.Threading.Tasks;

namespace SqlFroega.Application.Abstractions;

public interface IUserWorkspaceStateStore
{
    Task<UserWorkspaceState?> LoadAsync(Guid userId);
    Task SaveAsync(Guid userId, UserWorkspaceState state);
}

