// 用途：把正式配置表序列化为 JSON 数据，并将复杂字段输出为结构化对象。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System.Text.Json;
using System.Text.Json.Serialization;
using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed class JsonTableExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string Export(
        TableDocument document,
        ArraySeparatorOptions? separators = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>? localizationCatalog = null)
    {
        separators ??= ArraySeparatorOptions.Default;
        var rows = document.Rows.Where(row => !row.IsTest).Select(row => document.Schema.Fields.ToDictionary(
            field => field.Name,
            field => GetJsonValue(row, field, separators, localizationCatalog),
            StringComparer.OrdinalIgnoreCase)).ToArray();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tableName"] = document.Schema.Name,
            ["isSingleton"] = document.Schema.IsSingleton,
            ["primaryKey"] = document.Schema.IsSingleton ? null : document.Schema.PrimaryKey,
            ["fields"] = document.Schema.Fields.Select(field => new
            {
                name = field.Name,
                type = field.Type.DisplayText,
                defaultValue = field.DefaultValue
            }).ToArray(),
            ["arraySeparators"] = new
            {
                inner = separators.Inner,
                middle = separators.Middle,
                outer = separators.Outer
            }
        };

        if (document.Schema.IsSingleton)
            payload["data"] = rows.SingleOrDefault();
        else
            payload["rows"] = rows;

        return JsonSerializer.Serialize(payload, Options);
    }

    private static object? GetJsonValue(
        TableRow row,
        FieldSchema field,
        ArraySeparatorOptions separators,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>? localizationCatalog)
    {
        var raw = row.GetValue(field);
        var value = string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
        if (!field.Type.IsComplex)
            return value;
        return ComplexValueSerializer.ParseAndSerialize(value, field.Type, separators, localizationCatalog);
    }
}
