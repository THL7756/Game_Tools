using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime;

public sealed class TableRuntimeDocument
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string?>> rows;

    public TableRuntimeDocument(string tableName, string schemaHash, IEnumerable<IReadOnlyDictionary<string, string?>> rows, string? primaryKey, bool isSingleton)
    {
        TableName = tableName;
        SchemaHash = schemaHash;
        IsSingleton = isSingleton;
        PrimaryKey = primaryKey;
        var materializedRows = rows.ToArray();
        SingletonRow = isSingleton ? materializedRows.SingleOrDefault() : null;
        this.rows = isSingleton || string.IsNullOrWhiteSpace(primaryKey)
            ? new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal)
            : materializedRows.Where(row => row.TryGetValue(primaryKey!, out var key) && !string.IsNullOrWhiteSpace(key))
                .ToDictionary(row => row[primaryKey!]!, row => row, StringComparer.Ordinal);
    }

    public string TableName { get; }
    public string SchemaHash { get; }
    public string? PrimaryKey { get; }
    public bool IsSingleton { get; }
    public IReadOnlyDictionary<string, string?>? SingletonRow { get; }
    public IReadOnlyCollection<string> Keys => rows.Keys;

    public bool TryGet(string key, out IReadOnlyDictionary<string, string?> row) => rows.TryGetValue(key, out row!);

    public bool TryGetSingleton(out IReadOnlyDictionary<string, string?> row)
    {
        row = SingletonRow!;
        return SingletonRow is not null;
    }
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
