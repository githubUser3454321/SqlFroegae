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
    [InlineData("SELECT * FROM [abc].[abc_table]", "SELECT * FROM [om].[om_table]")]
    [InlineData("SELECT * FROM abc.abc_table", "SELECT * FROM om.om_table")]
    [InlineData("SELECT *\nFROM [AbC] . [AbC_Table]", "SELECT *\nFROM [om].[om_Table]")]
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
    public async Task NormalizeForStorage_Rejects_UseStatement()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync("USE OtherDb; SELECT * FROM om_db.syn_adkont_sql;"));
    }

    [Fact]
    public async Task NormalizeForStorage_Rejects_DatabaseQualifiedObjectName()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync("SELECT * FROM OtherDb.om_db.syn_adkont_sql;"));
    }

    [Fact]
    public async Task NormalizeForStorage_Rewrites_Objects_ButNotColumns()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var sql = "SELECT omT.[Column] FROM om_db.syn_adkont_sql AS omT WHERE omT.syn_status = 1;";
        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Equal("SELECT omT.[Column] FROM om.om_adkont_sql AS omT WHERE omT.syn_status = 1;", result);
    }


    [Fact]
    public async Task NormalizeForStorage_Rewrites_UnqualifiedObjectName()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var result = await service.NormalizeForStorageAsync("SELECT * FROM syn_adkont_sql;");

        Assert.Equal("SELECT * FROM om.om_adkont_sql;", result);
    }

    [Fact]
    public async Task NormalizeForStorage_Throws_OnMixedSourceMappingsInScript()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om_db2", ObjectPrefix = "syn2_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync("SELECT * FROM om_db.syn_adkont_sql; SELECT * FROM om_db2.syn2_x;"));
    }


    [Fact]
    public async Task NormalizeForStorage_DoesNotAppendSqlSuffix()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var result = await service.NormalizeForStorageAsync("SELECT * FROM syn_adkont;");

        Assert.Equal("SELECT * FROM om.om_adkont;", result);
    }

    [Fact]
    public async Task NormalizeForStorage_AllowsUnqualifiedSamePrefixAcrossMultipleSchemas()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var result = await service.NormalizeForStorageAsync("SELECT * FROM syn_testtable; SELECT * FROM om.syn_adkont_sql;");

        Assert.Equal("SELECT * FROM om.om_testtable; SELECT * FROM om.om_adkont_sql;", result);
    }

    [Fact]
    public async Task NormalizeForStorage_Throws_WhenQualifiedSchemaDiffersForSamePrefix()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync("SELECT * FROM om_db.syn_TestTable; SELECT * FROM om.syn_adkont_sql;"));
    }

    [Fact]
    public async Task NormalizeForStorage_DoesNotRewrite_SysObjects()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "sys", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var result = await service.NormalizeForStorageAsync("SELECT * FROM sys.syn_objects;");

        Assert.Equal("SELECT * FROM sys.syn_objects;", result);
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
    public void BuildObjectSearchTokens_InferSchema_FromSingleToken()
    {
        var method = typeof(SqlFroega.Infrastructure.Persistence.SqlServer.ScriptRepository)
            .GetMethod("BuildObjectSearchTokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var tokens = Assert.IsAssignableFrom<IReadOnlyList<string>>(method!.Invoke(null, new object[] { "syn_adkont_sql" })!);
        Assert.Contains("om_db.syn_adkont_sql", tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extractor_MapsUnqualifiedColumn_ToSingleFromTable()
    {
        var sql = @"SELECT omT.[Column2]
FROM om.om_adkont_sql AS omT
WHERE omT.testColumn IS NULL AND [Column] = '';";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.Column2" && r.Type == DbObjectType.Column);
        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.testColumn" && r.Type == DbObjectType.Column);
        Assert.Contains(refs, r => r.Name == "om.om_adkont_sql.Column" && r.Type == DbObjectType.Column);
    }

    [Fact]
    public void Extractor_MapsUnqualifiedColumn_ToAllFromTables_WhenJoinExists()
    {
        var sql = @"SELECT MyColumn, A.Age, B.Name
FROM om.om_ages AS A
INNER JOIN om.om_names AS B ON A.Id = B.Id;";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_ages.MyColumn" && r.Type == DbObjectType.Column);
        Assert.Contains(refs, r => r.Name == "om.om_names.MyColumn" && r.Type == DbObjectType.Column);
    }



    [Fact]
    public void Extractor_MapsUnqualifiedColumn_OnlyToCurrentSubqueryScope()
    {
        var sql = @"SELECT a.Id
FROM om.om_a AS a
WHERE EXISTS (
    SELECT 1
    FROM om.om_types AS t
    WHERE Type = 'X'
);";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_types.Type" && r.Type == DbObjectType.Column);
        Assert.DoesNotContain(refs, r => r.Name == "om.om_a.Type" && r.Type == DbObjectType.Column);
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
        public Task DeleteAsync(Guid customerId, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<CustomerMappingItem>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_items);

        public Task<CustomerMappingItem?> GetByCodeAsync(string customerCode, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.CustomerCode == customerCode));

        public Task UpsertAsync(CustomerMappingItem mapping, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
