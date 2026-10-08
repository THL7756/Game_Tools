// 用途：保存配置表数组一到三维解析使用的分隔符。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public sealed record ArraySeparatorOptions(
    string Inner = "#",
    string Middle = "|",
    string Outer = ";")
{
    public static ArraySeparatorOptions Default { get; } = new();

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
        value.Length == 1 && !char.IsWhiteSpace(value[0]);
}
