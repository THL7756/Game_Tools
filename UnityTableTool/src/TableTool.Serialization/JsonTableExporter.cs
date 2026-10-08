// 用途：把正式配置表序列化为 JSON 数据。
// 编写日期：2026-10-08
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

    public string Export(TableDocument document)
    {
        var rows = document.Rows.Where(row => !row.IsTest).Select(row => document.Schema.Fields.ToDictionary(
            field => field.Name,
            field => GetEffectiveValue(row, field),
            StringComparer.OrdinalIgnoreCase)).ToArray();
        var payload = new
        {
            formatVersion = 1,
            tableName = document.Schema.Name,
            schemaHash = SchemaHasher.Compute(document.Schema),
            isSingleton = document.Schema.IsSingleton,
            primaryKey = document.Schema.IsSingleton ? null : document.Schema.PrimaryKey,
            fields = document.Schema.Fields.Select(field => new
            {
                name = field.Name,
                type = field.Type.DisplayText,
                defaultValue = field.DefaultValue
            }).ToArray(),
            rows = document.Schema.IsSingleton ? Array.Empty<Dictionary<string, string?>>() : rows,
            data = document.Schema.IsSingleton ? rows.SingleOrDefault() : null
        };

        return JsonSerializer.Serialize(payload, Options);
    }

    private static string? GetEffectiveValue(TableRow row, FieldSchema field)
    {
        var raw = row.RawValues.GetValueOrDefault(field.Name);
        return string.IsNullOrWhiteSpace(raw) ? field.DefaultValue : raw;
    }
}
