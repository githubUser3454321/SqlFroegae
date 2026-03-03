using SqlFroega.Api;
using Xunit;

namespace SqlFroega.Tests;

public sealed class SpotlightSearchRequestValidatorTests
{
    [Fact]
    public void Validate_WithEmptyGroups_ReturnsOnlyGroupsError()
    {
        var request = new SpotlightSearchRequest("OR", Array.Empty<SpotlightRuleGroupRequest>());

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups"));
        Assert.Single(errors);
    }

    [Theory]
    [InlineData("XOR")]
    [InlineData("NOT")]
    [InlineData("and-or")]
    public void Validate_WithInvalidOperator_ReturnsGroupOperatorError(string op)
    {
        var request = new SpotlightSearchRequest(op, [Group(query: "abc")]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groupOperator"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AND")]
    [InlineData("and")]
    [InlineData("OR")]
    [InlineData("or")]
    public void Validate_WithSupportedOperatorValues_DoesNotReturnGroupOperatorError(string? op)
    {
        var request = new SpotlightSearchRequest(op, [Group(query: "abc")]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.DoesNotContain("groupOperator", errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Validate_WithTakeOutOfRange_ReturnsTakeError(int take)
    {
        var request = new SpotlightSearchRequest("OR", [Group(query: "abc")], Take: take, Skip: 0);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("take"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    [InlineData(500)]
    public void Validate_WithTakeInRange_DoesNotReturnTakeError(int take)
    {
        var request = new SpotlightSearchRequest("OR", [Group(query: "abc")], Take: take, Skip: 0);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.DoesNotContain("take", errors.Keys);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-99)]
    public void Validate_WithNegativeSkip_ReturnsSkipError(int skip)
    {
        var request = new SpotlightSearchRequest("OR", [Group(query: "abc")], Take: 200, Skip: skip);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("skip"));
    }

    [Fact]
    public void Validate_WithEmptyRuleGroup_ReturnsGroupCompletionError()
    {
        var request = new SpotlightSearchRequest("AND", [Group()]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups[0]"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void Validate_WithInvalidScope_ReturnsScopeError(int scope)
    {
        var request = new SpotlightSearchRequest("AND", [Group(query: "with-scope", scope: scope)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.True(errors.ContainsKey("groups[0].scope"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_WithValidScope_DoesNotReturnScopeError(int scope)
    {
        var request = new SpotlightSearchRequest("AND", [Group(query: "with-scope", scope: scope)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.DoesNotContain("groups[0].scope", errors.Keys);
    }

    [Fact]
    public void Validate_WithSecondGroupInvalid_ReturnsIndexedErrorForSecondGroup()
    {
        var request = new SpotlightSearchRequest("OR", [Group(query: "ok"), Group()]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.DoesNotContain("groups[0]", errors.Keys);
        Assert.True(errors.ContainsKey("groups[1]"));
    }

    [Fact]
    public void Validate_WithIncludeDeletedOnly_IsAcceptedAsValidFilterGroup()
    {
        var request = new SpotlightSearchRequest("OR", [Group(includeDeleted: true)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithSearchHistoryOnly_IsAcceptedAsValidFilterGroup()
    {
        var request = new SpotlightSearchRequest("OR", [Group(searchHistory: true)]);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithValidMinimalRequest_ReturnsNoErrors()
    {
        var request = new SpotlightSearchRequest("OR", [Group(query: "proc customer")], Take: 200, Skip: 0);

        var errors = SpotlightSearchRequestValidator.Validate(request);

        Assert.Empty(errors);
    }

    private static SpotlightRuleGroupRequest Group(
        string? query = null,
        int? scope = null,
        bool includeDeleted = false,
        bool searchHistory = false)
    {
        return new SpotlightRuleGroupRequest(
            Query: query,
            Scope: scope,
            CustomerId: null,
            Module: null,
            MainModule: null,
            RelatedModule: null,
            RelatedModules: null,
            Tags: null,
            ReferencedObject: null,
            ReferencedObjects: null,
            FolderId: null,
            CollectionId: null,
            IncludeDeleted: includeDeleted,
            SearchHistory: searchHistory);
    }
}
