// 用途：解析基础类型、数组类型和用户自定义 enum 类型声明。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

using Company.UnityTableRuntime.Shared;

namespace TableTool.Core.Parsing;

public sealed record TypeDescriptor(
    string BaseType,
    int Dimensions,
    IReadOnlyList<string>? EnumValues = null,
    bool IsValid = true,
    string? RawText = null)
{

    public static TypeDescriptor Parse(string text)
    {
        var parsed = RuntimeTypeDescriptor.Parse(text);
        return new TypeDescriptor(parsed.BaseType, parsed.Dimensions, parsed.EnumValues, parsed.IsValid, parsed.RawText);
    }

    public static TypeDescriptor Invalid(string text) =>
        new("unknown", 0, null, false, text);

    public bool IsEnum => BaseType.Equals("enum", StringComparison.OrdinalIgnoreCase);

    public string DisplayText => IsEnum
        ? $"enum({string.Join('|', EnumValues ?? [])})" + string.Concat(Enumerable.Repeat("()", Dimensions))
        : BaseType + string.Concat(Enumerable.Repeat("()", Dimensions));
}
