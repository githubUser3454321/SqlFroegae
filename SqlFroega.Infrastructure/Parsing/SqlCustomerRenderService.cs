using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlFroega.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Parsing;

public sealed class SqlCustomerRenderService : ISqlCustomerRenderService
{
    private const string CanonicalDbUser = "om";
    private const string CanonicalPrefix = "om_";
    private readonly ICustomerMappingRepository _mappingRepository;
    private static readonly Regex JoinOrApplyTargetLineBreakRegex = new(
        @"(?im)^(?<indent>[ \t]*)(?<clause>(?:(?:(?:INNER|LEFT|RIGHT|FULL)(?:\s+OUTER)?|CROSS)\s+JOIN|JOIN|(?:CROSS|OUTER)\s+APPLY))\s*\r?\n(?<targetIndent>[ \t]*)(?<target>.+)$",
        RegexOptions.Compiled);

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

    public Task<string> FormatSqlAsync(string sql, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var sourceSql = sql ?? string.Empty;
        var fragment = ParseSql(sql);
        var leadingComments = ExtractLeadingComments(fragment, sourceSql);
        var options = new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IncludeSemicolons = true,
            NewLineBeforeFromClause = true,
            NewLineBeforeWhereClause = true,
            NewLineBeforeGroupByClause = true,
            NewLineBeforeOrderByClause = true,
            NewLineBeforeJoinClause = true,
            AlignClauseBodies = false,
            IndentationSize = 4
        };

        var generator = new Sql160ScriptGenerator(options);
        generator.GenerateScript(fragment, out var formattedSql);

        var normalized = (formattedSql?.Trim() ?? string.Empty);

        if (StartsWithSemicolonWithClause(fragment) && normalized.StartsWith("WITH ", StringComparison.OrdinalIgnoreCase))
            normalized = ";" + normalized;

        if (!string.IsNullOrWhiteSpace(leadingComments) && !StartsWithLeadingComments(normalized, leadingComments))
            normalized = leadingComments + Environment.NewLine + normalized;

        normalized = NormalizeJoinAndApplyTargetLineBreaks(normalized);

