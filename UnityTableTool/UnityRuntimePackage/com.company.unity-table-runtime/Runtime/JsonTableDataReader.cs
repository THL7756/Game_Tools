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
                && json.RootElement.TryGetProperty("rows", out var rows)
                && rows.ValueKind == JsonValueKind.Array;
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
        var schemaHash = root.GetProperty("schemaHash").GetString() ?? string.Empty;
        var isSingleton = root.TryGetProperty("isSingleton", out var singletonProperty) && singletonProperty.GetBoolean();
        var primaryKey = root.TryGetProperty("primaryKey", out var primaryKeyProperty) && primaryKeyProperty.ValueKind != JsonValueKind.Null
            ? primaryKeyProperty.GetString()
            : null;
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var property in row.EnumerateObject())
                values[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString();
            rows.Add(values);
        }
        return new TableRuntimeDocument(tableName, schemaHash, rows, primaryKey, isSingleton);
    }
}
