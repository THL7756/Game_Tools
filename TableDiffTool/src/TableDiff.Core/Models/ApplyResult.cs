// 用途：返回双向替换的执行结果。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ApplyResult
{
    public required string TargetPath { get; init; }

    public required string BackupPath { get; init; }

    public int AppliedCount { get; init; }

    public List<string> Conflicts { get; init; } = [];

    public bool Succeeded => Conflicts.Count == 0;
}