        return Task.FromResult(normalized);
    }

    private static bool StartsWithLeadingComments(string formattedSql, string leadingComments)
        => formattedSql.StartsWith(leadingComments, StringComparison.Ordinal);

    private static string NormalizeJoinAndApplyTargetLineBreaks(string sql)
        => JoinOrApplyTargetLineBreakRegex.Replace(sql, m => $"{m.Groups["indent"].Value}{m.Groups["clause"].Value} {m.Groups["target"].Value}");

    private static bool StartsWithSemicolonWithClause(TSqlFragment fragment)
    {
        var tokens = fragment.ScriptTokenStream;
        if (tokens is null || tokens.Count == 0)
            return false;

        var first = NextSignificantTokenIndex(tokens, 0);
        if (first < 0 || tokens[first].TokenType != TSqlTokenType.Semicolon)
            return false;

        var second = NextSignificantTokenIndex(tokens, first + 1);
        return second >= 0 && tokens[second].TokenType == TSqlTokenType.With;
    }

    private static string ExtractLeadingComments(TSqlFragment fragment, string sourceSql)
    {
        if (string.IsNullOrEmpty(sourceSql))
            return string.Empty;

        var tokens = fragment.ScriptTokenStream;
        if (tokens is null || tokens.Count == 0)
            return string.Empty;

        var endOffset = -1;
        var sawComment = false;

        foreach (var token in tokens)
        {
            if (token.TokenType == TSqlTokenType.SingleLineComment || token.TokenType == TSqlTokenType.MultilineComment)
            {
                sawComment = true;
                endOffset = token.Offset + token.Text.Length;
                continue;
            }

            if (IsTriviaToken(token.TokenType))
            {
                if (sawComment)
                    endOffset = token.Offset + token.Text.Length;

                continue;
            }

            break;
        }

        if (!sawComment || endOffset <= 0)
            return string.Empty;

        return sourceSql[..Math.Min(endOffset, sourceSql.Length)].TrimEnd();
    }

    private static int NextSignificantTokenIndex(IList<TSqlParserToken> tokens, int startIndex)
    {
        for (var i = startIndex; i < tokens.Count; i++)
        {
            if (!IsTriviaToken(tokens[i].TokenType))
                return i;
        }

        return -1;
    }

    private static bool IsTriviaToken(TSqlTokenType tokenType)
        => tokenType == TSqlTokenType.WhiteSpace
        || tokenType == TSqlTokenType.SingleLineComment
        || tokenType == TSqlTokenType.MultilineComment;

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
        var diagnostics = BuildParserDiagnostics(sql ?? string.Empty, errors, first);
        throw new InvalidOperationException($"SQL parse failed at line {first.Line}, col {first.Column}: {first.Message}{Environment.NewLine}{diagnostics}");
    }

    private static string BuildParserDiagnostics(string sql, IList<ParseError> errors, ParseError first)
    {
        var lines = sql.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var lineIndex = Math.Max(0, Math.Min(lines.Length - 1, first.Line - 1));
        var failingLine = lines.Length == 0 ? string.Empty : lines[lineIndex];
        var pointerPadding = new string(' ', Math.Max(0, first.Column - 1));
        var sb = new StringBuilder();
        sb.AppendLine("Parser diagnostics:");
        sb.AppendLine($"Line {first.Line}: {failingLine}");
        sb.AppendLine($"         {pointerPadding}^");

        if (errors.Count > 1)
        {
            sb.AppendLine("Additional parser errors:");
            foreach (var err in errors.Skip(1).Take(4))
                sb.AppendLine($"- line {err.Line}, col {err.Column}: {err.Message}");
        }

        return sb.ToString().TrimEnd();
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
        private readonly HashSet<string> _usedSourcePrefixes = new(StringComparer.OrdinalIgnoreCase);
        private string? _usedQualifiedSourceSchema;

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
            if (node.Identifiers.Count < 1 || node.Identifiers.Count >= 3)
                return;

            if (node.Identifiers.Count == 1)
            {
                var obj = node.Identifiers[0];
                var objectName = obj.Value ?? string.Empty;
                var rule = FindRuleForUnqualifiedObject(objectName);
                if (rule is null)
                    return;

                EnsurePrefixConsistency(rule.SourcePrefix);

                var rewrittenObject = rule.TargetPrefix + objectName[rule.SourcePrefix.Length..];
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

            var matchedRules = _rules
                .Where(r => currentSchema.Equals(r.SourceSchema, StringComparison.OrdinalIgnoreCase)
                         && currentObject.StartsWith(r.SourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchedRules.Count == 0)
            {
                var schemaRules = _rules
                    .Where(r => currentSchema.Equals(r.SourceSchema, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (schemaRules.Count == 0)
                    return;

                var schemaRule = schemaRules[0];
                EnsureQualifiedSchemaConsistency(schemaRule.SourceSchema);

                var schemaOnlyReplacement = FormatIdentifier(schema, schemaRule.TargetSchema) + "." + FormatIdentifier(obj2, currentObject);
                _replacements.Add(new TextReplacement(node.StartOffset, node.FragmentLength, schemaOnlyReplacement));
                return;
            }

            var distinctPrefixes = matchedRules.Select(r => r.SourcePrefix).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctPrefixes.Count > 1)
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");

            var rule2 = matchedRules[0];
            EnsurePrefixConsistency(rule2.SourcePrefix);
            EnsureQualifiedSchemaConsistency(rule2.SourceSchema);

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

            var distinctPrefixes = matchedRules.Select(r => r.SourcePrefix).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctPrefixes.Count > 1)
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");

            return matchedRules[0];
        }

        private void EnsurePrefixConsistency(string prefix)
        {
            _usedSourcePrefixes.Add(prefix);
            if (_usedSourcePrefixes.Count > 1)
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");
        }

        private void EnsureQualifiedSchemaConsistency(string schema)
        {
            if (_usedQualifiedSourceSchema is null)
            {
                _usedQualifiedSourceSchema = schema;
                return;
            }

            if (!_usedQualifiedSourceSchema.Equals(schema, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Script contains mixed schema/prefix mappings (e.g. om_db.syn_ and om_db2.syn2_). Automatic replacement has been disabled.");
        }

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
