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
        private readonly List<DbObjectRef> _refs = new();

        public override void ExplicitVisit(NamedTableReference node)
        {
            Add(node.SchemaObject, DbObjectType.Table);
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
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterTableStatement node)
        {
            Add(node.SchemaObjectName, DbObjectType.Table);
            base.ExplicitVisit(node);
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
