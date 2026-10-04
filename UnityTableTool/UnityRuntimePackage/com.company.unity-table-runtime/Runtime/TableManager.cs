using System;
using System.Collections.Generic;

namespace Company.UnityTableRuntime;

public sealed class TableManager
{
    private readonly Dictionary<string, TableRuntimeDocument> tables = new(StringComparer.Ordinal);

    public void Load(string tableName, byte[] data, string? manifestFormat = null)
    {
        var table = RuntimeFormat.ReadAuto(data, manifestFormat);
        tables[tableName] = table;
    }

    public bool Has(string tableName, string key) => tables.TryGetValue(tableName, out var table) && table.TryGet(key, out _);

    public IReadOnlyDictionary<string, string?> Get(string tableName, string key)
    {
        if (!tables.TryGetValue(tableName, out var table) || !table.TryGet(key, out var row))
            throw new KeyNotFoundException($"Table '{tableName}' does not contain key '{key}'.");
        return row;
    }
}
