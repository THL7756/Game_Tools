// 用途：按字段类型解析配置表数据，覆盖标量、结构值和一到三维数组。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.Globalization;
using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public static class ValueParser
{
    public static object Parse(
        string text,
        TypeDescriptor type,
        ArraySeparatorOptions? separators = null)
    {
        if (!type.IsValid)
            throw new FormatException($"字段类型 '{type.RawText ?? type.BaseType}' 无效。");

        separators ??= ArraySeparatorOptions.Default;
        if (!separators.IsValid)
            throw new FormatException("数组分隔符设置无效。");

        var value = text?.Trim() ?? string.Empty;
        if (type.Dimensions == 0)
            return ParseScalar(value, type);

        return ParseLevel(value, type, separators, 0);
    }

    private static Array ParseLevel(string text, TypeDescriptor type, ArraySeparatorOptions separators, int level)
    {
        var separator = SeparatorFor(type.Dimensions, separators, level);
        var parts = text.Split(separator);
        if (parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException($"Empty element in {type.BaseType} array.");

        var values = new object[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            values[i] = level + 1 == type.Dimensions
                ? ParseLeaf(parts[i], type, separators)
                : ParseLevel(parts[i], type, separators, level + 1);
        }

        var result = CreateArray(type, level, values.Length);
        for (var i = 0; i < values.Length; i++)
            result.SetValue(values[i], i);

        return result;
    }

    private static object ParseLeaf(string text, TypeDescriptor type, ArraySeparatorOptions separators)
    {
        foreach (var separator in new[] { separators.Inner[0], separators.Middle[0], separators.Outer[0] }
                     .Where(separator => !IsAllowedSeparator(type.Dimensions, separators, separator)))
        {
            if (text.IndexOf(separator) >= 0)
                throw new FormatException($"Unexpected high-dimensional separator '{separator}' in {type.BaseType} array.");
        }

        return ParseScalar(text.Trim(), type);
    }

    private static char SeparatorFor(int dimensions, ArraySeparatorOptions separators, int level)
    {
        if (dimensions is < 1 or > 3)
            throw new FormatException("Array dimensions must be between one and three.");
        var ordered = new[] { separators.Inner[0], separators.Middle[0], separators.Outer[0] };
        return ordered[dimensions - level - 1];
    }

    private static bool IsAllowedSeparator(int dimensions, ArraySeparatorOptions separators, char separator)
    {
        for (var level = 0; level < dimensions; level++)
        {
            if (SeparatorFor(dimensions, separators, level) == separator)
                return true;
        }
        return false;
    }

    private static Array CreateArray(TypeDescriptor type, int level, int length)
    {
        var elementType = GetRuntimeType(type.BaseType);
        for (var i = type.Dimensions - 1; i > level; i--)
            elementType = elementType.MakeArrayType();
        return Array.CreateInstance(elementType, length);
    }

    private static object ParseScalar(string text, TypeDescriptor type)
    {
        var baseType = type.BaseType;
        if (text.Length == 0)
            throw new FormatException($"Empty {baseType} value.");

        try
        {
            return baseType.ToLowerInvariant() switch
            {
                "int" => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "long" => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "float" => ParseFloating(text, baseType, isDouble: false),
                "double" => ParseFloating(text, baseType, isDouble: true),
                "bool" => ParseBool(text),
                "string" => text,
                "enum" => ParseEnum(text, type),
                "vector2" => ParseComponents(text, baseType, 2),
                "vector3" => ParseComponents(text, baseType, 3),
                "vector4" => ParseComponents(text, baseType, 4),
                "color" => ParseColor(text),
                "quaternion" => ParseComponents(text, baseType, 4),
                _ => throw new FormatException($"Unknown type '{baseType}'.")
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            if (ex is FormatException format && format.Message.Contains("invalid for type", StringComparison.OrdinalIgnoreCase))
                throw;
            throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.", ex);
        }
    }

    private static object ParseFloating(string text, string baseType, bool isDouble)
    {
        if (isDouble)
        {
            var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!double.IsFinite(value))
                throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
            return value;
        }

        var single = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!float.IsFinite(single))
            throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
        return single;
    }

    private static float[] ParseComponents(string text, string baseType, int expectedCount)
    {
        var normalized = RemoveOptionalBrackets(text.Trim());
        var parts = normalized.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != expectedCount || parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException($"Value '{text}' is invalid for type '{baseType}'; expected {expectedCount} comma-separated components.");

        return parts.Select(part => (float)ParseFloating(part, baseType, isDouble: false)).ToArray();
    }

    private static float[] ParseColor(string text)
    {
        var normalized = RemoveOptionalBrackets(text.Trim());
        var parts = normalized.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4) || parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException($"Value '{text}' is invalid for type 'Color'; expected three or four comma-separated components.");

        return parts.Select(part => (float)ParseFloating(part, "Color", isDouble: false)).ToArray();
    }

    private static string RemoveOptionalBrackets(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '(' && value[^1] == ')')
                || (value[0] == '[' && value[^1] == ']')
                || (value[0] == '{' && value[^1] == '}')))
            return value[1..^1].Trim();
        return value;
    }

    private static bool ParseBool(string text) => text.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new FormatException($"Value '{text}' is invalid for type 'bool'.")
    };

    private static string ParseEnum(string text, TypeDescriptor type)
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
        "string" => typeof(string),
        "enum" => typeof(string),
        "vector2" or "vector3" or "vector4" or "color" or "quaternion" => typeof(float[]),
        _ => throw new FormatException($"Unknown type '{baseType}'.")
    };
}
