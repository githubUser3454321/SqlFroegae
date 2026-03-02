using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Persistence;

public sealed class UserWorkspaceStateFileStore : IUserWorkspaceStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UserWorkspaceStateFileStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SqlFroega",
            "workspace-state.json");
    }

    public async Task<UserWorkspaceState?> LoadAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return null;

        await _gate.WaitAsync();
        try
        {
            var states = await ReadAllAsync();
            return states.TryGetValue(userId.ToString("D"), out var state) ? state : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(Guid userId, UserWorkspaceState state)
    {
        if (userId == Guid.Empty)
            return;

        await _gate.WaitAsync();
        try
        {
            var states = await ReadAllAsync();
            states[userId.ToString("D")] = state;

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, states, JsonOptions);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, UserWorkspaceState>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, UserWorkspaceState>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var states = await JsonSerializer.DeserializeAsync<Dictionary<string, UserWorkspaceState>>(stream, JsonOptions);
            return states ?? new Dictionary<string, UserWorkspaceState>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, UserWorkspaceState>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

