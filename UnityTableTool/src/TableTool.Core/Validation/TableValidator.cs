// 用途：验证字段类型、默认值、主键和表引用前提。
// 用途：验证字段类型、默认值、主键、枚举、数组和数据行。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Core.Validation;

public sealed class TableValidator
{
    private readonly ArraySeparatorOptions separators;

    public TableValidator(ArraySeparatorOptions? separators = null)
    {
        this.separators = separators ?? ArraySeparatorOptions.Default;
    }

    public IReadOnlyList<ValidationIssue> Validate(TableDocument document) =>
        Validate([document]);

    public IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<TableDocument> documents)
    {
        var issues = new List<ValidationIssue>();
        foreach (var document in documents)
            issues.AddRange(ValidateDocument(document));
        issues.AddRange(FindPrimaryKeyDuplicates(documents));
        return issues;
    }

    private IReadOnlyList<ValidationIssue> ValidateDocument(TableDocument document)
    {
        var issues = new List<ValidationIssue>();
        var fields = document.Schema.Fields;
        var primaryKeyField = fields.FirstOrDefault(field =>
            string.Equals(field.Name, document.Schema.PrimaryKey, StringComparison.OrdinalIgnoreCase));
        if (!document.Schema.IsSingleton && primaryKeyField is null)
        {
            issues.Add(new ValidationIssue(
                ErrorCodes.PrimaryKeyMissing,
                ValidationSeverity.Error,
                $"Primary key '{document.Schema.PrimaryKey}' is missing.",
                document.SourceName));
        }

        var invalidDefaults = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in document.Schema.AllFields.Where(field => !string.IsNullOrWhiteSpace(field.DefaultValue)))
        {
            try
            {
                ValueParser.Parse(field.DefaultValue!, field.Type, separators);
            }
            catch (FormatException)
            {
                invalidDefaults.Add(field.Name);
                issues.Add(new ValidationIssue(
                    ErrorCodes.FieldDefaultInvalid,
                    ValidationSeverity.Error,
                    $"字段“{field.Name}”的默认值不符合类型“{field.Type.DisplayText}”。",
                    document.SourceName,
                    field.DefaultRow,
                    FieldName: field.Name,
                    SourceColumn: field.SourceColumn + 1,
                    Suggestion: "请按字段类型填写默认值。"));
            }
        }

        foreach (var row in document.Rows)
        {
            var rowSource = row.SourceName ?? document.SourceName;
            foreach (var field in document.Schema.AllFields)
            {
                var raw = row.RawValues.TryGetValue(field.Name, out var value) ? value : null;
                var text = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
                if (text is null)
                    continue;

                try
                {
                    ValueParser.Parse(text, field.Type, separators);
                }
                catch (FormatException error)
                {
                    if (string.IsNullOrWhiteSpace(raw) && invalidDefaults.Contains(field.Name))
                        continue;

                    issues.Add(new ValidationIssue(
                        field.Type.Dimensions > 0 && error.Message.Contains("separator", StringComparison.OrdinalIgnoreCase)
                            ? ErrorCodes.FieldArraySeparatorInvalid
                            : field.Type.IsEnum ? ErrorCodes.EnumValueInvalid
                            : string.IsNullOrWhiteSpace(raw) ? ErrorCodes.FieldDefaultInvalid : ErrorCodes.FieldTypeUnknown,
                        ValidationSeverity.Error,
                        BuildValueMessage(field, raw, error),
                        rowSource,
                        document.Schema.IsSingleton ? field.NameRow : row.SourceRow,
                        field.SourceColumn + 1,
                        field.Name,
                        raw,
                        "检查字段类型、分隔符和默认值。"));
                }
            }

            if (document.Schema.IsSingleton || row.IsTest || primaryKeyField is null)
                continue;

            var rawKey = row.RawValues.TryGetValue(document.Schema.PrimaryKey, out var suppliedKey) ? suppliedKey : null;
            var key = (string.IsNullOrWhiteSpace(rawKey) ? primaryKeyField.DefaultValue : rawKey)?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.PrimaryKeyMissing,
                    ValidationSeverity.Error,
                    $"主键“{document.Schema.PrimaryKey}”不能为空。",
                    rowSource,
                    row.SourceRow,
                    primaryKeyField.SourceColumn + 1,
                    document.Schema.PrimaryKey,
                    Suggestion: "为该数据行填写主键，或设置有效的默认值。"));
            }
        }

        return issues;
    }

    private static IEnumerable<ValidationIssue> FindPrimaryKeyDuplicates(
        IReadOnlyList<TableDocument> documents)
    {
        var occurrences = documents
            .Where(document => !document.Schema.IsSingleton)
            .GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.SelectMany(document =>
            {
                var field = document.Schema.Fields.FirstOrDefault(candidate =>
                    candidate.Name.Equals(document.Schema.PrimaryKey, StringComparison.OrdinalIgnoreCase));
                return field is null
                    ? []
                    : document.Rows
                        .Where(row => !row.IsTest)
                        .Select(row =>
                        {
                            var raw = row.RawValues.GetValueOrDefault(field.Name);
                            var key = (string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw)?.Trim();
                            return key is null
                                ? null
                                : new PrimaryKeyOccurrence(
                                    key,
                                    row.SourceName ?? document.SourceName,
                                    row.SourceRow,
                                    field.SourceColumn + 1,
                                    document.Schema.PrimaryKey);
                        })
                        .Where(item => item is not null)
                        .Select(item => item!);
            }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToArray();

        return occurrences
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group =>
            {
                var locations = group
                    .Select(FormatLocation)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return group.Select(item => new ValidationIssue(
                    ErrorCodes.PrimaryKeyDuplicate,
                    ValidationSeverity.Error,
                    $"主键“{item.Key}”重复：{string.Join("、", locations)}。",
                    item.SourceName,
                    item.SourceRow,
                    item.SourceColumn,
                    item.FieldName,
                    item.Key,
                    "请修改重复的主键值。"));
            });
    }

    private static string FormatLocation(PrimaryKeyOccurrence occurrence)
    {
        var column = string.Empty;
        var value = occurrence.SourceColumn;
        while (value > 0)
        {
            value--;
            column = (char)('A' + value % 26) + column;
            value /= 26;
        }
        return $"{occurrence.SourceName}!{column}{occurrence.SourceRow}";
    }

    private static string BuildValueMessage(FieldSchema field, string? raw, FormatException error)
    {
        if (field.Type.IsEnum)
            return $"字段“{field.Name}”的值“{raw}”不在允许的枚举值中。";
        if (field.Type.Dimensions > 0 && error.Message.Contains("separator", StringComparison.OrdinalIgnoreCase))
            return $"字段“{field.Name}”的数组分隔符或层级不正确。";
        return $"字段“{field.Name}”的值“{raw}”不符合类型“{field.Type.DisplayText}”。";
    }

    private sealed record PrimaryKeyOccurrence(
        string Key,
        string SourceName,
        int SourceRow,
        int SourceColumn,
        string FieldName);
}
