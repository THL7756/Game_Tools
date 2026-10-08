using TableTool.Core.Models;

// 用途：按客户端或服务器端过滤表字段，同时保留行来源坐标。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Parsing;

public static class TableDocumentFilter
{
    public static TableDocument ForTarget(TableDocument document, ExportTarget target)
    {
        if (target is not (ExportTarget.Client or ExportTarget.Server))
            throw new ArgumentException("A single export target is required.", nameof(target));
        var fields = document.Schema.Fields
            .Where(field => field.Target == FieldTarget.Both
                || field.Target == (target == ExportTarget.Client ? FieldTarget.Client : FieldTarget.Server)
                || (!document.Schema.IsSingleton
                    && field.Name.Equals(document.Schema.PrimaryKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var names = fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var rows = document.Rows
            .Select(row => new TableRow(
                row.SourceRow,
                row.IsTest,
                row.RawValues
                    .Where(pair => names.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                row.SourceName))
            .ToArray();

        var schema = document.Schema with
        {
            Fields = fields,
            ValidationFields = null
        };
        return document with { Schema = schema, Rows = rows };
    }
}
