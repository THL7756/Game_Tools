#nullable enable
// 用途：为生成的客户端数据类提供带默认值和共享类型转换的数据行读取入口。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using Company.UnityTableRuntime.Shared;

namespace Company.UnityTableRuntime
{
    public sealed class RuntimeFieldDefinition
    {
        public RuntimeFieldDefinition(string name, string type, string? defaultValue)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public string Type { get; }
        public string? DefaultValue { get; }
    }

    public sealed class TableRowView
    {
        private readonly IReadOnlyDictionary<string, string?> values;
        private readonly IReadOnlyDictionary<string, RuntimeFieldDefinition> fields;
        private readonly RuntimeArraySeparators separators;

        internal TableRowView(
            IReadOnlyDictionary<string, string?> values,
            IReadOnlyDictionary<string, RuntimeFieldDefinition> fields,
            RuntimeArraySeparators separators)
        {
            this.values = values;
            this.fields = fields;
            this.separators = separators;
        }

        public string? GetRaw(string fieldName) => values.TryGetValue(fieldName, out var value) ? value : null;

        public string? GetValue(string fieldName)
        {
            var raw = GetRaw(fieldName);
            if (!string.IsNullOrWhiteSpace(raw))
                return raw;
            return fields.TryGetValue(fieldName, out var field) ? field.DefaultValue : null;
        }

        public T Read<T>(string fieldName)
        {
            if (!fields.TryGetValue(fieldName, out var field))
                throw new KeyNotFoundException($"Field '{fieldName}' is not defined in the table.");

            var raw = GetValue(fieldName);
            if (raw is null && typeof(T) == typeof(string))
                return default!;
            var value = RuntimeValueParser.Parse(
                raw,
                RuntimeTypeDescriptor.Parse(field.Type),
                separators);
            return RuntimeValueConverter.Convert<T>(value, fieldName);
        }
    }
}
