using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Domain;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using System.Text;
using Xunit;

namespace SqlFroega.Tests;

public sealed class SqlParsingTests
{
  
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

    [Theory]
    [MemberData(nameof(NormalizeForStorage_ValidNormalizationCases))]
    public async Task NormalizeForStorage_ValidNormalizationCases_Run(string sql, string expected)
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));
        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> NormalizeForStorage_ValidNormalizationCases()
    {
        yield return new object[] { "SELECT * FROM syn_adkont;", "SELECT * FROM om.om_adkont;" };
        yield return new object[] { "SELECT * FROM syn_adkont_sql;", "SELECT * FROM om.om_adkont_sql;" };
        yield return new object[] { "SELECT * FROM [syn_adkont_sql];", "SELECT * FROM [om].[om_adkont_sql];" };
        yield return new object[] { "SELECT * FROM om_db.syn_adkont_sql;", "SELECT * FROM om.om_adkont_sql;" };
        yield return new object[] { "SELECT * FROM [om_db].[syn_adkont_sql];", "SELECT * FROM [om].[om_adkont_sql];" };
        yield return new object[] { "UPDATE syn_testtable SET col = 1;", "UPDATE om.om_testtable SET col = 1;" };
        yield return new object[] { "DELETE FROM om.syn_oldtable WHERE Id = 1;", "DELETE FROM om.om_oldtable WHERE Id = 1;" };
        yield return new object[] { "MERGE om_db.syn_target AS t USING syn_source AS s ON t.Id = s.Id WHEN MATCHED THEN UPDATE SET t.Name = s.Name;", "MERGE om.om_target AS t USING om.om_source AS s ON t.Id = s.Id WHEN MATCHED THEN UPDATE SET t.Name = s.Name;" };
        yield return new object[] { "SELECT * FROM sys.objects;", "SELECT * FROM sys.objects;" };
        yield return new object[] { "SELECT * FROM syn_testtable t JOIN om.syn_adkont_sql a ON a.Id = t.Id;", "SELECT * FROM om.om_testtable t JOIN om.om_adkont_sql a ON a.Id = t.Id;" };
        yield return new object[] { "SELECT * FROM om_db._ztMembershipSettings; SELECT * FROM om_db.syn_adkont_sql;", "SELECT * FROM om._ztMembershipSettings; SELECT * FROM om.om_adkont_sql;" };
    }

    [Fact]
    public async Task NormalizeForStorage_RewritesQualifiedObjectSchemaWithoutMappedPrefix()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var result = await service.NormalizeForStorageAsync("SELECT * FROM om_db._ztMembershipSettings;");

        Assert.Equal("SELECT * FROM om._ztMembershipSettings;", result);
    }

    [Theory]
    [MemberData(nameof(NormalizeForStorage_MixedPrefixConflictCases))]
    public async Task NormalizeForStorage_MixedPrefixConflictCases_Throw(string sql)
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om_db2", ObjectPrefix = "sws_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync(sql));
    }

    public static IEnumerable<object[]> NormalizeForStorage_MixedPrefixConflictCases()
    {
        yield return new object[] { "SELECT * FROM syn_a; SELECT * FROM sws_b;" };
        yield return new object[] { "SELECT * FROM om_db.syn_a; SELECT * FROM om_db2.sws_b;" };
        yield return new object[] { "UPDATE syn_a SET x=1; DELETE FROM sws_b;" };
        yield return new object[] { "SELECT * FROM [syn_a]; SELECT * FROM [sws_b];" };
        yield return new object[] { "SELECT * FROM om_db.syn_a a JOIN om_db2.sws_b b ON a.Id=b.Id;" };
        yield return new object[] { "MERGE syn_a AS t USING sws_b AS s ON t.Id=s.Id WHEN MATCHED THEN UPDATE SET t.X=s.X;" };
        yield return new object[] { "SELECT * FROM om_db.syn_a; SELECT * FROM sws_b;" };
        yield return new object[] { "SELECT * FROM syn_a; SELECT * FROM om_db2.sws_b;" };
        yield return new object[] { "DELETE FROM om_db.syn_a; DELETE FROM sws_b;" };
        yield return new object[] { "SELECT * FROM syn_a WHERE EXISTS (SELECT 1 FROM sws_b);" };
    }

    [Theory]
    [MemberData(nameof(NormalizeForStorage_QualifiedSchemaConflictCases))]
    public async Task NormalizeForStorage_QualifiedSchemaConflictCases_Throw(string sql)
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NormalizeForStorageAsync(sql));
    }

    public static IEnumerable<object[]> NormalizeForStorage_QualifiedSchemaConflictCases()
    {
        yield return new object[] { "SELECT * FROM om_db.syn_a; SELECT * FROM om.syn_b;" };
        yield return new object[] { "UPDATE om_db.syn_a SET x=1; DELETE FROM om.syn_b;" };
        yield return new object[] { "SELECT * FROM [om_db].[syn_a]; SELECT * FROM [om].[syn_b];" };
        yield return new object[] { "SELECT * FROM om_db.syn_a a JOIN om.syn_b b ON a.Id=b.Id;" };
        yield return new object[] { "MERGE om_db.syn_a AS t USING om.syn_b AS s ON t.Id=s.Id WHEN MATCHED THEN UPDATE SET t.X=s.X;" };
        yield return new object[] { "SELECT * FROM om_db.syn_a; SELECT * FROM syn_b; SELECT * FROM om.syn_c;" };
        yield return new object[] { "DELETE FROM om.syn_b; DELETE FROM om_db.syn_a;" };
        yield return new object[] { "SELECT * FROM om_db.syn_a WHERE EXISTS (SELECT 1 FROM om.syn_b);" };
        yield return new object[] { "SELECT * FROM om.syn_a; SELECT * FROM om_db.syn_b;" };
        yield return new object[] { "SELECT * FROM om_db.syn_testtable; SELECT * FROM om.syn_adkont_sql;" };
    }

    [Fact]
    public async Task NormalizeForStorage_RewritesObjects_InsideCteWithBlock()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var sql = @"WITH CustomerRows AS (
    SELECT x.Id
    FROM om_db.syn_MyTable AS x
    JOIN om_db.syn_Joined AS j ON j.Id = x.Id
)
SELECT *
FROM CustomerRows;";

        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Contains("FROM om.om_MyTable AS x", result);
        Assert.Contains("JOIN om.om_Joined AS j", result);
    }

    [Fact]
    public async Task NormalizeForStorage_RewritesObjects_InRecursiveCteReferences()
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));

        var sql = @"WITH RecursiveRows AS (
    SELECT RootId, ParentId
    FROM om_db.syn_MyTable
    UNION ALL
    SELECT c.RootId, c.ParentId
    FROM om_db.syn_MyTable AS c
    JOIN RecursiveRows AS r ON r.RootId = c.ParentId
)
SELECT *
FROM RecursiveRows;";

        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Contains("FROM om.om_MyTable", result);
        Assert.DoesNotContain("om_db.syn_MyTable", result, StringComparison.OrdinalIgnoreCase);
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
    [InlineData("Type", "type")]
    [InlineData("adkont_sql.Type", "adkont_sql.type")]
    [InlineData("om_adkont_sql.Type", "om_adkont_sql.type")]
    [InlineData("om_db.om_adkont_sql.Type", "om_db.om_adkont_sql.type")]
    public void BuildObjectSearchTokens_AcceptsHierarchicalShorthand(string input, string expectedToken)
    {
        var method = typeof(SqlFroega.Infrastructure.Persistence.SqlServer.ScriptRepository)
            .GetMethod("BuildObjectSearchTokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var tokens = Assert.IsAssignableFrom<IReadOnlyList<string>>(method!.Invoke(null, new object[] { input })!);
        Assert.Contains(expectedToken, tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildObjectSearchTokens_ExcludesSqlKeywords()
    {
        var method = typeof(SqlFroega.Infrastructure.Persistence.SqlServer.ScriptRepository)
            .GetMethod("BuildObjectSearchTokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var tokens = Assert.IsAssignableFrom<IReadOnlyList<string>>(method!.Invoke(null, new object[] { "SELECT" })!);
        Assert.Empty(tokens);
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

    [Fact]
    public void Extractor_Throws_OnUnknownAliasQualifier()
    {
        var sql = @";with Cache AS (
    SELECT top 10
        Id,
        c.MoreId,
        a.InvoiceId,
        a.*
    from om.om_InvoiceView as a
    where a.RecordName = 'Hello'
)
SELECT
    A.MembershipId
FROM om.om_BaseMembershipDefaultView as A
inner join Cache as B
    ON a.RecordId2 = B.RecordId2";

        var extractor = new SqlObjectReferenceExtractor();

        var ex = Assert.Throws<InvalidOperationException>(() => extractor.Extract(sql));
        Assert.Contains("Unresolved column qualifier 'c'", ex.Message);
    }

    [Fact]
    public void Extractor_Finds_ThreePartQualifiedColumn()
    {
        var sql = @"SELECT om.om_InvoiceView.RecordId2 FROM om.om_InvoiceView;";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_InvoiceView.RecordId2" && r.Type == DbObjectType.Column);
    }

    [Fact]
    public void Extractor_Maps_CteAliasColumn_ToUnderlyingSourceTable_WhenWildcardIsSelected()
    {
        var sql = @";with Cache AS (
    select
        a.*
    from om.om_InvoiceView as a
    where a.RecordName = 'Hello'
)
SELECT
    A.MembershipId
FROM om.om_BaseMembershipDefaultView as A
inner join Cache as B
    ON a.RecordId2 = B.RecordId2";

        var extractor = new SqlObjectReferenceExtractor();
        var extractorDiagnostics = new List<string>();
        var refs = extractor.Extract(sql, extractorDiagnostics);

        var expectedName = "om.om_InvoiceView.RecordId2";
        var expectedType = DbObjectType.Column;

        var hasExpectedReference = refs.Any(r => r.Name == expectedName && r.Type == expectedType);

        var debug = new StringBuilder()
            .AppendLine("Expected reference was not found.")
            .AppendLine($"Expected: Name='{expectedName}', Type='{expectedType}'")
            .AppendLine($"Total extracted refs: {refs.Count}")
            .AppendLine("Extracted refs:");

        foreach (var reference in refs.OrderBy(r => r.Type).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            debug.AppendLine($" - {reference.Type}: {reference.Name}");
        }

        debug
            .AppendLine("Potentially related refs (contains 'RecordId2' or 'InvoiceView'):")
            .AppendLine($" - RecordId2 refs: {string.Join(", ", refs.Where(r => r.Name.Contains("RecordId2", StringComparison.OrdinalIgnoreCase)).Select(r => $"{r.Type}:{r.Name}"))}")
            .AppendLine($" - InvoiceView refs: {string.Join(", ", refs.Where(r => r.Name.Contains("InvoiceView", StringComparison.OrdinalIgnoreCase)).Select(r => $"{r.Type}:{r.Name}"))}")
            .AppendLine("Extractor diagnostics:")
            .AppendLine(extractorDiagnostics.Count == 0 ? " - <none>" : string.Join(Environment.NewLine, extractorDiagnostics.Select(d => $" - {d}")))
            .AppendLine("Original SQL:")
            .AppendLine(sql);

        Assert.True(hasExpectedReference, debug.ToString());
    }

    [Fact]
    public void Extractor_KeepsSourceTableReference_ForCteWithExplicitColumnsAndWildcard()
    {
        var sql = @";with Cache AS (
    select
        a.Id,
        a.InvoiceId,
        a.*
    from om.om_InvoiceView as a
    where a.RecordName = 'Hello'
)
SELECT
    A.MembershipId
FROM om.om_BaseMembershipDefaultView as A
inner join Cache as B
    ON a.RecordId2 = B.RecordId2";

        var extractor = new SqlObjectReferenceExtractor();
        var refs = extractor.Extract(sql);

        Assert.Contains(refs, r => r.Name == "om.om_InvoiceView" && r.Type == DbObjectType.Table);
    }

    [Theory]
    [MemberData(nameof(Extractor_SpecialTsqlReferenceCases))]
    public void Extractor_HandlesSpecialTsqlObjectReferences(string sql, string[] expectedTables, string[] expectedColumns)
    {
        var extractor = new SqlObjectReferenceExtractor();

        var refs = extractor.Extract(sql);

        foreach (var expectedTable in expectedTables)
        {
            Assert.Contains(refs, r => r.Name == expectedTable && r.Type == DbObjectType.Table);
        }

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.Contains(refs, r => r.Name == expectedColumn && r.Type == DbObjectType.Column);
        }
    }

    public static IEnumerable<object[]> Extractor_SpecialTsqlReferenceCases()
    {
        yield return
        [
            @"SELECT c.CustomerId
FROM om.om_Customers AS c
GROUP BY c.CustomerId
HAVING COUNT(*) > 1;",
            new[] { "om.om_Customers" },
            new[] { "om.om_Customers.CustomerId" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, SUM(o.Amount) AS TotalAmount
FROM om.om_Customers AS c
JOIN om.om_Orders AS o ON o.CustomerId = c.CustomerId
GROUP BY c.CustomerId
HAVING SUM(o.Amount) > 500
ORDER BY SUM(o.Amount) DESC;",
            new[] { "om.om_Customers", "om.om_Orders" },
            new[] { "om.om_Customers.CustomerId", "om.om_Orders.Amount", "om.om_Orders.CustomerId" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, lastOrder.LastOrderDate
FROM om.om_Customers AS c
OUTER APPLY (
    SELECT TOP (1) o.OrderDate AS LastOrderDate
    FROM om.om_Orders AS o
    WHERE o.CustomerId = c.CustomerId
    ORDER BY o.OrderDate DESC
) AS lastOrder;",
            new[] { "om.om_Customers", "om.om_Orders" },
            new[] { "om.om_Customers.CustomerId", "om.om_Orders.CustomerId", "om.om_Orders.OrderDate" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, calc.TotalAmount
FROM om.om_Customers AS c
OUTER APPLY (
    SELECT SUM(x.Amount) AS TotalAmount
    FROM om.om_OrderLines AS x
    WHERE x.CustomerId = c.CustomerId
) AS calc
ORDER BY calc.TotalAmount DESC;",
            new[] { "om.om_Customers", "om.om_OrderLines" },
            new[] { "om.om_Customers.CustomerId", "om.om_OrderLines.Amount", "om.om_OrderLines.CustomerId" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, oa3.LatestStatus
FROM om.om_Customers AS c
OUTER APPLY (
    SELECT oa2.LatestStatus
    FROM (
        SELECT oa1.LatestStatus
        FROM (
            SELECT TOP (1) s.StatusName AS LatestStatus
            FROM om.om_StatusHistory AS s
            WHERE s.CustomerId = c.CustomerId
            ORDER BY s.ChangedAt DESC
        ) AS oa1
    ) AS oa2
) AS oa3
ORDER BY oa3.LatestStatus;",
            new[] { "om.om_Customers", "om.om_StatusHistory" },
            new[] { "om.om_Customers.CustomerId", "om.om_StatusHistory.StatusName", "om.om_StatusHistory.CustomerId", "om.om_StatusHistory.ChangedAt" }
        ];

        yield return
        [
            @"SELECT c.CustomerId
FROM om.om_Customers AS c
WHERE EXISTS (
    SELECT 1
    FROM om.om_Orders AS o
    WHERE o.CustomerId = c.CustomerId
    GROUP BY o.CustomerId
    HAVING COUNT(*) > 2
);",
            new[] { "om.om_Customers", "om.om_Orders" },
            new[] { "om.om_Customers.CustomerId", "om.om_Orders.CustomerId" }
        ];

        yield return
        [
            @"WITH RankedOrders AS (
    SELECT o.CustomerId,
           o.OrderId,
           ROW_NUMBER() OVER (PARTITION BY o.CustomerId ORDER BY o.OrderDate DESC) AS rn
    FROM om.om_Orders AS o
)
SELECT ro.CustomerId
FROM RankedOrders AS ro
JOIN om.om_Customers AS c ON c.CustomerId = ro.CustomerId
WHERE ro.rn = 1
ORDER BY ro.CustomerId;",
            new[] { "om.om_Orders", "om.om_Customers" },
            new[] { "om.om_Orders.CustomerId", "om.om_Orders.OrderId", "om.om_Orders.OrderDate", "om.om_Customers.CustomerId" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, p.ProductName
FROM om.om_Customers AS c
OUTER APPLY (
    SELECT TOP (1) p.ProductName
    FROM om.om_Products AS p
    JOIN om.om_OrderLines AS ol ON ol.ProductId = p.ProductId
    WHERE ol.CustomerId = c.CustomerId
    ORDER BY ol.CreatedAt DESC
) AS p
ORDER BY p.ProductName;",
            new[] { "om.om_Customers", "om.om_Products", "om.om_OrderLines" },
            new[] { "om.om_Customers.CustomerId", "om.om_Products.ProductName", "om.om_Products.ProductId", "om.om_OrderLines.ProductId", "om.om_OrderLines.CustomerId", "om.om_OrderLines.CreatedAt" }
        ];

        yield return
        [
            @"SELECT c.CustomerId, oa.Score
FROM om.om_Customers AS c
OUTER APPLY (
    SELECT SUM(m.Points) AS Score
    FROM om.om_Metrics AS m
    WHERE m.CustomerId = c.CustomerId
    GROUP BY m.CustomerId
    HAVING SUM(m.Points) > 100
) AS oa
ORDER BY oa.Score DESC;",
            new[] { "om.om_Customers", "om.om_Metrics" },
            new[] { "om.om_Customers.CustomerId", "om.om_Metrics.Points", "om.om_Metrics.CustomerId" }
        ];

        yield return
        [
            @"SELECT base.CustomerId, nestedOa.ValueFromThirdApply
FROM om.om_Customers AS base
OUTER APPLY (
    SELECT level2.ValueFromThirdApply
    FROM (
        SELECT level1.ValueFromThirdApply
        FROM (
            SELECT TOP (1) d.DetailValue AS ValueFromThirdApply
            FROM om.om_CustomerDetails AS d
            WHERE d.CustomerId = base.CustomerId
            ORDER BY d.ChangedAt DESC
        ) AS level1
    ) AS level2
) AS nestedOa
ORDER BY nestedOa.ValueFromThirdApply;",
            new[] { "om.om_Customers", "om.om_CustomerDetails" },
            new[] { "om.om_Customers.CustomerId", "om.om_CustomerDetails.DetailValue", "om.om_CustomerDetails.CustomerId", "om.om_CustomerDetails.ChangedAt" }
        ];
    }

    [Theory]
    [MemberData(nameof(Extractor_DedupAndScopeEdgeCases))]
    public void Extractor_DedupAndScopeEdgeCases_Run(string sql, string[] expectedUniqueRefs)
    {
        var extractor = new SqlObjectReferenceExtractor();

        var refs = extractor.Extract(sql);

        foreach (var expected in expectedUniqueRefs)
            Assert.Contains(refs, r => r.Name == expected);

        foreach (var expected in expectedUniqueRefs)
            Assert.Equal(1, refs.Count(r => r.Name.Equals(expected, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<object[]> Extractor_DedupAndScopeEdgeCases()
    {
        yield return
        [
            @"SELECT om.om_table.Column1, om.om_table.Column1
FROM om.om_table
WHERE om.om_table.Column1 IS NOT NULL;",
            new[] { "om.om_table", "om.om_table.Column1" }
        ];

        yield return
        [
            @"SELECT a.Id
FROM om.om_table AS a
INNER JOIN om.om_table AS b ON a.Id = b.Id;",
            new[] { "om.om_table", "om.om_table.Id" }
        ];

        yield return
        [
            @"SELECT [om].[om_table].[Column1]
FROM [om].[om_table]
WHERE [om].[om_table].[Column1] > 0;",
            new[] { "om.om_table", "om.om_table.Column1" }
        ];

        yield return
        [
            @"SELECT x.ColA
FROM (SELECT t.ColA FROM om.om_table AS t) AS x
WHERE x.ColA > 1;",
            new[] { "om.om_table", "om.om_table.ColA" }
        ];

        yield return
        [
            @"WITH C AS (
    SELECT t.Col1, t.Col2
    FROM om.om_table AS t
)
SELECT c.Col1
FROM C AS c
WHERE c.Col2 IS NOT NULL;",
            new[] { "om.om_table", "om.om_table.Col1", "om.om_table.Col2" }
        ];

        yield return
        [
            @"SELECT o.OrderId
FROM om.om_orders AS o
WHERE EXISTS (
    SELECT 1
    FROM om.om_order_items AS i
    WHERE i.OrderId = o.OrderId AND i.OrderId > 0
);",
            new[] { "om.om_orders", "om.om_order_items", "om.om_orders.OrderId", "om.om_order_items.OrderId" }
        ];

        yield return
        [
            @"SELECT d.Id
FROM om.om_data AS d
UNION ALL
SELECT d2.Id
FROM om.om_data AS d2;",
            new[] { "om.om_data", "om.om_data.Id" }
        ];

        yield return
        [
            @"SELECT topRows.Id
FROM (
    SELECT TOP (10) x.Id
    FROM om.om_table AS x
    ORDER BY x.Id DESC
) AS topRows;",
            new[] { "om.om_table", "om.om_table.Id" }
        ];

        yield return
        [
            @"SELECT t.Id, t.Name
FROM om.om_table AS t
GROUP BY t.Id, t.Name
HAVING COUNT(*) > 0
ORDER BY t.Name;",
            new[] { "om.om_table", "om.om_table.Id", "om.om_table.Name" }
        ];

        yield return
        [
            @"SELECT o.Id
FROM om.om_orders AS o
OUTER APPLY (
    SELECT TOP (1) i.Id
    FROM om.om_order_items AS i
    WHERE i.OrderId = o.Id
    ORDER BY i.Id DESC
) AS lastItem;",
            new[] { "om.om_orders", "om.om_order_items", "om.om_orders.Id", "om.om_order_items.Id", "om.om_order_items.OrderId" }
        ];
    }

    [Theory]
    [MemberData(nameof(NormalizeForStorage_AdditionalEdgeCases))]
    public async Task NormalizeForStorage_AdditionalEdgeCases_Run(string sql, string expected)
    {
        var mappings = new List<CustomerMappingItem>
        {
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C1", CustomerName = "C1", DatabaseUser = "om_db", ObjectPrefix = "syn_" },
            new() { CustomerId = Guid.NewGuid(), CustomerCode = "C2", CustomerName = "C2", DatabaseUser = "om", ObjectPrefix = "syn_" }
        };

        var service = new SqlCustomerRenderService(new FakeMappingRepository(mappings));
        var result = await service.NormalizeForStorageAsync(sql);

        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> NormalizeForStorage_AdditionalEdgeCases()
    {
        yield return new object[] { "SELECT * FROM [om_db].[syn_A];", "SELECT * FROM [om].[om_A];" };
        yield return new object[] { "SELECT * FROM om_db.syn_A a CROSS JOIN om_db.syn_B b;", "SELECT * FROM om.om_A a CROSS JOIN om.om_B b;" };
        yield return new object[] { "WITH X AS (SELECT * FROM om_db.syn_A) SELECT * FROM X;", "WITH X AS (SELECT * FROM om.om_A) SELECT * FROM X;" };
        yield return new object[] { "SELECT * FROM syn_A WHERE EXISTS (SELECT 1 FROM om_db.syn_B b WHERE b.Id = 1);", "SELECT * FROM om.om_A WHERE EXISTS (SELECT 1 FROM om.om_B b WHERE b.Id = 1);" };
        yield return new object[] { "UPDATE om_db.syn_A SET Name='x' FROM om_db.syn_A a JOIN syn_B b ON a.Id=b.Id;", "UPDATE om.om_A SET Name='x' FROM om.om_A a JOIN om.om_B b ON a.Id=b.Id;" };
        yield return new object[] { "DELETE a FROM om_db.syn_A a INNER JOIN om_db.syn_B b ON b.Id=a.Id;", "DELETE a FROM om.om_A a INNER JOIN om.om_B b ON b.Id=a.Id;" };
        yield return new object[] { "SELECT * FROM om_db._ztMembershipSettings s JOIN om_db.syn_A a ON a.Id=s.Id;", "SELECT * FROM om._ztMembershipSettings s JOIN om.om_A a ON a.Id=s.Id;" };
        yield return new object[] { "SELECT * FROM syn_A UNION ALL SELECT * FROM om_db.syn_A;", "SELECT * FROM om.om_A UNION ALL SELECT * FROM om.om_A;" };
        yield return new object[] { "SELECT * FROM [syn_A] AS a JOIN [om_db].[syn_B] AS b ON a.Id=b.Id;", "SELECT * FROM [om].[om_A] AS a JOIN [om].[om_B] AS b ON a.Id=b.Id;" };
        yield return new object[] { "MERGE syn_A AS t USING (SELECT * FROM om_db.syn_B) AS s ON t.Id=s.Id WHEN MATCHED THEN UPDATE SET t.Name=s.Name;", "MERGE om.om_A AS t USING (SELECT * FROM om.om_B) AS s ON t.Id=s.Id WHEN MATCHED THEN UPDATE SET t.Name=s.Name;" };
    }


    [Fact]
    public async Task FormatSql_UppercasesKeywords_AndAddsLineBreaks()
    {
        var service = new SqlCustomerRenderService(new FakeMappingRepository(Array.Empty<CustomerMappingItem>()));

        var result = await service.FormatSqlAsync("select a from om.om_table where id = 1");

        Assert.Equal($@"SELECT a
FROM om.om_table
WHERE id = 1;", result);
    }

    [Fact]
    public async Task FormatSql_Throws_OnInvalidSql()
    {
        var service = new SqlCustomerRenderService(new FakeMappingRepository(Array.Empty<CustomerMappingItem>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FormatSqlAsync("SELECT FROM"));
    }

    [Fact]
    public async Task FormatSql_PreservesLeadingCommentsAndFormatsInnerJoinAndOnWithIndentation()
    {
        var service = new SqlCustomerRenderService(new FakeMappingRepository(Array.Empty<CustomerMappingItem>()));

        var sql = @"--use Test
;with Cache AS (
    select
        a.Id,
        a.InvoiceId,
        a.*,
        c.*
    from om.om_InvoiceView as a
    where a.RecordName = 'Hello'
)

SELECT
    A.MembershipId
FROM om.om_BaseMembershipDefaultView as A
inner join Cache as B
    ON a.RecordId2 = B.RecordId2";

        var result = await service.FormatSqlAsync(sql);

        var diagnostics = BuildSqlFormattingDiagnostics(result);

        Assert.True(result.Contains("--use Test", StringComparison.Ordinal), diagnostics);
        Assert.True(result.Contains(";WITH Cache", StringComparison.Ordinal), diagnostics);
        Assert.True(result.Contains("INNER JOIN Cache", StringComparison.Ordinal), diagnostics);
        Assert.True(result.Contains("B", StringComparison.Ordinal), diagnostics);
        Assert.True(result.Contains("ON a.RecordId2 = B.RecordId2;", StringComparison.Ordinal), diagnostics);
    }

    private static string BuildSqlFormattingDiagnostics(string sql)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Formatted SQL did not match expected shape.");
        sb.AppendLine("Raw output (escaped):");
        sb.AppendLine(EscapeSql(sql));
        sb.AppendLine("Line-by-line:");

        var lines = sql.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
            sb.AppendLine($"{i + 1:00}: '{EscapeSql(lines[i])}'");

        return sb.ToString();
    }

    private static string EscapeSql(string sql)
        => sql
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

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
