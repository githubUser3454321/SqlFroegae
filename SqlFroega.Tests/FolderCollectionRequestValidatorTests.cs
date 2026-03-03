using SqlFroega.Api;
using Xunit;

namespace SqlFroega.Tests;

public sealed class FolderCollectionRequestValidatorTests
{
    [Fact]
    public void ValidateFolderUpsert_WithMissingName_ReturnsNameError()
    {
        var request = new ScriptFolderUpsertRequest(null, "", null);

        var errors = FolderCollectionRequestValidator.ValidateFolderUpsert(request);

        Assert.True(errors.ContainsKey("name"));
    }

    [Fact]
    public void ValidateFolderUpsert_WithParentEqualRouteId_ReturnsParentError()
    {
        var id = Guid.NewGuid();
        var request = new ScriptFolderUpsertRequest(null, "Folder", id);

        var errors = FolderCollectionRequestValidator.ValidateFolderUpsert(request, id);

        Assert.True(errors.ContainsKey("parentId"));
    }

    [Fact]
    public void ValidateCollectionUpsert_WithMissingName_ReturnsNameError()
    {
        var request = new ScriptCollectionUpsertRequest(null, "   ", null, "private");

        var errors = FolderCollectionRequestValidator.ValidateCollectionUpsert(request);

        Assert.True(errors.ContainsKey("name"));
    }

    [Fact]
    public void ValidateCollectionUpsert_WithParentEqualRouteId_ReturnsParentError()
    {
        var id = Guid.NewGuid();
        var request = new ScriptCollectionUpsertRequest(null, "Collection", id, "private");

        var errors = FolderCollectionRequestValidator.ValidateCollectionUpsert(request, id);

        Assert.True(errors.ContainsKey("parentId"));
    }

    [Fact]
    public void ValidateCollectionAssignment_WithPrimaryOutsideCollectionIds_ReturnsError()
    {
        var request = new ScriptCollectionAssignmentRequest(
            [Guid.NewGuid(), Guid.NewGuid()],
            Guid.NewGuid());

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.True(errors.ContainsKey("primaryCollectionId"));
    }

    [Fact]
    public void ValidateCollectionAssignment_WithPrimaryInsideCollectionIds_ReturnsNoError()
    {
        var first = Guid.NewGuid();
        var request = new ScriptCollectionAssignmentRequest(
            [first, Guid.NewGuid(), first],
            first);

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.Empty(errors);
    }
}
