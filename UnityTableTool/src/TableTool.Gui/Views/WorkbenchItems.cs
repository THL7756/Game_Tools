// 用途：承载工作台问题和打表日志的展示字段。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;

namespace TableTool.Gui.Views;

public sealed record IssueDisplayItem(string Headline, string Detail, ValidationSeverity Severity);

public sealed class LogDisplayItem
{
    public LogDisplayItem(DateTime time, string level, string message)
    {
        TimeText = time.ToString("HH:mm:ss");
        Level = level;
        Message = message;
    }

    public string TimeText { get; }
    public string Level { get; }
    public string Message { get; }
}
