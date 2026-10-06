// 用途：承载工作台问题和打表日志的展示字段。
// 最近修改日期：2026-10-06

using System.Windows.Media;

namespace TableTool.Gui.Views;

public sealed record IssueDisplayItem(string Headline, string Detail);

public sealed class LogDisplayItem
{
    public LogDisplayItem(DateTime time, string level, string message)
    {
        TimeText = time.ToString("HH:mm:ss");
        Level = level;
        Message = message;
        LevelBrush = level switch
        {
            "PASS" => new SolidColorBrush(Color.FromRgb(84, 214, 160)),
            "WARN" => new SolidColorBrush(Color.FromRgb(231, 185, 94)),
            "FAIL" => new SolidColorBrush(Color.FromRgb(255, 123, 123)),
            _ => new SolidColorBrush(Color.FromRgb(147, 157, 173))
        };
    }

    public string TimeText { get; }
    public string Level { get; }
    public string Message { get; }
    public System.Windows.Media.Brush LevelBrush { get; }
}
