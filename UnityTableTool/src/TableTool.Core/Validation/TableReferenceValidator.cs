// 用途：校验 item_id 字段对应的逻辑表、主键类型和主键值。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.Globalization;
using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Core.Validation;

public static class TableReferenceValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(
        IReadOnlyList<TableDocument> allDocuments,
        IReadOnlyList<TableDocument> selectedDocuments,
        ArraySeparatorOptions? separators = null)
    {
        separators ??= ArraySeparatorOptions.Default;
        var tables = allDocuments
            .GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var indexes = tables.ToDictionary(
            pair => pair.Key,
            pair => BuildKeyIndex(pair.Value, separators),
            StringComparer.OrdinalIgnoreCase);
        var issues = new List<ValidationIssue>();

        foreach (var document in selectedDocuments)
        foreach (var field in document.Schema.AllFields.Where(IsReferenceField))
        {
            var referencedTableName = GetReferencedTableName(field.Name);
            if (!tables.TryGetValue(referencedTableName, out var referencedDocuments))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.TableReferenceMissing,
                    ValidationSeverity.Error,
                    $"引用表“{referencedTableName}”未找到。",
                    document.SourceName,
                    SourceRow: field.NameRow,
                    FieldName: field.Name,
                    SourceColumn: field.SourceColumn + 1,
                    Key: referencedTableName,
                    Suggestion: "补全引用表，或移除对应引用字段。"));
                continue;
            }

            var primaryKey = GetLogicalPrimaryKey(referencedDocuments);
            var targetField = referencedDocuments
                .Select(item => item.Schema.Fields.FirstOrDefault(candidate =>
                    candidate.Name.Equals(primaryKey, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(candidate => candidate is not null);
            if (targetField is null)
                continue;

            if (!HasCompatibleType(field.Type, targetField.Type))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.TableReferenceTypeMismatch,
                    ValidationSeverity.Error,
                    $"引用字段“{field.Name}”类型“{field.Type.DisplayText}”与目标主键类型“{targetField.Type.DisplayText}”不一致。",
                    document.SourceName,
                    field.TypeRow,
                    field.SourceColumn + 1,
                    field.Name,
                    referencedTableName,
                    "请让引用字段和目标表主键使用相同类型。"));
                continue;
            }

            if (!indexes.TryGetValue(referencedTableName, out var keyIndex))
                continue;
            foreach (var row in document.Rows)
            {
                var raw = row.RawValues.GetValueOrDefault(field.Name);
                var value = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var normalized = NormalizeValue(value, field.Type, separators);
                if (normalized is null || keyIndex.Contains(normalized))
                    continue;

                issues.Add(new ValidationIssue(
                    ErrorCodes.TableReferenceKeyMissing,
                    ValidationSeverity.Error,
                    $"字段“{field.Name}”引用的主键值“{value}”在表“{referencedTableName}”中不存在。",
                    row.SourceName ?? document.SourceName,
                    document.Schema.IsSingleton ? field.NameRow : row.SourceRow,
                    field.SourceColumn + 1,
                    field.Name,
                    value,
                    "请填写目标表中存在的主键值。"));
            }
        }

        return issues.Distinct().ToArray();
    }

    private static HashSet<string> BuildKeyIndex(
        IReadOnlyList<TableDocument> documents,
        ArraySeparatorOptions separators)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (documents.Count == 0)
            return keys;

        var primaryKey = GetLogicalPrimaryKey(documents);
        foreach (var document in documents)
        {
            var field = document.Schema.Fields.FirstOrDefault(candidate =>
                candidate.Name.Equals(primaryKey, StringComparison.OrdinalIgnoreCase));
            if (field is null)
                continue;

            foreach (var row in document.Rows.Where(row => !row.IsTest))
            {
                var raw = row.RawValues.GetValueOrDefault(field.Name);
                var value = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                var normalized = NormalizeValue(value, field.Type, separators);
                if (normalized is not null)
                    keys.Add(normalized);
            }
        }

        return keys;
    }

    private static bool IsReferenceField(FieldSchema field) =>
        field.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
        && field.Name.Length > 3;

    private static string GetLogicalPrimaryKey(IReadOnlyList<TableDocument> documents) =>
        documents.SelectMany(document => document.Schema.Fields)
            .FirstOrDefault(field => field.Name.Equals("id", StringComparison.OrdinalIgnoreCase))?.Name
        ?? documents[0].Schema.PrimaryKey;

    private static string GetReferencedTableName(string fieldName) => fieldName[..^3];

    private static bool HasCompatibleType(TypeDescriptor left, TypeDescriptor right) =>
        left.IsValid
        && right.IsValid
        && string.Equals(left.BaseType, right.BaseType, StringComparison.OrdinalIgnoreCase)
        && left.Dimensions == right.Dimensions
        && (left.EnumValues ?? []).SequenceEqual(right.EnumValues ?? [], StringComparer.Ordinal);

    private static string? NormalizeValue(
        string value,
        TypeDescriptor type,
        ArraySeparatorOptions separators)
    {
        try
        {
            var parsed = ValueParser.Parse(value, type, separators);
            return parsed switch
            {
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => Convert.ToString(parsed, CultureInfo.InvariantCulture)
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
