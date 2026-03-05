using System;
using System.Collections.Generic;
using System.IO;
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

    public WorkspaceOpenResult SaveScript(ScriptDetail detail, bool openReadonly)
    {
        var index = LoadIndex();
        var existingEntry = index.TryGetValue(detail.Id, out var found) ? found : null;

        var filePath = ResolveFilePath(detail, existingEntry);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        File.WriteAllText(filePath, detail.Content);
        ApplyReadonlyAttribute(filePath, openReadonly);

        var now = DateTimeOffset.UtcNow;
        index[detail.Id] = new WorkspaceIndexEntry(
            ScriptId: detail.Id,
            NumberId: detail.NumberId,
            Name: detail.Name,
            LocalPath: filePath,
            LastOpenedUtc: now,
            LastSyncedUtc: now,
            OpenMode: openReadonly ? "readonly" : "edit");

        SaveIndex(index);
        return new WorkspaceOpenResult(filePath, openReadonly ? "readonly" : "edit", now, index.Count);
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
    int IndexedScriptCount);

internal sealed record WorkspaceIndexEntry(
    Guid ScriptId,
    int NumberId,
    string Name,
    string LocalPath,
    DateTimeOffset LastOpenedUtc,
    DateTimeOffset LastSyncedUtc,
    string OpenMode);
