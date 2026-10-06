// 用途：验证字段类型、默认值、主键和表引用前提。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Core.Validation;

public sealed class TableValidator
{
    public IReadOnlyList<ValidationIssue> Validate(TableDocument document)
    {
        var issues = new List<ValidationIssue>();
        var fields = document.Schema.Fields;
        var formalRows = document.Rows.Where(row => !row.IsTest).ToArray();
        if (document.Schema.IsSingleton && formalRows.Length != 1)
        {
            issues.Add(new ValidationIssue(
                ErrorCodes.SingletonRowCountInvalid,
                ValidationSeverity.Error,
                $"Singleton table must contain exactly one formal data row, found {formalRows.Length}.",
                document.SourceName,
                Suggestion: "删除多余数据行，或去掉 type:single 元数据。"));
        }

        var primaryKeyField = fields.FirstOrDefault(field => field.Name == document.Schema.PrimaryKey);
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
                ValueParser.Parse(field.DefaultValue!, field.Type);
            }
            catch (FormatException error)
            {
                invalidDefaults.Add(field.Name);
                issues.Add(new ValidationIssue(
                    ErrorCodes.FieldDefaultInvalid,
                    ValidationSeverity.Error,
                    error.Message,
                    document.SourceName,
                    FieldName: field.Name,
                    SourceColumn: field.SourceColumn + 1,
                    Suggestion: "检查默认值是否符合字段类型。"));
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
                    ValueParser.Parse(text, field.Type);
                }
                catch (FormatException error)
                {
                    if (string.IsNullOrWhiteSpace(raw) && invalidDefaults.Contains(field.Name))
                        continue;

                    issues.Add(new ValidationIssue(
                        field.Type.Dimensions > 0 && error.Message.Contains("separator", StringComparison.OrdinalIgnoreCase)
                            ? ErrorCodes.FieldArraySeparatorInvalid
                            : string.IsNullOrWhiteSpace(raw) ? ErrorCodes.FieldDefaultInvalid : ErrorCodes.FieldTypeUnknown,
                        row.IsTest ? ValidationSeverity.Error : ValidationSeverity.Error,
                        error.Message,
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

            var rawKey = row.RawValues.TryGetValue(document.Schema.PrimaryKey, out var suppliedKey) ? suppliedKey : null;
            var key = string.IsNullOrWhiteSpace(rawKey) ? primaryKeyField.DefaultValue : rawKey;
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!row.IsTest && !keys.Add(key))
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.PrimaryKeyDuplicate,
                    ValidationSeverity.Error,
                    $"Duplicate primary key '{key}'.",
                    document.SourceName,
                    row.SourceRow,
                    primaryKeyField.SourceColumn + 1,
                    document.Schema.PrimaryKey,
                    key));
            }
        }

        return issues;
    }
}
