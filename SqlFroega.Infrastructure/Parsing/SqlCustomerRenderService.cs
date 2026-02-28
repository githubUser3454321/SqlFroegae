using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlFroega.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Parsing;

public sealed class SqlCustomerRenderService : ISqlCustomerRenderService
{
    private const string CanonicalDbUser = "om";
    private const string CanonicalPrefix = "om_";
    private readonly ICustomerMappingRepository _mappingRepository;

    public SqlCustomerRenderService(ICustomerMappingRepository mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    public async Task<string> NormalizeForStorageAsync(string sql, CancellationToken ct = default)
    {
        var fragment = ParseSql(sql);

        var mappings = await _mappingRepository.GetAllAsync(ct);
        if (mappings.Count == 0)
            return sql;

        var rewriter = new SchemaObjectNameRewriter();
        foreach (var mapping in mappings)
        {
            rewriter.AddRule(mapping.DatabaseUser, mapping.ObjectPrefix, CanonicalDbUser, CanonicalPrefix);
        }

        return rewriter.Rewrite(fragment);
    }

    public async Task<string> RenderForCustomerAsync(string sql, string customerCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            return sql;

        var mapping = await _mappingRepository.GetByCodeAsync(customerCode.Trim(), ct);
        if (mapping is null)
            throw new InvalidOperationException($"No customer mapping found for '{customerCode}'.");

        var fragment = ParseSql(sql);
        var rewriter = new SchemaObjectNameRewriter();
        rewriter.AddRule(CanonicalDbUser, CanonicalPrefix, mapping.DatabaseUser, mapping.ObjectPrefix);
        return rewriter.Rewrite(fragment);
    }

    private static TSqlFragment ParseSql(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql ?? string.Empty);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count == 0)
            return fragment;

        var first = errors[0];
        throw new InvalidOperationException($"SQL parse failed at line {first.Line}, col {first.Column}: {first.Message}");
    }

    private sealed class SchemaObjectNameRewriter : TSqlFragmentVisitor
    {
        private readonly List<RewriteRule> _rules = new();

        public void AddRule(string sourceSchema, string sourcePrefix, string targetSchema, string targetPrefix)
        {
            if (string.IsNullOrWhiteSpace(sourceSchema) || string.IsNullOrWhiteSpace(sourcePrefix))
                return;

            _rules.Add(new RewriteRule(
                sourceSchema.Trim(),
                sourcePrefix.Trim(),
                string.IsNullOrWhiteSpace(targetSchema) ? sourceSchema.Trim() : targetSchema.Trim(),
                string.IsNullOrWhiteSpace(targetPrefix) ? sourcePrefix.Trim() : targetPrefix.Trim()));
        }

        public string Rewrite(TSqlFragment fragment)
        {
            if (_rules.Count == 0)
                return GetSql(fragment);

            fragment.Accept(this);
            return GetSql(fragment);
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            TryRewrite(node.SchemaObject);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectName node)
        {
            TryRewrite(node);
            base.ExplicitVisit(node);
        }

        private void TryRewrite(SchemaObjectName? node)
        {
            if (node is null || node.Identifiers.Count < 2)
                return;

            var schema = node.Identifiers[^2];
            var obj = node.Identifiers[^1];
            var currentSchema = schema.Value ?? string.Empty;
            var currentObject = obj.Value ?? string.Empty;

            var rule = _rules.FirstOrDefault(r =>
                currentSchema.Equals(r.SourceSchema, StringComparison.OrdinalIgnoreCase) &&
                currentObject.StartsWith(r.SourcePrefix, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
                return;

            schema.Value = rule.TargetSchema;
            obj.Value = rule.TargetPrefix + currentObject[rule.SourcePrefix.Length..];
        }

        private static string GetSql(TSqlFragment fragment)
        {
            var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions
            {
                IncludeSemicolons = true,
                NewLineBeforeFromClause = false,
                NewLineBeforeWhereClause = false
            });

            generator.GenerateScript(fragment, out var sql);
            return sql;
        }
    }

    private sealed record RewriteRule(string SourceSchema, string SourcePrefix, string TargetSchema, string TargetPrefix);
}
