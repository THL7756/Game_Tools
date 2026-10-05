using System;
using System.Collections.Generic;
using System.Text.Json;

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
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        if (isSingleton && root.TryGetProperty("data", out var singletonData) && singletonData.ValueKind == JsonValueKind.Object)
            rows.Add(ReadRow(singletonData));
        else if (root.TryGetProperty("rows", out var rowArray) && rowArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rowArray.EnumerateArray())
                rows.Add(ReadRow(row));
        }
        return new TableRuntimeDocument(tableName, schemaHash, rows, primaryKey, isSingleton);
    }

    private static IReadOnlyDictionary<string, string?> ReadRow(JsonElement row)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in row.EnumerateObject())
            values[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString();
        return values;
    }
}
