// 用途：兼容工具端的字段值解析入口，并委托给 Runtime Package 的共享实现。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using Company.UnityTableRuntime.Shared;
using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public static class ValueParser
{
    public static object Parse(
        string text,
        TypeDescriptor type,
        ArraySeparatorOptions? separators = null)
    {
        var sharedType = new RuntimeTypeDescriptor(
            type.BaseType,
            type.Dimensions,
            type.EnumValues,
            type.IsValid,
            type.RawText);
        var sharedSeparators = separators is null
            ? RuntimeArraySeparators.Default
            : new RuntimeArraySeparators(separators.Inner, separators.Middle, separators.Outer);
        return RuntimeValueParser.Parse(text, sharedType, sharedSeparators);
    }
}
