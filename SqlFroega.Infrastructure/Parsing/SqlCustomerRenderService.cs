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
        var mappings = await _mappingRepository.GetAllAsync(ct);
        if (mappings.Count == 0)
            return sql;

        var fragment = ParseSql(sql);
        ValidateStorageSafety(fragment);

        var rewriter = new SqlTextRuleRewriter(sql ?? string.Empty);
        foreach (var mapping in mappings)
            rewriter.AddRule(mapping.DatabaseUser, mapping.ObjectPrefix, CanonicalDbUser, CanonicalPrefix);

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

    private static void ValidateStorageSafety(TSqlFragment fragment)
    {
        var guard = new StorageSqlSafetyVisitor();
        fragment.Accept(guard);

        if (guard.ContainsUseStatement)
            throw new InvalidOperationException("Scripts with USE statements are not allowed.");

        if (guard.ContainsDatabaseQualifiedObject)
            throw new InvalidOperationException("Database-qualified object names are not allowed. Please use schema-qualified names only.");
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

    private sealed class StorageSqlSafetyVisitor : TSqlFragmentVisitor
    {
        public bool ContainsUseStatement { get; private set; }
        public bool ContainsDatabaseQualifiedObject { get; private set; }

        public override void ExplicitVisit(UseStatement node)
        {
            ContainsUseStatement = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectName node)
        {
            if (node.Identifiers.Count >= 3)
                ContainsDatabaseQualifiedObject = true;

            base.ExplicitVisit(node);
        }
    }

    private sealed class SqlTextRuleRewriter : TSqlFragmentVisitor
    {
        private readonly string _sql;
        private readonly List<RewriteRule> _rules = new();
        private readonly List<TextReplacement> _replacements = new();
        private readonly HashSet<string> _appliedSourceRuleKeys = new(StringComparer.OrdinalIgnoreCase);

        public SqlTextRuleRewriter(string sql)
        {
            _sql = sql;
        }

        public void AddRule(string sourceSchema, string sourcePrefix, string targetSchema, string targetPrefix)
        {
            if (string.IsNullOrWhiteSpace(sourceSchema) || string.IsNullOrWhiteSpace(sourcePrefix))
                return;

            var normalized = new RewriteRule(
                sourceSchema.Trim(),
                sourcePrefix.Trim(),
                string.IsNullOrWhiteSpace(targetSchema) ? sourceSchema.Trim() : targetSchema.Trim(),
                string.IsNullOrWhiteSpace(targetPrefix) ? sourcePrefix.Trim() : targetPrefix.Trim());

            if (normalized.SourceSchema.Equals("sys", StringComparison.OrdinalIgnoreCase))
                return;

            if (_rules.Any(x => x.SourceSchema.Equals(normalized.SourceSchema, StringComparison.OrdinalIgnoreCase)
                && x.SourcePrefix.Equals(normalized.SourcePrefix, StringComparison.OrdinalIgnoreCase)
                && x.TargetSchema.Equals(normalized.TargetSchema, StringComparison.OrdinalIgnoreCase)
                && x.TargetPrefix.Equals(normalized.TargetPrefix, StringComparison.OrdinalIgnoreCase)))
                return;

            _rules.Add(normalized);
        }

        public string Rewrite(TSqlFragment fragment)
        {
            if (_rules.Count == 0)
                return _sql;

            fragment.Accept(this);
            if (_appliedSourceRuleKeys.Count > 1)
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");

            if (_replacements.Count == 0)
                return _sql;

            var sb = new StringBuilder(_sql);
            foreach (var replacement in _replacements.OrderByDescending(x => x.StartOffset))
                sb.Remove(replacement.StartOffset, replacement.Length).Insert(replacement.StartOffset, replacement.Replacement);

            return sb.ToString();
        }

        public override void ExplicitVisit(SchemaObjectName node)
        {
            TryQueueReplacement(node);
            base.ExplicitVisit(node);
        }

        private void TryQueueReplacement(SchemaObjectName node)
        {
            if (node.Identifiers.Count < 1)
                return;

            if (node.Identifiers.Count >= 3)
                return;

            if (node.Identifiers.Count == 1)
            {
                var obj = node.Identifiers[0];
                var rule = FindRuleForUnqualifiedObject(obj.Value ?? string.Empty);
                if (rule is null)
                    return;

                TrackAppliedRule(rule);
                var rewrittenObject = rule.TargetPrefix + (obj.Value ?? string.Empty)[rule.SourcePrefix.Length..];
                var replacement = FormatIdentifier(obj, rule.TargetSchema) + "." + FormatIdentifier(obj, rewrittenObject);
                _replacements.Add(new TextReplacement(node.StartOffset, node.FragmentLength, replacement));
                return;
            }

            var schema = node.Identifiers[^2];
            var obj2 = node.Identifiers[^1];
            var currentSchema = schema.Value ?? string.Empty;
            var currentObject = obj2.Value ?? string.Empty;

            if (currentSchema.Equals("sys", StringComparison.OrdinalIgnoreCase))
                return;

            var matchedRules = _rules.Where(r =>
                currentSchema.Equals(r.SourceSchema, StringComparison.OrdinalIgnoreCase) &&
                currentObject.StartsWith(r.SourcePrefix, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchedRules.Count == 0)
                return;

            var distinctSourceKeys = matchedRules.Select(GetSourceRuleKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctSourceKeys.Count > 1)
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");

            var rule2 = matchedRules[0];
            TrackAppliedRule(rule2);

            var rewrittenObject2 = rule2.TargetPrefix + currentObject[rule2.SourcePrefix.Length..];
            var replacement2 = FormatIdentifier(schema, rule2.TargetSchema) + "." + FormatIdentifier(obj2, rewrittenObject2);
            _replacements.Add(new TextReplacement(node.StartOffset, node.FragmentLength, replacement2));
        }

        private RewriteRule? FindRuleForUnqualifiedObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            var matchedRules = _rules
                .Where(r => objectName.StartsWith(r.SourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchedRules.Count == 0)
                return null;

            var distinctSourceKeys = matchedRules.Select(GetSourceRuleKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctSourceKeys.Count > 1)
                throw new InvalidOperationException("Script contains unqualified object names that match multiple schema/prefix mappings. Automatic replacement has been disabled.");

            return matchedRules[0];
        }

        private void TrackAppliedRule(RewriteRule rule)
            => _appliedSourceRuleKeys.Add(GetSourceRuleKey(rule));

        private static string GetSourceRuleKey(RewriteRule rule)
            => rule.SourceSchema + "|" + rule.SourcePrefix;

        private static string FormatIdentifier(Identifier original, string value)
            => original.QuoteType == QuoteType.SquareBracket ? $"[{value}]" : value;
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
    private sealed record TextReplacement(int StartOffset, int Length, string Replacement);
}
