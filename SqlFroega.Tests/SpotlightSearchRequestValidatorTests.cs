using SqlFroega.Api;
using Xunit;

namespace SqlFroega.Tests;

public sealed class SpotlightSearchRequestValidatorTests
{
    [Fact]
    public void Validate_WithEmptyGroups_ReturnsGroupsError()
    {
        var request = new SpotlightSearchRequest("OR", Array.Empty<SpotlightRuleGroupRequest>());

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups"));
    }

    [Fact]
    public void Validate_WithInvalidOperator_ReturnsGroupOperatorError()
    {
        var request = new SpotlightSearchRequest(
            "XOR",
            [new SpotlightRuleGroupRequest(Query: "abc", Scope: null, CustomerId: null, Module: null, MainModule: null, RelatedModule: null, RelatedModules: null, Tags: null, ReferencedObject: null, ReferencedObjects: null, FolderId: null, CollectionId: null)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groupOperator"));
    }

    [Fact]
    public void Validate_WithTakeOutOfRange_ReturnsTakeError()
    {
        var request = new SpotlightSearchRequest(
            "OR",
            [new SpotlightRuleGroupRequest(Query: "abc", Scope: null, CustomerId: null, Module: null, MainModule: null, RelatedModule: null, RelatedModules: null, Tags: null, ReferencedObject: null, ReferencedObjects: null, FolderId: null, CollectionId: null)],
            Take: 900,
            Skip: 0);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("take"));
    }

    [Fact]
    public void Validate_WithEmptyRuleGroup_ReturnsGroupCompletionError()
    {
        var request = new SpotlightSearchRequest(
            "AND",
            [new SpotlightRuleGroupRequest(Query: null, Scope: null, CustomerId: null, Module: null, MainModule: null, RelatedModule: null, RelatedModules: null, Tags: null, ReferencedObject: null, ReferencedObjects: null, FolderId: null, CollectionId: null)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups[0]"));
    }

    [Fact]
    public void Validate_WithInvalidScope_ReturnsScopeError()
    {
        var request = new SpotlightSearchRequest(
            "AND",
            [new SpotlightRuleGroupRequest(Query: "with-scope", Scope: 99, CustomerId: null, Module: null, MainModule: null, RelatedModule: null, RelatedModules: null, Tags: null, ReferencedObject: null, ReferencedObjects: null, FolderId: null, CollectionId: null)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups[0].scope"));
    }

    [Fact]
    public void Validate_WithValidMinimalRequest_ReturnsNoErrors()
    {
        var request = new SpotlightSearchRequest(
            "OR",
            [new SpotlightRuleGroupRequest(Query: "proc customer", Scope: null, CustomerId: null, Module: null, MainModule: null, RelatedModule: null, RelatedModules: null, Tags: null, ReferencedObject: null, ReferencedObjects: null, FolderId: null, CollectionId: null)],
            Take: 200,
            Skip: 0);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.Empty(errors);
    }
}
