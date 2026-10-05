// 用途：汇总 Excel Diff 的输入数据和变更列表。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ExcelDiffResult
{
    public required string LeftPath { get; init; }

    public required string RightPath { get; init; }

    public required ExcelWorkbookData LeftWorkbook { get; init; }

    public required ExcelWorkbookData RightWorkbook { get; init; }

    public List<DiffEntry> Entries { get; init; } = [];

    public int AddedCount => Entries.Count(entry => entry.Kind == DiffKind.Added);

    public int RemovedCount => Entries.Count(entry => entry.Kind == DiffKind.Removed);

    public int ModifiedCount => Entries.Count(entry => entry.Kind == DiffKind.Modified);
}
