// 用途：定义客户端表运行时文档及 JSON/UTB1 自动读取入口，不执行业务校验。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Linq;
using Company.UnityTableRuntime.Shared;

namespace Company.UnityTableRuntime;

public sealed class TableRuntimeDocument
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string?>> rows;

    public TableRuntimeDocument(
        string tableName,
        string schemaHash,
        IEnumerable<IReadOnlyDictionary<string, string?>> rows,
        string? primaryKey,
        bool isSingleton)
        : this(tableName, schemaHash, rows, primaryKey, isSingleton, Array.Empty<RuntimeFieldDefinition>(), RuntimeArraySeparators.Default)
    {
    }

    public TableRuntimeDocument(
        string tableName,
        string schemaHash,
        IEnumerable<IReadOnlyDictionary<string, string?>> rows,
        string? primaryKey,
        bool isSingleton,
        IEnumerable<RuntimeFieldDefinition> fields,
        RuntimeArraySeparators separators)
    {
        TableName = tableName;
        SchemaHash = schemaHash;
        IsSingleton = isSingleton;
        PrimaryKey = primaryKey;
        Fields = fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        Separators = separators;
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
    public IReadOnlyDictionary<string, RuntimeFieldDefinition> Fields { get; }
    public RuntimeArraySeparators Separators { get; }

    public bool TryGet(string key, out IReadOnlyDictionary<string, string?> row) => rows.TryGetValue(key, out row!);

    internal IReadOnlyDictionary<string, string?> Get(string key) => rows[key];

    public bool TryGetSingleton(out IReadOnlyDictionary<string, string?> row)
    {
        row = SingletonRow!;
        return SingletonRow is not null;
    }

    public TableRowView View(IReadOnlyDictionary<string, string?> row) =>
        new(row, Fields, Separators);
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
