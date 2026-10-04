using System.Globalization;

namespace TableTool.Core.Parsing;

public static class ValueParser
{
    private static readonly char[] DimensionSeparators = ['#', '|', ';'];

    public static object Parse(string text, TypeDescriptor type)
    {
        var value = text?.Trim() ?? string.Empty;
        if (type.Dimensions == 0)
            return ParseScalar(value, type.BaseType);

        return ParseLevel(value, type, 0);
    }

    private static Array ParseLevel(string text, TypeDescriptor type, int level)
    {
        var separator = SeparatorFor(type.Dimensions, level);
        var parts = text.Split(separator);
        if (parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException($"Empty element in {type.BaseType} array.");

        var values = new object[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            values[i] = level + 1 == type.Dimensions
                ? ParseLeaf(parts[i], type)
                : ParseLevel(parts[i], type, level + 1);
        }

        var result = CreateArray(type, level, values.Length);
        for (var i = 0; i < values.Length; i++)
            result.SetValue(values[i], i);

        return result;
    }

    private static object ParseLeaf(string text, TypeDescriptor type)
    {
        foreach (var separator in DimensionSeparators.Where(separator => !IsAllowedSeparator(type.Dimensions, separator)))
        {
            if (text.IndexOf(separator) >= 0)
                throw new FormatException($"Unexpected high-dimensional separator '{separator}' in {type.BaseType} array.");
        }

        return ParseScalar(text.Trim(), type.BaseType);
    }

    private static char SeparatorFor(int dimensions, int level)
    {
        if (dimensions is < 1 or > 3)
            throw new FormatException("Array dimensions must be between one and three.");
        return DimensionSeparators[dimensions - level - 1];
    }

    private static bool IsAllowedSeparator(int dimensions, char separator)
    {
        for (var level = 0; level < dimensions; level++)
        {
            if (SeparatorFor(dimensions, level) == separator)
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

    private static object ParseScalar(string text, string baseType)
    {
        if (text.Length == 0)
            throw new FormatException($"Empty {baseType} value.");

        try
        {
            return baseType.ToLowerInvariant() switch
            {
                "int" => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "long" => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "float" => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                "double" => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                "bool" => ParseBool(text),
                "string" => text,
                _ => text
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.", ex);
        }
    }

    private static bool ParseBool(string text) => text.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new FormatException($"Value '{text}' is invalid for type 'bool'.")
    };

    private static Type GetRuntimeType(string baseType) => baseType.ToLowerInvariant() switch
    {
        "int" => typeof(int),
        "long" => typeof(long),
        "float" => typeof(float),
        "double" => typeof(double),
        "bool" => typeof(bool),
        "string" => typeof(string),
        _ => typeof(string)
    };
}
