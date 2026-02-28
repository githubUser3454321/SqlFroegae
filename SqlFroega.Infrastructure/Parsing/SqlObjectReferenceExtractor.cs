using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlFroega.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlFroega.Infrastructure.Parsing;

public sealed class SqlObjectReferenceExtractor
{
    public IReadOnlyList<DbObjectRef> Extract(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql ?? string.Empty);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0)
        {
            var first = errors[0];
            throw new InvalidOperationException($"SQL parse failed at line {first.Line}, col {first.Column}: {first.Message}");
        }

        var visitor = new ReferenceVisitor();
        fragment.Accept(visitor);
        return visitor.GetDistinct();
    }

    private sealed class ReferenceVisitor : TSqlFragmentVisitor
    {
        private readonly record struct TableRef(string Schema, string Table);

        private readonly List<DbObjectRef> _refs = new();
        private readonly Stack<SchemaObjectName?> _tableContext = new();
        private readonly Stack<Dictionary<string, TableRef>> _queryScope = new();

        public ReferenceVisitor()
        {
            _queryScope.Push(new Dictionary<string, TableRef>(StringComparer.OrdinalIgnoreCase));
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            _queryScope.Push(new Dictionary<string, TableRef>(StringComparer.OrdinalIgnoreCase));
            try
            {
                base.ExplicitVisit(node);
            }
            finally
            {
                _queryScope.Pop();
            }
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            Add(node.SchemaObject, DbObjectType.Table);
            RegisterTableLookupKeys(node.SchemaObject, node.Alias?.Value);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SchemaObjectFunctionTableReference node)
        {
            Add(node.SchemaObject, DbObjectType.Function);
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
            if (node.MultiPartIdentifier?.Identifiers is not { Count: >= 2 } ids)
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
            else
            {
                var qualifier = ids[^2].Value;
                var column = ids[^1].Value;

                if (!string.IsNullOrWhiteSpace(qualifier)
                    && !string.IsNullOrWhiteSpace(column)
                    && TryResolveQualifier(qualifier, out var tableRef))
                {
                    _refs.Add(new DbObjectRef($"{tableRef.Schema}.{tableRef.Table}.{column}", DbObjectType.Column));
                }
            }

            base.ExplicitVisit(node);
        }

        private void RegisterTableLookupKeys(SchemaObjectName? schemaObjectName, string? alias)
        {
            if (schemaObjectName is null || schemaObjectName.Identifiers.Count < 2 || _queryScope.Count == 0)
                return;

            var schema = schemaObjectName.Identifiers[^2].Value;
            var table = schemaObjectName.Identifiers[^1].Value;
            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
                return;

            var currentScope = _queryScope.Peek();
            var tableRef = new TableRef(schema, table);

            currentScope[table] = tableRef;
            currentScope[$"{schema}.{table}"] = tableRef;

            if (!string.IsNullOrWhiteSpace(alias))
                currentScope[alias] = tableRef;
        }

        private bool TryResolveQualifier(string qualifier, out TableRef tableRef)
        {
            foreach (var scope in _queryScope)
            {
                if (scope.TryGetValue(qualifier, out tableRef))
                    return true;
            }

            tableRef = default;
            return false;
        }

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
