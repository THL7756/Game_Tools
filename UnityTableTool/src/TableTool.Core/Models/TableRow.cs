// 用途：保存配置表数据行、原始单元格值和来源坐标。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public sealed record TableRow(
    int SourceRow,
    bool IsTest,
    IReadOnlyDictionary<string, string?> RawValues,
    string? SourceName = null);
