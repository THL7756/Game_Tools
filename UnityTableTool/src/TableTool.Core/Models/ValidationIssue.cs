// 用途：承载配置表校验问题、严重级别和源表定位信息。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string SourceName,
    int? SourceRow = null,
    int? SourceColumn = null,
    string? FieldName = null,
    string? Key = null,
    string? Suggestion = null);
