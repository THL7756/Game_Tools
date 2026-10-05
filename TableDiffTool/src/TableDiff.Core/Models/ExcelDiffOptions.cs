// 用途：保存 Excel Diff 的比较选项。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ExcelDiffOptions
{
    public string? KeyColumn { get; init; }

    public bool CaseSensitive { get; init; }
}
