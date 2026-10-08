// 用途：解析基础类型、数组类型和用户自定义 enum 类型声明。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Parsing;

public sealed record TypeDescriptor(
    string BaseType,
    int Dimensions,
    IReadOnlyList<string>? EnumValues = null,
    bool IsValid = true,
    string? RawText = null)
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "long", "float", "double", "bool", "string",
        "Vector2", "Vector3", "Vector4", "Color", "Quaternion"
    };

    public static TypeDescriptor Parse(string text)
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
            var values = content
                .Split('|', StringSplitOptions.TrimEntries)
                .Where(value => value.Length > 0)
                .ToArray();
            if (values.Length == 0
                || values.Length != content.Split('|').Length
                || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new FormatException($"Enum type '{text}' must contain unique non-empty values separated by '|'.");

            return new TypeDescriptor("enum", dimensions, values);
        }

        var canonical = SupportedTypes.FirstOrDefault(type => type.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            throw new FormatException($"Unknown type '{text}'.");

        return new TypeDescriptor(canonical, dimensions);
    }

    public static TypeDescriptor Invalid(string text) =>
        new("unknown", 0, null, false, text);

    public bool IsEnum => BaseType.Equals("enum", StringComparison.OrdinalIgnoreCase);

    public string DisplayText => IsEnum
        ? $"enum({string.Join('|', EnumValues ?? [])})" + string.Concat(Enumerable.Repeat("()", Dimensions))
        : BaseType + string.Concat(Enumerable.Repeat("()", Dimensions));
}
