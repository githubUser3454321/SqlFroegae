using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlFroega.SsmsExtension;

internal sealed class WorkspaceManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _workspaceRoot;
    private readonly string _indexPath;

    public WorkspaceManager(SsmsExtensionSettings settings)
    {
        _workspaceRoot = settings.WorkspaceRoot;
        _indexPath = Path.Combine(_workspaceRoot, "workspace-index.json");
        Directory.CreateDirectory(_workspaceRoot);
    }

    public WorkspaceOpenResult SaveScript(ScriptDetail detail, bool openReadonly, string? versionToken)
    {
        var index = LoadIndex();
        var existingEntry = index.TryGetValue(detail.Id, out var found) ? found : null;

        var filePath = ResolveFilePath(detail, existingEntry);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var localHashBeforeOpen = File.Exists(filePath) ? ComputeContentHash(File.ReadAllText(filePath)) : null;
        var hasUnsyncedLocalChanges = HasUnsyncedLocalChanges(existingEntry, localHashBeforeOpen);
        var serverContentHash = ComputeContentHash(detail.Content);

        if (!hasUnsyncedLocalChanges)
        {
            File.WriteAllText(filePath, detail.Content);
            ApplyReadonlyAttribute(filePath, openReadonly);
            localHashBeforeOpen = serverContentHash;
        }

        var now = DateTimeOffset.UtcNow;
        index[detail.Id] = new WorkspaceIndexEntry(
            ScriptId: detail.Id,
            NumberId: detail.NumberId,
            Name: detail.Name,
            LocalPath: filePath,
            LastOpenedUtc: now,
            LastSyncedUtc: hasUnsyncedLocalChanges ? existingEntry?.LastSyncedUtc ?? now : now,
            OpenMode: openReadonly ? "readonly" : "edit",
            VersionToken: string.IsNullOrWhiteSpace(versionToken) ? existingEntry?.VersionToken : versionToken,
            LastSyncedContentHash: serverContentHash,
            LastKnownLocalContentHash: localHashBeforeOpen ?? serverContentHash,
            HasUnsyncedLocalChanges: hasUnsyncedLocalChanges);

        SaveIndex(index);
        return new WorkspaceOpenResult(
            filePath,
            openReadonly ? "readonly" : "edit",
            now,
            index.Count,
            hasUnsyncedLocalChanges,
            string.IsNullOrWhiteSpace(versionToken) ? existingEntry?.VersionToken : versionToken);
    }

    private static bool HasUnsyncedLocalChanges(WorkspaceIndexEntry? existingEntry, string? localHash)
    {
        if (existingEntry is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existingEntry.LastKnownLocalContentHash) || string.IsNullOrWhiteSpace(localHash))
        {
            return false;
        }

        return !string.Equals(existingEntry.LastKnownLocalContentHash, localHash, StringComparison.Ordinal);
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private string ResolveFilePath(ScriptDetail detail, WorkspaceIndexEntry? existingEntry)
    {
        if (existingEntry is not null && !string.IsNullOrWhiteSpace(existingEntry.LocalPath))
        {
            var existing = existingEntry.LocalPath;
            if (File.Exists(existing) || !Path.IsPathRooted(existing))
            {
                return Path.IsPathRooted(existing) ? existing : Path.GetFullPath(Path.Combine(_workspaceRoot, existing));
            }
        }

        var safeName = Regex.Replace(detail.Name, "[^a-zA-Z0-9_-]", "_");
        var fileName = $"{safeName}_{detail.Id:N}.sql";
        return Path.Combine(_workspaceRoot, fileName);
    }

    private static void ApplyReadonlyAttribute(string filePath, bool openReadonly)
    {
        var attributes = File.GetAttributes(filePath);

        if (openReadonly)
        {
            if ((attributes & FileAttributes.ReadOnly) == 0)
            {
                File.SetAttributes(filePath, attributes | FileAttributes.ReadOnly);
            }

            return;
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private Dictionary<Guid, WorkspaceIndexEntry> LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return new Dictionary<Guid, WorkspaceIndexEntry>();
        }

        var json = File.ReadAllText(_indexPath);
        var parsed = JsonSerializer.Deserialize<Dictionary<Guid, WorkspaceIndexEntry>>(json, JsonOptions);
        return parsed ?? new Dictionary<Guid, WorkspaceIndexEntry>();
    }

    private void SaveIndex(Dictionary<Guid, WorkspaceIndexEntry> index)
    {
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index, JsonOptions));
    }
}

internal sealed record WorkspaceOpenResult(
    string LocalPath,
    string OpenMode,
    DateTimeOffset LastOpenedUtc,
    int IndexedScriptCount,
    bool HasUnsyncedLocalChanges,
    string? VersionToken);

internal sealed record WorkspaceIndexEntry(
    Guid ScriptId,
    int NumberId,
    string Name,
    string LocalPath,
    DateTimeOffset LastOpenedUtc,
    DateTimeOffset LastSyncedUtc,
    string OpenMode,
    string? VersionToken,
    string LastSyncedContentHash,
    string LastKnownLocalContentHash,
    bool HasUnsyncedLocalChanges);
