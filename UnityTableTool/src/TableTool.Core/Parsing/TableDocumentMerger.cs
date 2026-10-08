// 用途：在打表导出阶段合并同名、同结构的表文档。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Validation;

namespace TableTool.Core.Parsing;

public static class TableDocumentMerger
{
    public static IReadOnlyList<ValidationIssue> FindSchemaConflicts(IEnumerable<TableDocument> documents)
    {
        var issues = new List<ValidationIssue>();
        foreach (var group in documents.GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase))
        {
            var shards = group.ToArray();
            for (var index = 1; index < shards.Length; index++)
                ComparePair(shards[0], shards[index], issues);
        }

        return issues;
    }

    public static IReadOnlyList<TableDocument> Merge(IEnumerable<TableDocument> documents)
    {
        var ordered = documents.ToArray();
        var merged = new List<TableDocument>();
        foreach (var group in ordered.GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase))
        {
            var shards = group.ToArray();
            if (shards.Length == 1)
            {
                merged.Add(shards[0]);
                continue;
            }

            var schema = shards[0].Schema;
            foreach (var shard in shards.Skip(1))
            {
                if (!HasSameSchema(schema, shard.Schema))
                    throw new FormatException(
                        $"Table '{group.Key}' has incompatible schemas across sheets or files: '{shards[0].SourceName}' and '{shard.SourceName}'.");
            }

            var rows = shards.SelectMany(shard => shard.Rows).ToArray();
            var sourceName = string.Join(" + ", shards.Select(shard => shard.SourceName).Distinct(StringComparer.OrdinalIgnoreCase));
            merged.Add(new TableDocument(sourceName, schema, rows, shards.SelectMany(shard => shard.ParseIssues ?? []).ToArray()));
        }

        return merged;
    }

    private static bool HasSameSchema(TableSchema left, TableSchema right) =>
        left.IsSingleton == right.IsSingleton
        && string.Equals(left.PrimaryKey, right.PrimaryKey, StringComparison.OrdinalIgnoreCase)
        && HasSameFields(left.Fields, right.Fields)
        && HasSameFields(left.ValidationFields ?? [], right.ValidationFields ?? []);

    private static bool HasSameFields(
        IReadOnlyList<FieldSchema> left,
        IReadOnlyList<FieldSchema> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair => HasSameField(pair.First, pair.Second));

    private static bool HasSameField(FieldSchema left, FieldSchema right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal)
        && HasSameType(left.Type, right.Type)
        && left.Target == right.Target
        && string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal);

    private static bool HasSameType(TypeDescriptor left, TypeDescriptor right) =>
        string.Equals(left.BaseType, right.BaseType, StringComparison.OrdinalIgnoreCase)
        && left.Dimensions == right.Dimensions
        && left.IsValid == right.IsValid
        && string.Equals(left.RawText, right.RawText, StringComparison.Ordinal)
        && (left.EnumValues ?? []).SequenceEqual(right.EnumValues ?? [], StringComparer.Ordinal);

    private static void ComparePair(TableDocument left, TableDocument right, ICollection<ValidationIssue> issues)
    {
        if (left.Schema.IsSingleton != right.Schema.IsSingleton)
        {
            AddConflict(left, null, 1, 1, $"同名表“{left.Schema.Name}”的单例/普通表定义不一致。", issues);
            AddConflict(right, null, 1, 1, $"同名表“{right.Schema.Name}”的单例/普通表定义不一致。", issues);
        }

        CompareFields(left, right, left.Schema.Fields, right.Schema.Fields, "正式字段", issues);
        CompareFields(left, right, left.Schema.ValidationFields ?? [], right.Schema.ValidationFields ?? [], "测试字段", issues);
    }

    private static void CompareFields(
        TableDocument leftDocument,
        TableDocument rightDocument,
        IReadOnlyList<FieldSchema> left,
        IReadOnlyList<FieldSchema> right,
        string groupName,
        ICollection<ValidationIssue> issues)
    {
        var count = Math.Max(left.Count, right.Count);
        for (var index = 0; index < count; index++)
        {
            var leftField = index < left.Count ? left[index] : null;
            var rightField = index < right.Count ? right[index] : null;
            if (leftField is null || rightField is null)
            {
                var present = leftField ?? rightField!;
                AddConflict(leftField is null ? rightDocument : leftDocument, present, present.NameRow, present.SourceColumn + 1, $"同名表的{groupName}数量或顺序不一致，字段“{present.Name}”位置不同。", issues);
                AddConflict(leftField is null ? leftDocument : rightDocument, null, 1, 1, $"同名表缺少对方的{groupName}“{present.Name}”。", issues);
                continue;
            }

            if (!HasSameField(leftField, rightField))
            {
                AddConflict(leftDocument, leftField, ConflictRow(leftField, rightField), ConflictColumn(leftField), $"同名表的{groupName}“{leftField.Name}”类型、默认值、端标记或位置不一致。", issues);
                AddConflict(rightDocument, rightField, ConflictRow(rightField, leftField), ConflictColumn(rightField), $"同名表的{groupName}“{rightField.Name}”类型、默认值、端标记或位置不一致。", issues);
            }
        }
    }

    private static int ConflictRow(FieldSchema left, FieldSchema right) =>
        !left.Type.Equals(right.Type) ? left.TypeRow
        : !string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal) ? left.DefaultRow
        : left.NameRow;

    private static int ConflictColumn(FieldSchema field) => field.SourceColumn + 1;

    private static void AddConflict(
        TableDocument document,
        FieldSchema? field,
        int row,
        int column,
        string message,
        ICollection<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            ErrorCodes.TableMergeInvalid,
            ValidationSeverity.Error,
            message,
            document.SourceName,
            row,
            column,
            field?.Name,
            Suggestion: "请让同名表对应字段的名称、类型、默认值、端标记和测试列保持一致。"));
}
