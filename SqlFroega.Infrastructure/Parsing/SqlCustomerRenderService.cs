using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlFroega.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SqlFroega.Infrastructure.Parsing;

public sealed class SqlCustomerRenderService : ISqlCustomerRenderService
{
    private const string CanonicalSchema = "om";
    private const string CanonicalPrefix = "om_";
    private readonly ICustomerMappingRepository _mappingRepository;

    public SqlCustomerRenderService(ICustomerMappingRepository mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    public async Task<string> NormalizeForStorageAsync(string sql, CancellationToken ct = default)
    {
        ValidateSql(sql);

        var mappings = await _mappingRepository.GetAllAsync(ct);
        if (mappings.Count == 0)
            return sql;

        var normalized = sql;
        foreach (var mapping in mappings)
        {
            normalized = ReplaceSchemaPrefix(normalized, mapping.SchemaName, mapping.ObjectPrefix, CanonicalSchema, CanonicalPrefix);
            normalized = ReplaceDbUserPrefix(normalized, mapping.DatabaseUser, CanonicalSchema, CanonicalPrefix);
        }

        return normalized;
    }

    public async Task<string> RenderForCustomerAsync(string sql, string customerCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            return sql;

        var mapping = await _mappingRepository.GetByCodeAsync(customerCode.Trim(), ct);
        if (mapping is null)
            throw new InvalidOperationException($"No customer mapping found for '{customerCode}'.");

        return ReplaceSchemaPrefix(sql, CanonicalSchema, CanonicalPrefix, mapping.SchemaName, mapping.ObjectPrefix);
    }

    private static void ValidateSql(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql ?? string.Empty);
        parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count == 0)
            return;

        var first = errors[0];
        throw new InvalidOperationException($"SQL parse failed at line {first.Line}, col {first.Column}: {first.Message}");
    }

    private static string ReplaceSchemaPrefix(string sql, string sourceSchema, string sourcePrefix, string targetSchema, string targetPrefix)
    {
        if (string.IsNullOrWhiteSpace(sourceSchema) || string.IsNullOrWhiteSpace(sourcePrefix))
            return sql;

        var pattern = $@"(?i)(\[?{Regex.Escape(sourceSchema)}\]?\s*\.\s*\[?){Regex.Escape(sourcePrefix)}";
        return Regex.Replace(sql, pattern, m =>
        {
            var schemaPart = m.Groups[1].Value;
            return schemaPart.Replace(sourceSchema, targetSchema, StringComparison.OrdinalIgnoreCase) + targetPrefix;
        });
    }

    private static string ReplaceDbUserPrefix(string sql, string sourceDbUser, string targetSchema, string targetPrefix)
    {
        if (string.IsNullOrWhiteSpace(sourceDbUser))
            return sql;

        var pattern = $@"(?i)\b{Regex.Escape(sourceDbUser)}\s*\.\s*{Regex.Escape(sourceDbUser)}_";
        return Regex.Replace(sql, pattern, $"{targetSchema}.{targetPrefix}");
    }
}
