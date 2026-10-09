// 用途：读取工具导出的客户端 JSON，并保留字段定义、默认值和诊断 schema hash。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Text.Json;
using Company.UnityTableRuntime.Shared;

namespace Company.UnityTableRuntime;

public static class JsonTableDataReader
{
    public static bool CanRead(byte[] data)
    {
        try
        {
            using var json = JsonDocument.Parse(data);
            return json.RootElement.TryGetProperty("tableName", out _)
                && ((json.RootElement.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
                    || (json.RootElement.TryGetProperty("data", out var singletonData) && singletonData.ValueKind == JsonValueKind.Object));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static TableRuntimeDocument Read(byte[] data)
    {
        using var json = JsonDocument.Parse(data);
        var root = json.RootElement;
        var tableName = root.GetProperty("tableName").GetString() ?? string.Empty;
        var schemaHash = root.TryGetProperty("schemaHash", out var schemaProperty) ? schemaProperty.GetString() ?? string.Empty : string.Empty;
        var isSingleton = root.TryGetProperty("isSingleton", out var singletonProperty) && singletonProperty.GetBoolean();
        var primaryKey = root.TryGetProperty("primaryKey", out var primaryKeyProperty) && primaryKeyProperty.ValueKind != JsonValueKind.Null
            ? primaryKeyProperty.GetString()
            : null;
        var fields = new List<RuntimeFieldDefinition>();
        if (root.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fieldArray.EnumerateArray())
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var type = field.GetProperty("type").GetString() ?? string.Empty;
                var defaultValue = field.TryGetProperty("defaultValue", out var defaultProperty)
                    && defaultProperty.ValueKind != JsonValueKind.Null
                    ? defaultProperty.GetString()
                    : null;
                if (name.Length > 0 && type.Length > 0)
                    fields.Add(new RuntimeFieldDefinition(name, type, defaultValue));
            }
        }

        var separators = ReadSeparators(root);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        if (isSingleton && root.TryGetProperty("data", out var singletonData) && singletonData.ValueKind == JsonValueKind.Object)
            rows.Add(ReadRow(singletonData));
        else if (root.TryGetProperty("rows", out var rowArray) && rowArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rowArray.EnumerateArray())
                rows.Add(ReadRow(row));
        }
        return new TableRuntimeDocument(tableName, schemaHash, rows, primaryKey, isSingleton, fields, separators);
    }

    private static RuntimeArraySeparators ReadSeparators(JsonElement root)
    {
        if (!root.TryGetProperty("arraySeparators", out var separatorObject)
            || separatorObject.ValueKind != JsonValueKind.Object)
            return RuntimeArraySeparators.Default;

        var inner = separatorObject.TryGetProperty("inner", out var innerProperty)
            ? innerProperty.GetString() ?? "#"
            : "#";
        var middle = separatorObject.TryGetProperty("middle", out var middleProperty)
            ? middleProperty.GetString() ?? "|"
            : "|";
        var outer = separatorObject.TryGetProperty("outer", out var outerProperty)
            ? outerProperty.GetString() ?? ";"
            : ";";
        var separators = new RuntimeArraySeparators(inner, middle, outer);
        return separators.IsValid ? separators : RuntimeArraySeparators.Default;
    }

    private static IReadOnlyDictionary<string, string?> ReadRow(JsonElement row)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in row.EnumerateObject())
            values[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString();
        return values;
    }
}
