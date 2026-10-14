#nullable enable
// 用途：管理已生成客户端表的加载、主键查询、单例查询和批量转换。
// 编写日期：2026-10-10
// 最近修改日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime
{
    public sealed class TableManager
    {
        private readonly Dictionary<string, LoadedTable> tables = new(StringComparer.Ordinal);

        public void Load(string tableName, byte[] data, string? manifestFormat = null)
        {
            _ = LoadIfChanged(tableName, data, manifestFormat);
        }

        public bool LoadIfChanged(string tableName, byte[] data, string? manifestFormat = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("A table name is required.", nameof(tableName));
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            if (tables.TryGetValue(tableName, out var loaded)
                && string.Equals(loaded.ManifestFormat, manifestFormat, StringComparison.OrdinalIgnoreCase)
                && loaded.Data.AsSpan().SequenceEqual(data))
            {
                return false;
            }

            var table = RuntimeFormat.ReadAuto(data, manifestFormat);
            tables[tableName] = new LoadedTable(table, data.ToArray(), manifestFormat);
            return true;
        }

        public void LoadClient(string tableName, TableRuntimeSettings settings, string? manifestFormat = null)
        {
            if (settings.LoadBytes is null)
                throw new InvalidOperationException("TableRuntimeSettings.LoadBytes is required for client loading.");
            Load(tableName, settings.LoadBytes(tableName), manifestFormat);
        }

        public bool IsLoaded(string tableName) => tables.ContainsKey(tableName);

        public IReadOnlyCollection<string> LoadedTableNames => tables.Keys.ToArray();

        public void Unload(string tableName) => tables.Remove(tableName);

        public void Clear() => tables.Clear();

        public bool Has(string tableName, string key) => tables.TryGetValue(tableName, out var table) && table.Document.TryGet(key, out _);

        public IReadOnlyDictionary<string, string?> Get(string tableName, string key)
        {
            if (!tables.TryGetValue(tableName, out var loaded) || !loaded.Document.TryGet(key, out var row))
                throw new KeyNotFoundException($"Table '{tableName}' does not contain key '{key}'.");
            return row;
        }

        public IReadOnlyDictionary<string, string?> GetSingleton(string tableName)
        {
            if (!tables.TryGetValue(tableName, out var loaded) || !loaded.Document.TryGetSingleton(out var row))
                throw new KeyNotFoundException($"Table '{tableName}' is not a singleton table.");
            return row;
        }

        public T Get<T>(string tableName, string key, Func<TableRowView, T> converter)
        {
            if (!tables.TryGetValue(tableName, out var loaded) || !loaded.Document.TryGet(key, out var row))
                throw new KeyNotFoundException($"Table '{tableName}' does not contain key '{key}'.");
            return converter(loaded.Document.View(row));
        }

        public T GetSingleton<T>(string tableName, Func<TableRowView, T> converter)
        {
            if (!tables.TryGetValue(tableName, out var loaded) || !loaded.Document.TryGetSingleton(out var row))
                throw new KeyNotFoundException($"Table '{tableName}' is not a singleton table.");
            return converter(loaded.Document.View(row));
        }

        public IReadOnlyList<T> GetAll<T>(string tableName, Func<TableRowView, T> converter)
        {
            if (!tables.TryGetValue(tableName, out var loaded))
                throw new KeyNotFoundException($"Table '{tableName}' is not loaded.");
            return loaded.Document.Keys.Select(key => converter(loaded.Document.View(loaded.Document.Get(key)))).ToArray();
        }

        private sealed class LoadedTable
        {
            public LoadedTable(TableRuntimeDocument document, byte[] data, string? manifestFormat)
            {
                Document = document;
                Data = data;
                ManifestFormat = manifestFormat;
            }

            public TableRuntimeDocument Document { get; }
            public byte[] Data { get; }
            public string? ManifestFormat { get; }
        }
    }
}
