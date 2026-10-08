// 用途：返回表解析文档、正式性状态以及可定位的解析诊断。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public sealed record TableParseResult(
    TableDocument? Document,
    IReadOnlyList<ValidationIssue> Issues,
    bool IsFormal)
{
    public static TableParseResult Informal(string sourceName, string message) => new(
        null,
        [new ValidationIssue(
            "TABLE_NOT_FORMAL",
            ValidationSeverity.Info,
            message,
            sourceName)],
        false);
}
