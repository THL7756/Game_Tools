// 用途：保存一个正式配置表 Sheet 的来源、结构、数据和解析诊断。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public sealed record TableDocument(
    string SourceName,
    TableSchema Schema,
    IReadOnlyList<TableRow> Rows,
    IReadOnlyList<ValidationIssue>? ParseIssues = null);
