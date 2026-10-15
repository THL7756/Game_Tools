// 用途：把复杂字段的解析结果转换为 JSON 可序列化的 localization、beans 和 map 结构。
// 编写日期：2026-10-14
// 最近修改日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Company.UnityTableRuntime.Shared;
using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Serialization
{
    public static class ComplexValueSerializer
    {
        public static object? ParseAndSerialize(
            string? raw,
            TypeDescriptor type,
            ArraySeparatorOptions separators)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var parsed = ValueParser.Parse(raw, type, separators);
            return ToJsonValue(parsed, type.ToRuntime());
        }

        public static object? ToJsonValue(
            object? value,
            RuntimeTypeDescriptor type)
        {
            if (value is null)
                return null;
            if (type.Dimensions > 0 && value is Array array)
            {
                var elementType = WithoutOneDimension(type);
                return array.Cast<object?>()
                    .Select(item => ToJsonValue(item, elementType))
                    .ToArray();
            }
            if (type.IsLocalization && value is RuntimeLocalizationValue localization)
                return localization.Key;
            if (type.IsBeans && value is RuntimeBeanValue bean)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var field in type.BeanFields ?? Array.Empty<RuntimeBeanField>())
                    result[field.Name] = ToJsonValue(bean.Get(field.Name), field.Type);
                return result;
            }
            if (type.IsMap && value is RuntimeMapValue map)
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var entry in map.Entries)
                {
                    var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                    result[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["text"] = ToJsonValue(entry.Text, type.ValueType!),
                        ["bean"] = ToJsonValue(entry.Bean, type.BeanType!)
                    };
                }
                return result;
            }
            return value;
        }

        private static RuntimeTypeDescriptor WithoutOneDimension(RuntimeTypeDescriptor type) => new(
            type.BaseType,
            type.Dimensions - 1,
            type.EnumValues,
            type.IsValid,
            type.RawText,
            type.BeanFields,
            type.KeyType,
            type.ValueType,
            type.BeanType);
    }
}
