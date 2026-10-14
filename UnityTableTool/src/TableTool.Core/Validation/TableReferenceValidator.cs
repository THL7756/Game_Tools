// 用途：校验普通字段及 beans/map 内部的逻辑表引用和 localization 键引用。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System.Globalization;
using Company.UnityTableRuntime.Shared;
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
        var localizationKeys = BuildLocalizationKeys(allDocuments);
        var localizationTables = allDocuments
            .Where(document => document.Schema.Name.Equals("localization", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var localizationHasId = localizationTables.Any(document =>
            document.Schema.Fields.Any(field => field.Name.Equals("id", StringComparison.OrdinalIgnoreCase)));
        var issues = new List<ValidationIssue>();

        foreach (var document in selectedDocuments)
        foreach (var field in document.Schema.AllFields)
        foreach (var row in document.Rows.Where(row => !row.IsTest))
        {
            var raw = row.GetValue(field);
            var value = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            object parsed;
            try
            {
                parsed = ValueParser.Parse(value, field.Type, separators);
            }
            catch (FormatException)
            {
                continue;
            }

            foreach (var item in WalkValues(field.Name, field.Type, parsed))
            {
                var sourceRow = document.Schema.IsSingleton ? field.NameRow : row.SourceRow;
                if (item.IsLocalization)
                {
                    if (localizationTables.Length > 0 && !localizationHasId)
                    {
                        issues.Add(new ValidationIssue(
                            ErrorCodes.LocalizationKeyMissing,
                            ValidationSeverity.Error,
                            "localization 表必须包含名为 id 的键列。",
                            localizationTables[0].SourceName,
                            localizationTables[0].Schema.Fields.FirstOrDefault()?.TypeRow,
                            localizationTables[0].Schema.Fields.FirstOrDefault()?.SourceColumn + 1,
                            Suggestion: "将 localization 表的键列命名为 id。"));
                    }
                    var key = item.Value is RuntimeLocalizationValue localization
                        ? localization.Key
                        : Convert.ToString(item.Value, CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(key) || localizationKeys.Contains(key))
                        continue;
                    issues.Add(new ValidationIssue(
                        ErrorCodes.LocalizationKeyMissing,
                        ValidationSeverity.Error,
                        $"字段“{field.Name}”引用的多语言键“{key}”在 localization 表中不存在。",
                        row.SourceName ?? document.SourceName,
                        sourceRow,
                        field.SourceColumn + 1,
                        field.Name,
                        key,
                        "请在 localization 表中补充该键。"));
                    continue;
                }

                if (!item.FieldName.Split('.').Last().EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                    continue;
                ValidateReference(
                    document,
                    row,
                    field,
                    sourceRow,
                    item.FieldName,
                    item.Type,
                    item.Value,
                    tables,
                    indexes,
                    issues);
            }
        }

        return issues.Distinct().ToArray();
    }

    private static void ValidateReference(
        TableDocument document,
        TableRow row,
        FieldSchema sourceField,
        int sourceRow,
        string fieldName,
        TypeDescriptor type,
        object? parsedValue,
        IReadOnlyDictionary<string, TableDocument[]> tables,
        IReadOnlyDictionary<string, HashSet<string>> indexes,
        ICollection<ValidationIssue> issues)
    {
        var simpleFieldName = fieldName.Split('.').Last();
        var referencedTableName = simpleFieldName[..^3];
        if (!tables.TryGetValue(referencedTableName, out var referencedDocuments))
        {
            issues.Add(new ValidationIssue(
                ErrorCodes.TableReferenceMissing,
                ValidationSeverity.Error,
                $"引用表“{referencedTableName}”未找到。",
                document.SourceName,
                sourceRow,
                sourceField.SourceColumn + 1,
                sourceField.Name,
                referencedTableName,
                "补全引用表，或移除对应引用字段。"));
            return;
        }

        var primaryKey = GetLogicalPrimaryKey(referencedDocuments);
        var targetField = referencedDocuments
            .Select(item => item.Schema.Fields.FirstOrDefault(candidate =>
                candidate.Name.Equals(primaryKey, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(candidate => candidate is not null);
        if (targetField is not null && !HasCompatibleType(type, targetField.Type))
        {
            issues.Add(new ValidationIssue(
                ErrorCodes.TableReferenceTypeMismatch,
                ValidationSeverity.Error,
                $"引用字段“{fieldName}”类型“{type.DisplayText}”与目标主键类型“{targetField.Type.DisplayText}”不一致。",
                document.SourceName,
                sourceField.TypeRow,
                sourceField.SourceColumn + 1,
                sourceField.Name,
                referencedTableName,
                "请让引用字段和目标表主键使用相同类型。"));
            return;
        }

        var normalized = NormalizeValue(parsedValue);
        if (string.IsNullOrWhiteSpace(normalized) || indexes[referencedTableName].Contains(normalized))
            return;
        issues.Add(new ValidationIssue(
            ErrorCodes.TableReferenceKeyMissing,
            ValidationSeverity.Error,
            $"字段“{fieldName}”引用的主键值“{normalized}”在表“{referencedTableName}”中不存在。",
            row.SourceName ?? document.SourceName,
            sourceRow,
            sourceField.SourceColumn + 1,
            sourceField.Name,
            normalized,
            "请填写目标表中存在的主键值。"));
    }

    private static IEnumerable<ReferenceValue> WalkValues(string fieldName, TypeDescriptor type, object? value)
    {
        if (value is null)
            yield break;
        if (type.Dimensions > 0 && value is Array array)
        {
            var childType = WithoutOneDimension(type);
            foreach (var item in array)
            foreach (var nested in WalkValues(fieldName, childType, item))
                yield return nested;
            yield break;
        }
        if (type.IsLocalization)
        {
            yield return new ReferenceValue(fieldName, type, value, true);
            yield break;
        }
        if (type.IsBeans && value is RuntimeBeanValue bean)
        {
            foreach (var field in type.BeanFields ?? [])
            foreach (var nested in WalkValues($"{fieldName}.{field.Name}", field.Type, bean.Get(field.Name)))
                yield return nested;
            yield break;
        }
        if (type.IsMap && value is RuntimeMapValue map)
        {
            foreach (var entry in map.Entries)
            foreach (var nested in WalkValues(fieldName, type.BeanType!, entry.Bean))
                yield return nested;
            yield break;
        }
        yield return new ReferenceValue(fieldName, type, value, false);
    }

    private static HashSet<string> BuildLocalizationKeys(IEnumerable<TableDocument> documents)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents.Where(document => document.Schema.Name.Equals("localization", StringComparison.OrdinalIgnoreCase)))
        {
            var keyField = document.Schema.Fields.FirstOrDefault(field => field.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
            if (keyField is null)
                continue;
            foreach (var row in document.Rows.Where(row => !row.IsTest))
            {
                var key = row.GetValue(keyField)?.Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }
        }
        return keys;
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
                var raw = row.GetValue(field);
                var value = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                try
                {
                    keys.Add(NormalizeValue(ValueParser.Parse(value, field.Type, separators)));
                }
                catch (FormatException)
                {
                }
            }
        }
        return keys;
    }

    private static bool HasCompatibleType(TypeDescriptor left, TypeDescriptor right) =>
        left.IsValid
        && right.IsValid
        && string.Equals(left.BaseType, right.BaseType, StringComparison.OrdinalIgnoreCase)
        && left.Dimensions == right.Dimensions
        && (left.EnumValues ?? []).SequenceEqual(right.EnumValues ?? [], StringComparer.Ordinal);

    private static string NormalizeValue(object? value) => value switch
    {
        RuntimeLocalizationValue localization => localization.Key,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static TypeDescriptor WithoutOneDimension(TypeDescriptor type) => new(
        type.BaseType,
        type.Dimensions - 1,
        type.EnumValues,
        type.IsValid,
        type.RawText,
        type.BeanFields,
        type.KeyType,
        type.ValueType,
        type.BeanType);

    private static string GetLogicalPrimaryKey(IReadOnlyList<TableDocument> documents) => documents.SelectMany(document => document.Schema.Fields)
        .FirstOrDefault(field => field.Name.Equals("id", StringComparison.OrdinalIgnoreCase))?.Name
        ?? documents[0].Schema.PrimaryKey;

    private sealed record ReferenceValue(string FieldName, TypeDescriptor Type, object? Value, bool IsLocalization);
}
