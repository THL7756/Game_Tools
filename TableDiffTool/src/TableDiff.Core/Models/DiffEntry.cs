// 用途：表示一个工作表、行或单元格级别的变更。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class DiffEntry
{
    public required string SheetName { get; init; }

    public required string KeyColumn { get; init; }

    public required string RowKey { get; init; }

    public required string ColumnName { get; init; }

    public required DiffKind Kind { get; init; }

    public string? LeftValue { get; init; }

    public string? RightValue { get; init; }

    public Dictionary<string, string?> LeftRow { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, string?> RightRow { get; init; } = new(StringComparer.Ordinal);

    public string Id => $"{SheetName}|{RowKey}|{ColumnName}|{Kind}";
}
