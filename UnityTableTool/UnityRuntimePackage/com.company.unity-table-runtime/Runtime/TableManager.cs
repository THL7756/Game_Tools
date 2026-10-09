// 用途：管理已生成客户端表的加载、主键查询、单例查询和批量转换。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime;

public sealed class TableManager
{
    private readonly Dictionary<string, TableRuntimeDocument> tables = new(StringComparer.Ordinal);

    public void Load(string tableName, byte[] data, string? manifestFormat = null)
    {
        var table = RuntimeFormat.ReadAuto(data, manifestFormat);
        tables[tableName] = table;
    }

    public void LoadClient(string tableName, TableRuntimeSettings settings, string? manifestFormat = null)
    {
        if (settings.LoadBytes is null)
            throw new InvalidOperationException("TableRuntimeSettings.LoadBytes is required for client loading.");
        Load(tableName, settings.LoadBytes(tableName), manifestFormat);
    }

    public bool Has(string tableName, string key) => tables.TryGetValue(tableName, out var table) && table.TryGet(key, out _);

    public IReadOnlyDictionary<string, string?> Get(string tableName, string key)
    {
        if (!tables.TryGetValue(tableName, out var table) || !table.TryGet(key, out var row))
            throw new KeyNotFoundException($"Table '{tableName}' does not contain key '{key}'.");
        return row;
    }

    public IReadOnlyDictionary<string, string?> GetSingleton(string tableName)
    {
        if (!tables.TryGetValue(tableName, out var table) || !table.TryGetSingleton(out var row))
            throw new KeyNotFoundException($"Table '{tableName}' is not a singleton table.");
        return row;
    }

    public T Get<T>(string tableName, string key, Func<TableRowView, T> converter)
    {
        if (!tables.TryGetValue(tableName, out var table) || !table.TryGet(key, out var row))
            throw new KeyNotFoundException($"Table '{tableName}' does not contain key '{key}'.");
        return converter(table.View(row));
    }

    public T GetSingleton<T>(string tableName, Func<TableRowView, T> converter)
    {
        if (!tables.TryGetValue(tableName, out var table) || !table.TryGetSingleton(out var row))
            throw new KeyNotFoundException($"Table '{tableName}' is not a singleton table.");
        return converter(table.View(row));
    }

    public IReadOnlyList<T> GetAll<T>(string tableName, Func<TableRowView, T> converter)
    {
        if (!tables.TryGetValue(tableName, out var table))
            throw new KeyNotFoundException($"Table '{tableName}' is not loaded.");
        return table.Keys.Select(key => converter(table.View(table.Get(key)))).ToArray();
    }
}
