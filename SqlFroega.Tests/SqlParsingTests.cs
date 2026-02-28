using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Domain;
using SqlFroega.Infrastructure.Parsing;
using Xunit;

namespace SqlFroega.Tests;

public sealed class SqlParsingTests
{
    [Fact]
    public async Task NormalizeForStorage_Throws_OnParserError()
    {
        var service = new SqlCustomerRenderService(new FakeMappingRepository(new List<CustomerMappingItem>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync("SELECT FROM"));
    }

    [Theory]
    [InlineData("SELECT * FROM [abc].[abc_table]", "SELECT * FROM [om].[om_table];")]
    [InlineData("SELECT * FROM abc.abc_table", "SELECT * FROM om.om_table;")]
    [InlineData("SELECT *\nFROM [AbC] . [AbC_Table]", "SELECT * FROM [om].[om_Table];")]
    public async Task NormalizeForStorage_Rewrites_AstBased(string sql, string expected)
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "abc", ObjectPrefix = "abc_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));
        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Extractor_FindsReferences_InJoinAliasAndCte()
    {
        var sql = @"
WITH cte AS (
    SELECT t.Id FROM om.om_table t
)
SELECT cte.Id
FROM cte
JOIN om.om_other o ON o.Id = cte.Id
JOIN om.om_third t3 ON t3.Id = o.Id;";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_table" && r.Type == DbObjectType.Table);
        Assert.Contains(refs, r => r.Name == "om.om_other" && r.Type == DbObjectType.Table);
        Assert.Contains(refs, r => r.Name == "om.om_third" && r.Type == DbObjectType.Table);
        Assert.DoesNotContain(refs, r => r.Name.Equals("dbo.cte", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extractor_DoesNotAnalyze_DynamicSqlContent()
    {
        var sql = "DECLARE @sql nvarchar(max) = N'SELECT * FROM om.om_dynamic'; EXEC(@sql);";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.DoesNotContain(refs, r => r.Name.Equals("om.om_dynamic", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeMappingRepository : ICustomerMappingRepository
    {
        private readonly IReadOnlyList<CustomerMappingItem> _items;

        public FakeMappingRepository(IReadOnlyList<CustomerMappingItem> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<CustomerMappingItem>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_items);

        public Task<CustomerMappingItem?> GetByCodeAsync(string customerCode, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.CustomerCode == customerCode));

        public Task UpsertAsync(CustomerMappingItem mapping, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
