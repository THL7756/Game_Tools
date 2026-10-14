// 用途：按逐列 schema 检查合并同名配置表，并将分表默认值校准到输出数据。
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
            if (shards.Length < 2)
                continue;

            var first = shards[0];
            foreach (var shard in shards.Skip(1))
            {
                if (first.Schema.IsSingleton != shard.Schema.IsSingleton)
                {
                    AddConflict(first, null, 1, 1, $"同名表“{first.Schema.Name}”的单例/普通表定义不一致。", issues);
                    AddConflict(shard, null, 1, 1, $"同名表“{shard.Schema.Name}”的单例/普通表定义不一致。", issues);
                }

            }

            CompareFields(shards, isTest: false, "正式字段", issues);
            CompareFields(shards, isTest: true, "测试字段", issues);
            if (first.Schema.IsSingleton)
                CompareSingletonValues(shards, issues);
        }

        return issues.Distinct().ToArray();
    }

    public static IReadOnlyList<TableDocument> Merge(IEnumerable<TableDocument> documents)
    {
        var merged = new List<TableDocument>();
        foreach (var group in documents.GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase))
        {
            var shards = group.ToArray();
            var conflicts = FindSchemaConflicts(shards);
            if (conflicts.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
                throw new FormatException(conflicts[0].Message);

            merged.Add(MergeGroup(shards));
        }

        return merged;
    }

    private static TableDocument MergeGroup(IReadOnlyList<TableDocument> shards)
    {
        var first = shards[0];
        var fields = BuildCanonicalFields(shards, isTest: false);
        var validationFields = BuildCanonicalFields(shards, isTest: true);
        var primaryKey = fields.FirstOrDefault(field =>
                field.Name.Equals("id", StringComparison.OrdinalIgnoreCase))?.Name
            ?? first.Schema.PrimaryKey;
        var schema = new TableSchema(
            first.Schema.Name,
            fields,
            primaryKey,
            first.Schema.IsSingleton,
            validationFields);
        var sourceName = string.Join(
            " + ",
            shards.Select(shard => shard.SourceName).Distinct(StringComparer.OrdinalIgnoreCase));

        if (first.Schema.IsSingleton)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var rawCells = new Dictionary<int, string?>();
            var rawFieldValues = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var shard in shards)
            foreach (var row in shard.Rows.Where(row => !row.IsTest))
            foreach (var field in fields)
            {
                var value = GetEffectiveValue(shard, row, field);
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (!values.TryGetValue(field.Name, out var current) || string.IsNullOrWhiteSpace(current))
                    values[field.Name] = value;
                rawCells[field.SourceColumn] = values[field.Name];
                rawFieldValues[field.Identity] = values[field.Name];
            }

            var rowSource = first.Rows.FirstOrDefault(row => !row.IsTest);
            var combinedRow = new TableRow(
                rowSource?.SourceRow ?? 1,
                false,
                values,
                rowSource?.SourceName ?? first.SourceName,
                rawCells,
                rawFieldValues);
            return new TableDocument(sourceName, schema, [combinedRow], shards.SelectMany(shard => shard.ParseIssues ?? []).ToArray());
        }

        var rows = shards
            .SelectMany(shard => shard.Rows.Select(row => NormalizeRow(shard, row, fields, validationFields)))
            .ToArray();
        return new TableDocument(sourceName, schema, rows, shards.SelectMany(shard => shard.ParseIssues ?? []).ToArray());
    }

    private static TableRow NormalizeRow(
        TableDocument shard,
        TableRow row,
        IReadOnlyList<FieldSchema> fields,
        IReadOnlyList<FieldSchema> validationFields)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var rawCells = new Dictionary<int, string?>();
        var rawFieldValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in fields.Concat(validationFields))
        {
            var value = GetEffectiveValue(shard, row, field);
            values.TryAdd(field.Name, value);
            rawCells[field.SourceColumn] = value;
            rawFieldValues[field.Identity] = value;
        }

        return row with
        {
            RawValues = values,
            RawCells = rawCells,
            RawFieldValues = rawFieldValues,
            SourceName = row.SourceName ?? shard.SourceName
        };
    }

    private static string? GetEffectiveValue(TableDocument shard, TableRow row, FieldSchema canonicalField)
    {
        var sourceField = shard.Schema.AllFields.FirstOrDefault(field =>
            field.IsTest == canonicalField.IsTest
            && field.Name.Equals(canonicalField.Name, StringComparison.OrdinalIgnoreCase));
        var raw = sourceField is null
            ? null
            : row.GetValue(sourceField);
        if (!string.IsNullOrWhiteSpace(raw))
            return raw.Trim();

        return sourceField?.DefaultValue ?? canonicalField.DefaultValue;
    }

    private static IReadOnlyList<FieldSchema> BuildCanonicalFields(
        IReadOnlyList<TableDocument> shards,
        bool isTest)
    {
        return shards[0].Schema.AllFields
            .Where(field => field.IsTest == isTest)
            .ToArray();
    }

    private static void CompareFields(
        IReadOnlyList<TableDocument> shards,
        bool isTest,
        string groupName,
        ICollection<ValidationIssue> issues)
    {
        var first = shards[0].Schema.AllFields.Where(field => field.IsTest == isTest).ToArray();
        foreach (var shard in shards.Skip(1))
        {
            var current = shard.Schema.AllFields.Where(field => field.IsTest == isTest).ToArray();
            var count = Math.Max(first.Length, current.Length);
            for (var index = 0; index < count; index++)
            {
                var left = index < first.Length ? first[index] : null;
                var right = index < current.Length ? current[index] : null;
                if (left is null || right is null)
                {
                    if (left is not null)
                        AddConflict(shards[0], left, left.NameRow, left.SourceColumn + 1,
                            $"同名表的{groupName}数量或顺序不一致。", issues);
                    if (right is not null)
                        AddConflict(shard, right, right.NameRow, right.SourceColumn + 1,
                            $"同名表的{groupName}数量或顺序不一致。", issues);
                    continue;
                }

                if (left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase)
                    && HasSameDefinition(left, right))
                    continue;

                AddConflict(
                    shards[0],
                    left,
                    ConflictRow(left, right),
                    left.SourceColumn + 1,
                    $"同名表的{groupName}字段名称、顺序、类型、默认值或端标记不一致。",
                    issues);
                AddConflict(
                    shard,
                    right,
                    ConflictRow(right, left),
                    right.SourceColumn + 1,
                    $"同名表的{groupName}字段名称、顺序、类型、默认值或端标记不一致。",
                    issues);
            }
        }
    }

    private static void CompareSingletonValues(
        IReadOnlyList<TableDocument> shards,
        ICollection<ValidationIssue> issues)
    {
        var fields = BuildCanonicalFields(shards, isTest: false);
        foreach (var field in fields)
        {
            string? value = null;
            TableDocument? valueDocument = null;
            foreach (var shard in shards)
            {
                var row = shard.Rows.FirstOrDefault(item => !item.IsTest);
                if (row is null)
                    continue;
                var current = GetEffectiveValue(shard, row, field);
                if (string.IsNullOrWhiteSpace(current))
                    continue;
                if (value is null)
                {
                    value = current;
                    valueDocument = shard;
                    continue;
                }
                if (string.Equals(value, current, StringComparison.Ordinal))
                    continue;

                AddConflict(
                    valueDocument!,
                    field,
                    field.NameRow,
                    field.SourceColumn + 1,
                    $"同名单例表字段“{field.Name}”的非空值不一致。",
                    issues);
                AddConflict(
                    shard,
                    field,
                    field.NameRow,
                    field.SourceColumn + 1,
                    $"同名单例表字段“{field.Name}”的非空值不一致。",
                    issues);
            }
        }
    }

    private static bool HasSameDefinition(FieldSchema left, FieldSchema right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
        && HasSameType(left.Type, right.Type)
        && left.Target == right.Target
        && string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal);

    private static int ConflictRow(FieldSchema left, FieldSchema right)
    {
        if (!left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase))
            return left.NameRow;
        if (!HasSameType(left.Type, right.Type))
            return left.TypeRow;
        if (!string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal))
            return left.DefaultRow;
        return left.TargetRow;
    }

    private static bool HasSameType(TypeDescriptor left, TypeDescriptor right) =>
        string.Equals(left.BaseType, right.BaseType, StringComparison.OrdinalIgnoreCase)
        && left.Dimensions == right.Dimensions
        && left.IsValid == right.IsValid
        && (left.EnumValues ?? []).SequenceEqual(right.EnumValues ?? [], StringComparer.Ordinal);

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
            Suggestion: "请检查同名表对应字段的名称、类型和端标记。"));
}
