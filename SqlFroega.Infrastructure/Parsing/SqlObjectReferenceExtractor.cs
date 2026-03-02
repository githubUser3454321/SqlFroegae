using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlFroega.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlFroega.Infrastructure.Parsing;

public sealed class SqlObjectReferenceExtractor
{
    public IReadOnlyList<DbObjectRef> Extract(string sql, IList<string>? diagnostics = null)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql ?? string.Empty);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0)
        {
            var first = errors[0];
            throw new InvalidOperationException($"SQL parse failed at line {first.Line}, col {first.Column}: {first.Message}");
        }

        var visitor = new ReferenceVisitor(diagnostics);
        fragment.Accept(visitor);
        return visitor.GetDistinct();
    }

    private sealed class ReferenceVisitor : TSqlFragmentVisitor
    {
        private readonly record struct TableRef(string Schema, string Table);

        private readonly List<DbObjectRef> _refs = new();
        private readonly Stack<SchemaObjectName?> _tableContext = new();
        private readonly Stack<Dictionary<string, TableRef>> _queryScope = new();
        private readonly Stack<HashSet<string>> _derivedAliasScope = new();
        private readonly Dictionary<string, TableRef> _cteSourceLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly IList<string>? _diagnostics;

        public ReferenceVisitor(IList<string>? diagnostics)
        {
            _diagnostics = diagnostics;
            _queryScope.Push(new Dictionary<string, TableRef>(StringComparer.OrdinalIgnoreCase));
            _derivedAliasScope.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            RegisterCteSources(node.WithCtesAndXmlNamespaces);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            _queryScope.Push(new Dictionary<string, TableRef>(StringComparer.OrdinalIgnoreCase));
            _derivedAliasScope.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            try
            {
                // ScriptDom can visit SELECT elements before FROM references.
                // Pre-visit FROM so alias-qualified columns in SELECT can be resolved.
                node.FromClause?.Accept(this);
                base.ExplicitVisit(node);
            }
            finally
            {
                _derivedAliasScope.Pop();
                _queryScope.Pop();
            }
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            Add(node.SchemaObject, DbObjectType.Table);
            RegisterTableLookupKeys(node.SchemaObject, node.Alias?.Value);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommonTableExpression node)
        {
            base.ExplicitVisit(node);

            if (_queryScope.Count == 0)
                return;

            var cteName = node.ExpressionName?.Value;
            if (string.IsNullOrWhiteSpace(cteName))
                return;

            if (TryInferSingleSourceTableRef(node.QueryExpression, out var tableRef) && IsConcreteTableRef(tableRef))
            {
                _cteSourceLookup[cteName] = tableRef;
                _queryScope.Peek()[cteName] = tableRef;
            }
        }

        public override void ExplicitVisit(WithCtesAndXmlNamespaces node)
        {
            RegisterCteSources(node);

            base.ExplicitVisit(node);
        }

        private void RegisterCteSources(WithCtesAndXmlNamespaces? node)
        {
            if (node is null || _queryScope.Count == 0)
                return;

            var currentScope = _queryScope.Peek();
            foreach (var cte in node.CommonTableExpressions)
            {
                var cteName = cte.ExpressionName?.Value;
                if (string.IsNullOrWhiteSpace(cteName))
                    continue;

                if (TryInferSingleSourceTableRef(cte.QueryExpression, out var tableRef) && IsConcreteTableRef(tableRef))
                {
                    _cteSourceLookup[cteName] = tableRef;
                    currentScope[cteName] = tableRef;
                    _diagnostics?.Add($"CTE source registered: {cteName} -> {tableRef.Schema}.{tableRef.Table}");
                }
                else
                {
                    _diagnostics?.Add($"CTE source unresolved: {cteName}");
                }
            }
        }

        public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
        {
            // Schema-scoped TVFs participate in FROM/APPLY like tables.
            // Record them as table references so alias-qualified projections
            // used in modern SQL patterns are treated consistently.
            Add(node.SchemaObject, DbObjectType.Table);
            RegisterDerivedAlias(node.Alias?.Value);
            _diagnostics?.Add($"Function table alias registered: {node.Alias?.Value ?? "<none>"}");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BuiltInFunctionTableReference node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            _diagnostics?.Add($"Built-in function table alias registered: {node.Alias?.Value ?? "<none>"}");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OpenJsonTableReference node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            _diagnostics?.Add($"OPENJSON alias registered: {node.Alias?.Value ?? "<none>"}");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(PivotedTableReference node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            _diagnostics?.Add($"PIVOT alias registered: {node.Alias?.Value ?? "<none>"}");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UnpivotedTableReference node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            _diagnostics?.Add($"UNPIVOT alias registered: {node.Alias?.Value ?? "<none>"}");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(TableReferenceWithAlias node)
        {
            RegisterDerivedAlias(node.Alias?.Value);
            base.ExplicitVisit(node);
        }


        public override void ExplicitVisit(CreateViewStatement node)
        {
            Add(node.SchemaObjectName, DbObjectType.View);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterViewStatement node)
        {
            Add(node.SchemaObjectName, DbObjectType.View);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            Add(node.ProcedureReference?.Name, DbObjectType.Procedure);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterProcedureStatement node)
        {
            Add(node.ProcedureReference?.Name, DbObjectType.Procedure);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateFunctionStatement node)
        {
            Add(node.Name, DbObjectType.Function);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterFunctionStatement node)
        {
            Add(node.Name, DbObjectType.Function);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CreateTableStatement node)
        {
            Add(node.SchemaObjectName, DbObjectType.Table);

            _tableContext.Push(node.SchemaObjectName);
            try
            {
                base.ExplicitVisit(node);
            }
            finally
            {
                _tableContext.Pop();
            }
        }

        public override void ExplicitVisit(AlterTableStatement node)
        {
            Add(node.SchemaObjectName, DbObjectType.Table);

            _tableContext.Push(node.SchemaObjectName);
            try
            {
                base.ExplicitVisit(node);
            }
            finally
            {
                _tableContext.Pop();
            }
        }

        public override void ExplicitVisit(ColumnDefinition node)
        {
            if (node.ColumnIdentifier is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableName = _tableContext.Count > 0 ? _tableContext.Peek() : null;

            if (tableName is not null && tableName.Identifiers.Count >= 2)
            {
                var schema = tableName.Identifiers[^2].Value;
                var table = tableName.Identifiers[^1].Value;
                var column = node.ColumnIdentifier.Value;

                if (!string.IsNullOrWhiteSpace(schema) && !string.IsNullOrWhiteSpace(table) && !string.IsNullOrWhiteSpace(column))
                    _refs.Add(new DbObjectRef($"{schema}.{table}.{column}", DbObjectType.Column));
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            var ids = node.MultiPartIdentifier?.Identifiers;
            if (ids is null || ids.Count == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (ids.Count >= 3)
            {
                var schema = ids[^3].Value;
                var table = ids[^2].Value;
                var column = ids[^1].Value;

                if (!string.IsNullOrWhiteSpace(schema) && !string.IsNullOrWhiteSpace(table) && !string.IsNullOrWhiteSpace(column))
                    _refs.Add(new DbObjectRef($"{schema}.{table}.{column}", DbObjectType.Column));
            }
            else if (ids.Count == 2)
            {
                var qualifier = ids[^2].Value;
                var column = ids[^1].Value;

                if (!string.IsNullOrWhiteSpace(qualifier)
                    && !string.IsNullOrWhiteSpace(column)
                    && TryResolveQualifier(qualifier, out var tableRef))
                {
                    if (TryResolveConcreteTableRef(tableRef, out var concreteTableRef))
                        tableRef = concreteTableRef;

                    if (!IsConcreteTableRef(tableRef)
                        && !string.IsNullOrWhiteSpace(tableRef.Table)
                        && _cteSourceLookup.TryGetValue(tableRef.Table, out var cteSourceRef)
                        && IsConcreteTableRef(cteSourceRef))
                    {
                        tableRef = cteSourceRef;
                    }

                    if (IsConcreteTableRef(tableRef))
                    {
                        _refs.Add(new DbObjectRef($"{tableRef.Schema}.{tableRef.Table}.{column}", DbObjectType.Column));
                    }
                    else
                    {
                        _diagnostics?.Add($"Column unresolved to concrete table: {qualifier}.{column} -> {tableRef.Schema}.{tableRef.Table}");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(qualifier)
                         && !string.IsNullOrWhiteSpace(column)
                         && !IsKnownDerivedAlias(qualifier)
                         && !IsAllowedUnresolvedQualifier(qualifier))
                {
                    _diagnostics?.Add($"Unknown qualifier: {qualifier}.{column}; ctes=[{string.Join(",", _cteSourceLookup.Keys)}]");
                    throw new InvalidOperationException($"Unresolved column qualifier '{qualifier}' in '{qualifier}.{column}'.");
                }
            }
            else
            {
                var column = ids[^1].Value;
                if (!string.IsNullOrWhiteSpace(column))
                {
                    foreach (var tableRef in GetResolvableTables())
                    {
                        if (IsConcreteTableRef(tableRef))
                            _refs.Add(new DbObjectRef($"{tableRef.Schema}.{tableRef.Table}.{column}", DbObjectType.Column));
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private void RegisterTableLookupKeys(SchemaObjectName? schemaObjectName, string? alias)
        {
            if (schemaObjectName is null || schemaObjectName.Identifiers.Count == 0 || _queryScope.Count == 0)
                return;

            var table = schemaObjectName.Identifiers[^1].Value;
            var schema = schemaObjectName.Identifiers.Count >= 2
                ? schemaObjectName.Identifiers[^2].Value
                : string.Empty;

            if (string.IsNullOrWhiteSpace(table))
                return;

            var currentScope = _queryScope.Peek();
            var tableRef = new TableRef(schema, table);
            if (string.IsNullOrWhiteSpace(schema))
            {
                if (TryResolveConcreteTableRef(tableRef, out var concreteTableRef))
                    tableRef = concreteTableRef;
            }

            currentScope[table] = tableRef;
            if (!string.IsNullOrWhiteSpace(schema))
                currentScope[$"{schema}.{table}"] = tableRef;

            if (!string.IsNullOrWhiteSpace(alias))
                currentScope[alias] = tableRef;
        }

        private bool TryResolveQualifier(string qualifier, out TableRef tableRef, bool requireConcrete = false)
        {
            TableRef? firstMatch = null;

            foreach (var scope in _queryScope)
            {
                if (!scope.TryGetValue(qualifier, out tableRef))
                    continue;

                firstMatch ??= tableRef;

                if (IsConcreteTableRef(tableRef))
                    return true;
            }

            if (_cteSourceLookup.TryGetValue(qualifier, out tableRef))
            {
                firstMatch ??= tableRef;

                if (IsConcreteTableRef(tableRef))
                    return true;
            }

            if (!requireConcrete && firstMatch is TableRef match)
            {
                tableRef = match;
                return true;
            }

            tableRef = default;
            return false;
        }

        private bool TryResolveConcreteTableRef(TableRef tableRef, out TableRef concreteTableRef)
        {
            if (IsConcreteTableRef(tableRef))
            {
                concreteTableRef = tableRef;
                return true;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nextQualifier = tableRef.Table;

            while (!string.IsNullOrWhiteSpace(nextQualifier) && visited.Add(nextQualifier))
            {
                if (!TryResolveQualifier(nextQualifier, out var resolvedRef))
                    break;

                if (IsConcreteTableRef(resolvedRef))
                {
                    concreteTableRef = resolvedRef;
                    return true;
                }

                nextQualifier = resolvedRef.Table;
            }

            concreteTableRef = default;
            return false;
        }

        private void RegisterDerivedAlias(string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || _derivedAliasScope.Count == 0)
                return;

            _derivedAliasScope.Peek().Add(alias);
        }

        private bool IsKnownDerivedAlias(string qualifier)
        {
            foreach (var scope in _derivedAliasScope)
            {
                if (scope.Contains(qualifier))
                    return true;
            }

            return false;
        }

        private static bool TryInferSingleSourceTableRef(QueryExpression? queryExpression, out TableRef tableRef)
        {
            if (queryExpression is QuerySpecification querySpec && querySpec.FromClause is not null)
            {
                var firstTable = querySpec.FromClause.TableReferences.OfType<NamedTableReference>().FirstOrDefault();
                if (firstTable?.SchemaObject is not null && firstTable.SchemaObject.Identifiers.Count >= 2)
                {
                    var schema = firstTable.SchemaObject.Identifiers[^2].Value;
                    var table = firstTable.SchemaObject.Identifiers[^1].Value;

                    if (!string.IsNullOrWhiteSpace(schema) && !string.IsNullOrWhiteSpace(table))
                    {
                        tableRef = new TableRef(schema, table);
                        return true;
                    }
                }
            }

            tableRef = default;
            return false;
        }

        private IEnumerable<TableRef> GetResolvableTables()
        {
            if (_queryScope.Count == 0)
                return Enumerable.Empty<TableRef>();

            return _queryScope.Peek().Values.Distinct();
        }

        private static bool IsConcreteTableRef(TableRef tableRef)
            => !string.IsNullOrWhiteSpace(tableRef.Schema) && !string.IsNullOrWhiteSpace(tableRef.Table);

        private static bool IsAllowedUnresolvedQualifier(string qualifier)
            => qualifier.StartsWith("@", StringComparison.Ordinal) || qualifier.Equals("inserted", StringComparison.OrdinalIgnoreCase) || qualifier.Equals("deleted", StringComparison.OrdinalIgnoreCase);

        private void Add(SchemaObjectName? name, DbObjectType type)
        {
            if (name is null || name.Identifiers.Count < 2)
                return;

            var schema = name.Identifiers[^2].Value;
            var obj = name.Identifiers[^1].Value;

            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(obj))
                return;

            _refs.Add(new DbObjectRef($"{schema}.{obj}", type));
        }

        public IReadOnlyList<DbObjectRef> GetDistinct()
            => _refs
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var prioritized = g.OrderBy(x => x.Type == DbObjectType.Unknown ? 1 : 0).First();
                    return new DbObjectRef(g.First().Name, prioritized.Type);
                })
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
