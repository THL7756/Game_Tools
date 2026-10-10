// 用途：承载工作台问题和工具日志的统一展示字段。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System.ComponentModel;
using TableTool.Core.Models;
using TableTool.Gui.Services;

namespace TableTool.Gui.Views;

public sealed record IssueDisplayItem(
    string Level,
    string Source,
    string SourceTooltip,
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
            issue.SourceName,
            ValidationIssueFormatter.Location(issue),
            issue.Code,
            ValidationIssueFormatter.Description(issue),
            ValidationIssueFormatter.Detail(issue),
            issue.Severity);
    }

    public static IssueDisplayItem CatalogError(string message) => new(
        "错误",
        "配置表目录",
        message,
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
    string Message,
    int DuplicateCount = 1)
{
    public string DuplicateCountText => DuplicateCount > 1 ? $"×{DuplicateCount}" : string.Empty;

    public string DisplayLevel => Level switch
    {
        "SUCCESS" => "成功",
        "WARNING" => "警告",
        "ERROR" => "错误",
        _ => "信息"
    };

    public string DisplayMessage => DuplicateCount > 1 ? $"{Message} ×{DuplicateCount}" : Message;

    public string DisplayLine => string.Join("  ", TimeText, DisplayLevel, Source, DisplayMessage);

    public string CopyText => DuplicateCount > 1
        ? $"{TimeText}\t{DisplayLevel}\t{Category}\t{Source}\t{Message}\t×{DuplicateCount}"
        : $"{TimeText}\t{DisplayLevel}\t{Category}\t{Source}\t{Message}";

    public static LogDisplayItem FromEntry(ToolLogEntry entry) => new(
        entry.TimeText,
        entry.Level,
        entry.Category,
        entry.Source,
        entry.Message);
}

public static class LogDisplayItems
{
    public static IReadOnlyList<LogDisplayItem> Collapse(IEnumerable<LogDisplayItem> entries) =>
        entries.GroupBy(entry => $"{entry.Category}\u001f{entry.Source}\u001f{entry.Message}", StringComparer.Ordinal)
            .Select(group => group.Last() with { DuplicateCount = group.Count() })
            .ToArray();
}

public sealed class LogCategoryFilterItem : INotifyPropertyChanged
{
    private int count;
    private int errorCount;
    private int warningCount;
    private bool isSelected;

    public LogCategoryFilterItem(string category, int count, bool isSelected)
    {
        Category = category;
        this.count = count;
        this.isSelected = isSelected;
    }

    public string Category { get; }
    public string Label => $"{Category} ({Count})";

    public string ErrorMarker => ErrorCount > 0 ? "●" : string.Empty;
    public string WarningMarker => WarningCount > 0 ? "●" : string.Empty;

    public int Count
    {
        get => count;
        set
        {
            if (count == value)
                return;
            count = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    public int ErrorCount
    {
        get => errorCount;
        set
        {
            if (errorCount == value)
                return;
            errorCount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMarker)));
        }
    }

    public int WarningCount
    {
        get => warningCount;
        set
        {
            if (warningCount == value)
                return;
            warningCount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningMarker)));
        }
    }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
