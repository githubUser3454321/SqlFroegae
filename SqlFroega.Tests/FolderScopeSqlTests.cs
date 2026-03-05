using System;
using SqlFroega.Application.Models;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using Xunit;

namespace SqlFroega.Tests;

public sealed class FolderScopeSqlTests
{
    [Fact]
    public void ShouldUseFolderScopeCte_WithoutFolder_ReturnsFalse()
    {
        var filters = CreateFilters(folderId: null, exact: false);
        Assert.False(FolderScopeSql.ShouldUseFolderScopeCte(filters));
    }

    [Fact]
    public void ShouldUseFolderScopeCte_WithFolderAndDefaultMode_ReturnsTrue()
    {
        var filters = CreateFilters(folderId: Guid.NewGuid(), exact: false);
        Assert.True(FolderScopeSql.ShouldUseFolderScopeCte(filters));
    }

    [Fact]
    public void ShouldUseFolderScopeCte_WithFolderAndExactMode_ReturnsFalse()
    {
        var filters = CreateFilters(folderId: Guid.NewGuid(), exact: true);
        Assert.False(FolderScopeSql.ShouldUseFolderScopeCte(filters));
    }

    [Fact]
    public void BuildFolderScopeCte_ContainsRootSelection()
    {
        var cte = FolderScopeSql.BuildFolderScopeCte();
        Assert.Contains("WHERE Id = @folderId", cte);
    }

    [Fact]
    public void BuildFolderScopeCte_ContainsRecursiveJoin()
    {
        var cte = FolderScopeSql.BuildFolderScopeCte();
        Assert.Contains("INNER JOIN folder_scope current_scope ON descendant.ParentId = current_scope.Id", cte);
        Assert.DoesNotContain("parent.ParentId = child.Id", cte);
        Assert.DoesNotContain("ON current_scope.ParentId = descendant.Id", cte);
    }

    [Fact]
    public void BuildFolderScopeCte_ContainsUnionAll()
    {
        var cte = FolderScopeSql.BuildFolderScopeCte();
        Assert.Contains("UNION ALL", cte);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("x")]
    public void BuildFolderPredicate_ExactMode_UsesEqualityForAlias(string alias)
    {
        var predicate = FolderScopeSql.BuildFolderPredicate(alias, folderMustMatchExactly: true);
        Assert.Equal($"{alias}.FolderId = @folderId", predicate);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("hs")]
    public void BuildFolderPredicate_SubtreeMode_UsesFolderScopeInClause(string alias)
    {
        var predicate = FolderScopeSql.BuildFolderPredicate(alias, folderMustMatchExactly: false);
        Assert.Equal($"{alias}.FolderId IS NOT NULL AND {alias}.FolderId IN (SELECT Id FROM folder_scope)", predicate);
    }


    [Fact]
    public void BuildFolderPredicate_SubtreeMode_ExplicitlyExcludesScriptsWithoutFolder()
    {
        var predicate = FolderScopeSql.BuildFolderPredicate("s", folderMustMatchExactly: false);
        Assert.Contains("s.FolderId IS NOT NULL", predicate);
    }

    [Fact]
    public void BuildFolderFilterDebugMessage_SubtreeMode_ExplainsChildFolderBehavior()
    {
        var folderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var message = FolderScopeSql.BuildFolderFilterDebugMessage(folderId, folderMustMatchExactly: false);

        Assert.Contains("Subtree-Match", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Child-Folder werden eingeschlossen", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFolderFilterDebugMessage_ExactMode_ExplainsChildFolderExclusion()
    {
        var folderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var message = FolderScopeSql.BuildFolderFilterDebugMessage(folderId, folderMustMatchExactly: true);

        Assert.Contains("Exakter Match", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Child-Folder werden ausgeschlossen", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFolderPredicate_SubtreeMode_DoesNotContainEqualityOperator()
    {
        var predicate = FolderScopeSql.BuildFolderPredicate("s", folderMustMatchExactly: false);
        Assert.DoesNotContain("= @folderId", predicate);
    }

    [Fact]
    public void BuildFolderPredicate_ExactMode_DoesNotContainFolderScopeInClause()
    {
        var predicate = FolderScopeSql.BuildFolderPredicate("s", folderMustMatchExactly: true);
        Assert.DoesNotContain("folder_scope", predicate);
    }

    [Fact]
    public void BuildPagingClause_WithFolderScopeCte_UsesSingleStatementOptionClause()
    {
        var clause = FolderScopeSql.BuildPagingClause(useFolderScopeCte: true);

        Assert.Contains("FETCH NEXT @take ROWS ONLY OPTION (MAXRECURSION 32767);", clause);
        Assert.DoesNotContain("ONLY; OPTION", clause, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPagingClause_WithoutFolderScopeCte_DoesNotContainOptionHint()
    {
        var clause = FolderScopeSql.BuildPagingClause(useFolderScopeCte: false);

        Assert.Equal("OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;", clause);
        Assert.DoesNotContain("MAXRECURSION", clause, StringComparison.Ordinal);
    }

    private static ScriptSearchFilters CreateFilters(Guid? folderId, bool exact)
        => new(
            Scope: null,
            CustomerId: null,
            Module: null,
            MainModule: null,
            RelatedModule: null,
            RelatedModules: null,
            Tags: null,
            ReferencedObject: null,
            ReferencedObjects: null,
            FolderId: folderId,
            CollectionId: null,
            IncludeDeleted: false,
            SearchHistory: false,
            FolderMustMatchExactly: exact);
}
