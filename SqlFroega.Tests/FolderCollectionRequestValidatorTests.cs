using SqlFroega.Api;
using Xunit;

namespace SqlFroega.Tests;

public sealed class FolderCollectionRequestValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void ValidateFolderUpsert_WithMissingName_ReturnsNameError(string name)
    {
        var request = new ScriptFolderUpsertRequest(null, name, null);

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
    public void ValidateFolderUpsert_WithValidPayload_ReturnsNoError()
    {
        var request = new ScriptFolderUpsertRequest(null, "Folder", Guid.NewGuid());

        var errors = FolderCollectionRequestValidator.ValidateFolderUpsert(request, Guid.NewGuid());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void ValidateCollectionUpsert_WithMissingName_ReturnsNameError(string name)
    {
        var request = new ScriptCollectionUpsertRequest(null, name, null, "private");

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
    public void ValidateCollectionUpsert_WithValidPayload_ReturnsNoError()
    {
        var request = new ScriptCollectionUpsertRequest(null, "Collection", Guid.NewGuid(), "global");

        var errors = FolderCollectionRequestValidator.ValidateCollectionUpsert(request, Guid.NewGuid());

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCollectionAssignment_WithPrimaryOutsideCollectionIds_ReturnsError()
    {
        var request = new ScriptCollectionAssignmentRequest([Guid.NewGuid(), Guid.NewGuid()], Guid.NewGuid());

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.True(errors.ContainsKey("primaryCollectionId"));
    }

    [Fact]
    public void ValidateCollectionAssignment_WithNullCollectionIdsAndPrimary_ReturnsError()
    {
        var request = new ScriptCollectionAssignmentRequest(null, Guid.NewGuid());

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.True(errors.ContainsKey("primaryCollectionId"));
    }

    [Fact]
    public void ValidateCollectionAssignment_WithNullCollectionIdsAndNoPrimary_ReturnsNoError()
    {
        var request = new ScriptCollectionAssignmentRequest(null, null);

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCollectionAssignment_WithPrimaryInsideCollectionIds_ReturnsNoError()
    {
        var first = Guid.NewGuid();
        var request = new ScriptCollectionAssignmentRequest([first, Guid.NewGuid(), first], first);

        var errors = FolderCollectionRequestValidator.ValidateCollectionAssignment(request);

        Assert.Empty(errors);
    }
}
