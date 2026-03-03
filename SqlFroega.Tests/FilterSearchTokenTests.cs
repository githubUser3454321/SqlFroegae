using System;
using System.Collections.Generic;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using System.Reflection;
using Xunit;

namespace SqlFroega.Tests;

public sealed class FilterSearchTokenTests
{
    private static readonly MethodInfo BuildTokensMethod = typeof(ScriptRepository)
        .GetMethod("BuildObjectSearchTokens", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo NormalizeIdentifierMethod = typeof(ScriptRepository)
        .GetMethod("NormalizeIdentifier", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo SimplifyTableNameMethod = typeof(ScriptRepository)
        .GetMethod("SimplifyTableName", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("Type", "type")]
    [InlineData("om_table", "om_table")]
    [InlineData("om_table.Column", "om_table.column")]
    [InlineData("om.om_table", "om.om_table")]
    [InlineData("om.om_table.Column", "om.om_table.column")]
    [InlineData("[om].[om_table]", "om.om_table")]
    [InlineData("[om].[om_table].[Column]", "om.om_table.column")]
    [InlineData("om_db.om_table.Column", "om_db.om_table.column")]
    [InlineData("om_db.[om_table].[Column]", "om_db.om_table.column")]
    [InlineData("  [om].[om_table]  ", "om.om_table")]
    public void BuildObjectSearchTokens_ReturnsExpectedPrimaryToken(string input, string expected)
    {
        var tokens = InvokeBuildTokens(input);
        Assert.Contains(expected, tokens, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("FROM")]
    [InlineData("WHERE")]
    [InlineData("JOIN")]
    [InlineData("USE")]
    [InlineData("SELECT.Table")]
    [InlineData("om.SELECT")]
    [InlineData("om.table.SELECT")]
    [InlineData("table.WHERE")]
    [InlineData("")]
    public void BuildObjectSearchTokens_RejectsKeywordsAndInvalidInput(string input)
    {
        var tokens = InvokeBuildTokens(input);
        Assert.Empty(tokens);
    }

    [Theory]
    [InlineData("[OM].[Table]", "om.table")]
    [InlineData(" OM.Table ", "om.table")]
    [InlineData("[dbo].[X_Y_Z]", "dbo.x_y_z")]
    [InlineData("[dbo].X_Y", "dbo.x_y")]
    [InlineData("single", "single")]
    [InlineData("[single]", "single")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("[A].[B].[C]", "a.b.c")]
    [InlineData("a.b.c", "a.b.c")]
    public void NormalizeIdentifier_NormalizesAsExpected(string input, string expected)
    {
        var normalized = (string)NormalizeIdentifierMethod.Invoke(null, new object?[] { input })!;
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("om_table", "table")]
    [InlineData("om_adkont_sql", "adkont_sql")]
    [InlineData("table", "table")]
    [InlineData("_leading", "leading")]
    [InlineData("trailing_", "trailing_")]
    [InlineData("a_b_c", "b_c")]
    [InlineData("[om_table]", "table")]
    [InlineData("OM_TABLE", "table")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void SimplifyTableName_SimplifiesAsExpected(string input, string expected)
    {
        var simplified = (string)SimplifyTableNameMethod.Invoke(null, new object?[] { input })!;
        Assert.Equal(expected, simplified);
    }

    private static IReadOnlyList<string> InvokeBuildTokens(string input)
        => Assert.IsAssignableFrom<IReadOnlyList<string>>(BuildTokensMethod.Invoke(null, new object?[] { input })!);
}
