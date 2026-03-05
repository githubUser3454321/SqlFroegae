using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlFroega.SsmsExtension;

internal sealed class WorkspaceManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _workspaceRoot;
    private readonly string _indexPath;

    public WorkspaceManager()
    {
        _workspaceRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlFroega", "SsmsWorkspace");
        _indexPath = Path.Combine(_workspaceRoot, "workspace-index.json");
        Directory.CreateDirectory(_workspaceRoot);
    }

    public string SaveScript(ScriptDetail detail, bool openReadonly)
    {
        var safeName = Regex.Replace(detail.Name, "[^a-zA-Z0-9_-]", "_");
        var fileName = $"{safeName}_{detail.Id:N}.sql";
        var filePath = Path.Combine(_workspaceRoot, fileName);

        var content = openReadonly
            ? $"-- Opened as readonly snapshot at {DateTimeOffset.UtcNow:u}{Environment.NewLine}{detail.Content}"
            : detail.Content;

        File.WriteAllText(filePath, content);

        var index = LoadIndex();
        index[detail.Id] = new WorkspaceIndexEntry(filePath, DateTimeOffset.UtcNow);
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index, JsonOptions));

        return filePath;
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

    private sealed record WorkspaceIndexEntry(string Path, DateTimeOffset LastOpenedUtc);
}
