#nullable enable
// 用途：提供工具和 Unity Runtime 共用的基础值、结构值和数组解析规则。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Globalization;
using System.Linq;

namespace Company.UnityTableRuntime.Shared
{
    public static class RuntimeValueParser
    {
        public static object Parse(
            string? text,
            RuntimeTypeDescriptor type,
            RuntimeArraySeparators? separators = null)
        {
            if (!type.IsValid)
                throw new FormatException($"字段类型 '{type.RawText ?? type.BaseType}' 无效。");
            separators ??= RuntimeArraySeparators.Default;
            if (!separators.IsValid)
                throw new FormatException("数组分隔符设置无效。");

            var value = text?.Trim() ?? string.Empty;
            return type.Dimensions == 0
                ? ParseScalar(value, type)
                : ParseLevel(value, type, separators, 0);
        }

        private static Array ParseLevel(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators, int level)
        {
            var parts = text.Split(SeparatorFor(type.Dimensions, separators, level));
            if (parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Empty element in {type.BaseType} array.");

            var values = new object[parts.Length];
            for (var index = 0; index < parts.Length; index++)
                values[index] = level + 1 == type.Dimensions
                    ? ParseLeaf(parts[index], type, separators)
                    : ParseLevel(parts[index], type, separators, level + 1);

            var result = CreateArray(type, level, values.Length);
            for (var index = 0; index < values.Length; index++)
                result.SetValue(values[index], index);
            return result;
        }

        private static object ParseLeaf(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            foreach (var separator in new[] { separators.Inner[0], separators.Middle[0], separators.Outer[0] }
                         .Where(separator => !IsAllowedSeparator(type.Dimensions, separators, separator)))
            {
                if (text.IndexOf(separator) >= 0)
                    throw new FormatException($"Unexpected high-dimensional separator '{separator}' in {type.BaseType} array.");
            }
            return ParseScalar(text.Trim(), type);
        }

        private static char SeparatorFor(int dimensions, RuntimeArraySeparators separators, int level)
        {
            if (dimensions is < 1 or > 3)
                throw new FormatException("Array dimensions must be between one and three.");
            return new[] { separators.Inner[0], separators.Middle[0], separators.Outer[0] }[dimensions - level - 1];
        }

        private static bool IsAllowedSeparator(int dimensions, RuntimeArraySeparators separators, char separator)
        {
            for (var level = 0; level < dimensions; level++)
                if (SeparatorFor(dimensions, separators, level) == separator)
                    return true;
            return false;
        }

        private static Array CreateArray(RuntimeTypeDescriptor type, int level, int length)
        {
            var elementType = GetRuntimeType(type.BaseType);
            for (var index = type.Dimensions - 1; index > level; index--)
                elementType = elementType.MakeArrayType();
            return Array.CreateInstance(elementType, length);
        }

        private static object ParseScalar(string text, RuntimeTypeDescriptor type)
        {
            if (text.Length == 0)
                throw new FormatException($"Empty {type.BaseType} value.");
            try
            {
                return type.BaseType.ToLowerInvariant() switch
                {
                    "int" => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    "long" => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    "float" => ParseFloating(text, "float", false),
                    "double" => ParseFloating(text, "double", true),
                    "bool" => ParseBool(text),
                    "string" => text,
                    "enum" => ParseEnum(text, type),
                    "vector2" => ParseComponents(text, "Vector2", 2),
                    "vector3" => ParseComponents(text, "Vector3", 3),
                    "vector4" => ParseComponents(text, "Vector4", 4),
                    "color" => ParseColor(text),
                    "quaternion" => ParseComponents(text, "Quaternion", 4),
                    _ => throw new FormatException($"Unknown type '{type.BaseType}'.")
                };
            }
            catch (Exception error) when (error is FormatException or OverflowException)
            {
                if (error is FormatException format
                    && format.Message.Contains("invalid for type", StringComparison.OrdinalIgnoreCase))
                    throw;
                throw new FormatException($"Value '{text}' is invalid for type '{type.BaseType}'.", error);
            }
        }

        private static object ParseFloating(string text, string baseType, bool isDouble)
        {
            if (isDouble)
            {
                var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
                return value;
            }
            var single = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(single) || float.IsInfinity(single))
                throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
            return single;
        }

        private static float[] ParseComponents(string text, string baseType, int expectedCount)
        {
            var parts = SplitAndTrim(RemoveOptionalBrackets(text), ',');
            if (parts.Length != expectedCount || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Value '{text}' is invalid for type '{baseType}'; expected {expectedCount} comma-separated components.");
            return parts.Select(part => (float)ParseFloating(part, baseType, false)).ToArray();
        }

        private static float[] ParseColor(string text)
        {
            var parts = SplitAndTrim(RemoveOptionalBrackets(text), ',');
            if (parts.Length is not (3 or 4) || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Value '{text}' is invalid for type 'Color'; expected three or four comma-separated components.");
            return parts.Select(part => (float)ParseFloating(part, "Color", false)).ToArray();
        }

        private static string RemoveOptionalBrackets(string value)
        {
            value = value.Trim();
            return value.Length >= 2
                && ((value[0] == '(' && value[^1] == ')')
                    || (value[0] == '[' && value[^1] == ']')
                    || (value[0] == '{' && value[^1] == '}'))
                ? value[1..^1].Trim()
                : value;
        }

        private static string[] SplitAndTrim(string value, char separator) =>
            value.Split(separator).Select(part => part.Trim()).ToArray();
        private static bool ParseBool(string text) => text.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new FormatException($"Value '{text}' is invalid for type 'bool'.")
        };

        private static string ParseEnum(string text, RuntimeTypeDescriptor type)
        {
            if (type.EnumValues?.Contains(text, StringComparer.Ordinal) == true)
                return text;
            throw new FormatException($"值 '{text}' 不在允许的 enum 值列表中。");
        }

        private static Type GetRuntimeType(string baseType) => baseType.ToLowerInvariant() switch
        {
            "int" => typeof(int),
            "long" => typeof(long),
            "float" => typeof(float),
            "double" => typeof(double),
            "bool" => typeof(bool),
            "string" or "enum" => typeof(string),
            "vector2" or "vector3" or "vector4" or "color" or "quaternion" => typeof(float[]),
            _ => throw new FormatException($"Unknown type '{baseType}'.")
        };
    }

    public static class RuntimeValueConverter
    {
        public static T Convert<T>(object? value, string fieldName) =>
            (T)Convert(value, typeof(T), fieldName);

        private static object Convert(object? value, Type targetType, string fieldName)
        {
            if (value is null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
                    return null!;
                throw new FormatException($"Field '{fieldName}' has no value for '{targetType.Name}'.");
            }
            if (targetType.IsInstanceOfType(value))
                return value;
            if (targetType.IsEnum && value is string enumText)
                return Enum.Parse(targetType, enumText, ignoreCase: false);
            if (targetType.IsArray && value is Array source)
            {
                var elementType = targetType.GetElementType()!;
                var output = Array.CreateInstance(elementType, source.Length);
                for (var index = 0; index < source.Length; index++)
                    output.SetValue(Convert(source.GetValue(index), elementType, fieldName), index);
                return output;
            }
            if (targetType == typeof(string))
                return value.ToString() ?? string.Empty;
            if (value is IConvertible)
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)!;
            throw new FormatException($"Field '{fieldName}' cannot be converted to '{targetType.Name}'.");
        }
    }
}
