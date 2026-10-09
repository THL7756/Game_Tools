// 用途：承载工作台问题和工具日志的统一展示字段。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Gui.Services;

namespace TableTool.Gui.Views;

public sealed record IssueDisplayItem(
    string Level,
    string Source,
    string Location,
    string Code,
    string Message,
    string Detail,
    ValidationSeverity Severity)
{
    public string CopyText => $"{Level}\t{Source}\t{Location}\t{Code}\t{Message}\t{Detail}";

    public static IssueDisplayItem FromIssue(ValidationIssue issue)
    {
        return new IssueDisplayItem(
            ValidationIssueFormatter.Level(issue.Severity),
            ValidationIssueFormatter.Source(issue),
            ValidationIssueFormatter.Location(issue),
            issue.Code,
            ValidationIssueFormatter.Description(issue),
            ValidationIssueFormatter.Detail(issue),
            issue.Severity);
    }

    public static IssueDisplayItem CatalogError(string message) => new(
        "错误",
        "配置表目录",
        "读取",
        "CATALOG_READ_FAILED",
        message,
        message,
        ValidationSeverity.Error);
}

public sealed record LogDisplayItem(
    string TimeText,
    string Level,
    string Category,
    string Source,
    string Message)
{
    public string CopyText => $"{TimeText}\t{Level}\t{Category}\t{Source}\t{Message}";

    public static LogDisplayItem FromEntry(ToolLogEntry entry) => new(
        entry.TimeText,
        entry.Level,
        entry.Category,
        entry.Source,
        entry.Message);
}

public sealed class LogGroupDisplayItem
{
    public LogGroupDisplayItem(string category, IReadOnlyList<LogDisplayItem> entries, bool isExpanded)
    {
        Category = category;
        Entries = entries;
        IsExpanded = isExpanded;
    }

    public string Category { get; }
    public IReadOnlyList<LogDisplayItem> Entries { get; }
    public bool IsExpanded { get; set; }
    public string ToggleGlyph => IsExpanded ? "▾" : "▸";
    public int Count => Entries.Count;
    public int ErrorCount => Entries.Count(item => item.Level == "ERROR");
    public int WarningCount => Entries.Count(item => item.Level == "WARNING");
}

public sealed record LogCategoryFilterItem(string Category, int Count)
{
    public string Label => $"{Category} ({Count})";
}
