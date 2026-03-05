using SqlFroega.Application.Models;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

public static class FolderScopeSql
{
    public static bool ShouldUseFolderScopeCte(ScriptSearchFilters filters)
        => filters.FolderId is not null && !filters.FolderMustMatchExactly;

    public static string BuildFolderFilterDebugMessage(Guid folderId, bool folderMustMatchExactly)
        => folderMustMatchExactly
            ? $"Folder-Filter aktiv: Exakter Match auf FolderId '{folderId}'. Child-Folder werden ausgeschlossen."
            : $"Folder-Filter aktiv: Subtree-Match auf FolderId '{folderId}'. Child-Folder werden eingeschlossen.";

    public static string BuildFolderScopeCte()
        => "folder_scope AS (\n"
           + "    SELECT Id\n"
           + "    FROM dbo.ScriptFolders\n"
           + "    WHERE Id = @folderId\n"
           + "    UNION ALL\n"
           + "    SELECT child.Id\n"
           + "    FROM dbo.ScriptFolders child\n"
           + "    INNER JOIN folder_scope parent ON child.ParentId = parent.Id\n"
           + ")";

    public static string BuildFolderPredicate(string alias, bool folderMustMatchExactly)
        => folderMustMatchExactly
            ? $"{alias}.FolderId = @folderId"
            : $"{alias}.FolderId IS NOT NULL AND {alias}.FolderId IN (SELECT Id FROM folder_scope)";
}
