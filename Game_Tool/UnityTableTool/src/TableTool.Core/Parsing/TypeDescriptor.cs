namespace TableTool.Core.Parsing;

public sealed record TypeDescriptor(string BaseType, int Dimensions)
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

        var canonical = SupportedTypes.FirstOrDefault(type => type.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
            throw new FormatException($"Unknown type '{text}'.");

        return new TypeDescriptor(canonical, dimensions);
    }
}
