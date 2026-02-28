using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Domain;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence.SqlServer;
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
    public void Extractor_FindsColumns_InCreateTableAndQualifiedSelect()
    {
        var sql = @"
CREATE TABLE om.om_adkont_sql (
    KontoId int,
    Name nvarchar(200)
);
SELECT om.om_adkont_sql.KontoId FROM om.om_adkont_sql;";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.KontoId" && r.Type == DbObjectType.Column);
        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.Name" && r.Type == DbObjectType.Column);
    }


    [Fact]
    public void Extractor_FindsColumns_FromAliasQualifiedSelect()
    {
        var sql = @"SELECT omT.[Column] FROM om.om_adkont_sql AS omT;";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.Column" && r.Type == DbObjectType.Column);
    }

    [Theory]
    [InlineData("om_adkont_sql", "om.om_adkont_sql")]
    [InlineData("om_adkont_sql.[Column]", "om.om_adkont_sql.Column")]
    [InlineData("om_adkont_sql.TestSpalte", "om.om_adkont_sql.TestSpalte")]
    public void BuildObjectSearchTokens_AcceptsTableAndColumnShorthand(string input, string expectedToken)
    {
        var method = typeof(SqlFroega.Infrastructure.Persistence.SqlServer.ScriptRepository)
            .GetMethod("BuildObjectSearchTokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var tokens = Assert.IsAssignableFrom<IReadOnlyList<string>>(method!.Invoke(null, new object[] { input })!);
        Assert.Contains(expectedToken, tokens, StringComparer.OrdinalIgnoreCase);
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
