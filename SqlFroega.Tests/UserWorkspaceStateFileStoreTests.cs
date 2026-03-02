using SqlFroega.Application.Models;
using SqlFroega.Infrastructure.Persistence;
using Xunit;

namespace SqlFroega.Tests;

public sealed class UserWorkspaceStateFileStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsStatePerUser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"workspace-state-{Guid.NewGuid():N}.json");
        var store = new UserWorkspaceStateFileStore(path);

        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var firstState = CreateState(queryText: "foo", currentPage: 3, detailTarget: WorkspaceDetailTarget.ScriptItem, detailScriptId: Guid.NewGuid());
        var secondState = CreateState(queryText: "bar", currentPage: 1, detailTarget: WorkspaceDetailTarget.ModuleAdmin, detailScriptId: null);

        await store.SaveAsync(firstUser, firstState);
        await store.SaveAsync(secondUser, secondState);

        var loadedFirst = await store.LoadAsync(firstUser);
        var loadedSecond = await store.LoadAsync(secondUser);

        Assert.Equal(firstState, loadedFirst);
        Assert.Equal(secondState, loadedSecond);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupted_ReturnsNullInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"workspace-state-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ definitely-not-json");
        var store = new UserWorkspaceStateFileStore(path);

        var loaded = await store.LoadAsync(Guid.NewGuid());

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveAsync_WithEmptyUserId_DoesNotCreateFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"workspace-state-{Guid.NewGuid():N}.json");
        var store = new UserWorkspaceStateFileStore(path);

        await store.SaveAsync(Guid.Empty, CreateState("ignored", 1, WorkspaceDetailTarget.Placeholder, null));

        Assert.False(File.Exists(path));
    }

    private static UserWorkspaceState CreateState(string queryText, int currentPage, WorkspaceDetailTarget detailTarget, Guid? detailScriptId)
        => new(
            QueryText: queryText,
            ScopeFilterIndex: 2,
            MainModuleFilterText: "module-main",
            RelatedModuleFilterText: "module-related",
            CustomerCodeFilterText: "cust",
            TagsFilterText: "tag-a, tag-b",
            ObjectFilterText: "obj",
            ModuleCatalogSearchText: "mod",
            TagCatalogSearchText: "tag",
            IncludeDeleted: true,
            SearchInHistory: true,
            IsAdvancedSearchExpanded: true,
            CurrentPage: currentPage,
            HadExecutedSearch: true,
            DetailTarget: detailTarget,
            DetailScriptId: detailScriptId);
}
