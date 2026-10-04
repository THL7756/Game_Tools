using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime;

public sealed class TableRuntimeDocument
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string?>> rows;

    public TableRuntimeDocument(string tableName, string schemaHash, IEnumerable<IReadOnlyDictionary<string, string?>> rows, string primaryKey)
    {
        TableName = tableName;
        SchemaHash = schemaHash;
        PrimaryKey = primaryKey;
        this.rows = rows.Where(row => row.TryGetValue(primaryKey, out var key) && !string.IsNullOrWhiteSpace(key))
            .ToDictionary(row => row[primaryKey]!, row => row, StringComparer.Ordinal);
    }

    public string TableName { get; }
    public string SchemaHash { get; }
    public string PrimaryKey { get; }
    public IReadOnlyCollection<string> Keys => rows.Keys;

    public bool TryGet(string key, out IReadOnlyDictionary<string, string?> row) => rows.TryGetValue(key, out row!);
}

public static class RuntimeFormat
{
    public static TableRuntimeDocument ReadAuto(byte[] data, string? manifestFormat = null)
    {
        if (string.Equals(manifestFormat, "json", StringComparison.OrdinalIgnoreCase))
            return JsonTableDataReader.Read(data);
        if (string.Equals(manifestFormat, "bytes", StringComparison.OrdinalIgnoreCase))
            return BinaryTableDataReader.Read(data);
        if (BinaryTableDataReader.CanRead(data))
            return BinaryTableDataReader.Read(data);
        if (JsonTableDataReader.CanRead(data))
            return JsonTableDataReader.Read(data);
        throw new FormatException("Unknown table data format.");
    }
}
