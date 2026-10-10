#nullable enable
// 用途：提供工具和 Unity Runtime 共用的字段类型、数组维度和分隔符定义。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime.Shared
{
    public sealed class RuntimeArraySeparators
    {
        public RuntimeArraySeparators(string inner = "#", string middle = "|", string outer = ";")
        {
            Inner = inner;
            Middle = middle;
            Outer = outer;
        }

        public string Inner { get; }
        public string Middle { get; }
        public string Outer { get; }
        public static RuntimeArraySeparators Default { get; } = new();

        public bool IsValid => IsSingle(Inner)
            && IsSingle(Middle)
            && IsSingle(Outer)
            && Inner != Middle
            && Inner != Outer
            && Middle != Outer
            && Inner != ","
            && Middle != ","
            && Outer != ",";

        private static bool IsSingle(string value) =>
            value is not null && value.Length == 1 && !char.IsWhiteSpace(value[0]);
    }

    public sealed class RuntimeTypeDescriptor
    {
        private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "long", "float", "double", "bool", "string",
        "Vector2", "Vector3", "Vector4", "Color", "Quaternion"
    };

        public RuntimeTypeDescriptor(
            string baseType,
            int dimensions,
            IReadOnlyList<string>? enumValues = null,
            bool isValid = true,
            string? rawText = null)
        {
            BaseType = baseType;
            Dimensions = dimensions;
            EnumValues = enumValues;
            IsValid = isValid;
            RawText = rawText;
        }

        public string BaseType { get; }
        public int Dimensions { get; }
        public IReadOnlyList<string>? EnumValues { get; }
        public bool IsValid { get; }
        public string? RawText { get; }
        public bool IsEnum => BaseType.Equals("enum", StringComparison.OrdinalIgnoreCase);

        public string DisplayText => IsEnum
            ? $"enum({string.Join('|', EnumValues ?? Array.Empty<string>())})" + string.Concat(Enumerable.Repeat("()", Dimensions))
            : BaseType + string.Concat(Enumerable.Repeat("()", Dimensions));

        public static RuntimeTypeDescriptor Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new FormatException("Type cannot be empty.");

            var normalized = text.Trim();
            var dimensions = 0;
            while (normalized.EndsWith("()", StringComparison.Ordinal))
            {
                dimensions++;
                normalized = normalized[..^2];
            }

            if (dimensions > 3)
                throw new FormatException("Array dimensions cannot exceed three.");

            if (normalized.StartsWith("enum(", StringComparison.OrdinalIgnoreCase)
                && normalized.EndsWith(")", StringComparison.Ordinal))
            {
                var content = normalized[5..^1];
                var rawValues = content.Split('|');
                var values = rawValues.Select(value => value.Trim()).ToArray();
                if (values.Length == 0
                    || values.Any(value => value.Length == 0)
                    || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    throw new FormatException($"Enum type '{text}' must contain unique non-empty values separated by '|'.");
                return new RuntimeTypeDescriptor("enum", dimensions, values);
            }

            var canonical = SupportedTypes.FirstOrDefault(type =>
                type.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                throw new FormatException($"Unknown type '{text}'.");
            return new RuntimeTypeDescriptor(canonical, dimensions);
        }

        public static RuntimeTypeDescriptor Invalid(string text) =>
            new("unknown", 0, null, false, text);
    }
}
