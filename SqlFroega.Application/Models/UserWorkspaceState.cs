using System;

namespace SqlFroega.Application.Models;

public sealed record UserWorkspaceState(
    string QueryText,
    int ScopeFilterIndex,
    string MainModuleFilterText,
    string RelatedModuleFilterText,
    string CustomerCodeFilterText,
    string TagsFilterText,
    string ObjectFilterText,
    string ModuleCatalogSearchText,
    string TagCatalogSearchText,
    bool IncludeDeleted,
    bool SearchInHistory,
    bool IsAdvancedSearchExpanded,
    int CurrentPage,
    bool HadExecutedSearch,
    WorkspaceDetailTarget DetailTarget,
    Guid? DetailScriptId
);

public enum WorkspaceDetailTarget
{
    Placeholder = 0,
    ScriptItem = 1,
    CustomerMappingAdmin = 2,
    ModuleAdmin = 3,
    UserManagementAdmin = 4
}

