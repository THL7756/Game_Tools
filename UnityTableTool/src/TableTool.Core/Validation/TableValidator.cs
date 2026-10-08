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

    public IReadOnlyList<ValidationIssue> Validate(TableDocument document)
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

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.Rows)
        {
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
                        row.IsTest ? ValidationSeverity.Error : ValidationSeverity.Error,
                        BuildValueMessage(field, raw, error),
                        document.SourceName,
                        row.SourceRow,
                        field.SourceColumn + 1,
                        field.Name,
                        raw,
                        "检查字段类型、分隔符和默认值。"));
                }
            }

            if (document.Schema.IsSingleton)
                continue;

            if (primaryKeyField is null)
                continue;

            if (row.IsTest)
                continue;

            var rawKey = row.RawValues.TryGetValue(document.Schema.PrimaryKey, out var suppliedKey) ? suppliedKey : null;
            var key = string.IsNullOrWhiteSpace(rawKey) ? primaryKeyField.DefaultValue : rawKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.PrimaryKeyMissing,
                    ValidationSeverity.Error,
                    $"主键“{document.Schema.PrimaryKey}”不能为空。",
                    document.SourceName,
                    row.SourceRow,
                    primaryKeyField.SourceColumn + 1,
                    document.Schema.PrimaryKey,
                    Suggestion: "为该数据行填写主键，或设置有效的默认值。"));
                continue;
            }
            if (!keys.Add(key))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.PrimaryKeyDuplicate,
                    ValidationSeverity.Error,
                    $"主键“{key}”重复。",
                    document.SourceName,
                    row.SourceRow,
                    primaryKeyField.SourceColumn + 1,
                    document.Schema.PrimaryKey,
                    key));
            }
        }

        return issues;
    }

    private static string BuildValueMessage(FieldSchema field, string? raw, FormatException error)
    {
        if (field.Type.IsEnum)
            return $"字段“{field.Name}”的值“{raw}”不在允许的枚举值中。";
        if (field.Type.Dimensions > 0 && error.Message.Contains("separator", StringComparison.OrdinalIgnoreCase))
            return $"字段“{field.Name}”的数组分隔符或层级不正确。";
        return $"字段“{field.Name}”的值“{raw}”不符合类型“{field.Type.DisplayText}”。";
    }
}
