// 用途：提供右上角语言选择和界面文本切换。
// 最近修改日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TableTool.Gui.Services;

public sealed record LanguageOption(
    string Code,
    string DisplayName,
    IReadOnlyDictionary<string, string>? Translations = null)
{
    public override string ToString() => DisplayName;
}

public static class LanguageManager
{
    private static readonly IReadOnlyDictionary<string, string> EnglishText =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["游戏开发工具箱"] = "Game Dev Toolkit",
            ["文件"] = "File",
            ["编辑"] = "Edit",
            ["查看"] = "View",
            ["使用文档"] = "Documentation",
            ["打开配置表目录"] = "Open table directory",
            ["打开文档目录"] = "Open documentation",
            ["重新扫描"] = "Rescan",
            ["退出"] = "Exit",
            ["最小化"] = "Minimize",
            ["最大化"] = "Maximize",
            ["关闭"] = "Close",
            ["撤销"] = "Undo",
            ["恢复"] = "Redo",
            ["复制"] = "Copy",
            ["剪切"] = "Cut",
            ["粘贴"] = "Paste",
            ["全选"] = "Select all",
            ["搜索"] = "Search",
            ["白天"] = "Light",
            ["晚上"] = "Dark",
            ["系统"] = "System",
            ["设置"] = "Settings",
            ["页面模式"] = "Theme",
            ["工作台"] = "Workbench",
            ["配置工具"] = "Table tool",
            ["项目助手"] = "Project assistant",
            ["素材工具"] = "Asset tool",
            ["音频工具"] = "Audio tool",
            ["提示词仓库"] = "Prompt library",
            ["刷新表列表"] = "Refresh tables",
            ["配置表目录"] = "Table directory",
            ["刷新内容"] = "Refresh content",
            ["打开 Excel"] = "Open in Excel",
            ["表问题"] = "Table issues",
            ["日志"] = "Log",
            ["输出范围"] = "Output targets",
            ["客户端"] = "Client",
            ["服务器"] = "Server",
            ["开始打表"] = "Build tables",
            ["确定"] = "OK"
        };

    private static readonly IReadOnlyDictionary<string, string> ChineseText = BuildChineseText();

    public static string CurrentCode { get; private set; } = "zh-CN";

    public static IReadOnlyList<LanguageOption> LoadOptions() =>
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English")
    ];

    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (string.Equals(CurrentCode, "en-US", StringComparison.OrdinalIgnoreCase))
            return EnglishText.TryGetValue(value, out var english) ? english : value;

        return ChineseText.TryGetValue(value, out var chinese) ? chinese : value;
    }

    public static string Format(string? template, params object[] args) =>
        string.Format(Text(template), args);

    public static void Apply(DependencyObject root, string code)
    {
        CurrentCode = code;
        ApplyElement(root, new HashSet<DependencyObject>());
    }

    private static void ApplyElement(DependencyObject element, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(element))
            return;

        switch (element)
        {
            case TextBlock textBlock
                when !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)
                    && !string.IsNullOrWhiteSpace(textBlock.Text):
                textBlock.Text = Text(ToChinese(textBlock.Text));
                break;
            case MenuItem menuItem when menuItem.Header is string header:
                menuItem.Header = Text(ToChinese(header));
                break;
            case ComboBoxItem comboBoxItem when comboBoxItem.Content is string content:
                comboBoxItem.Content = Text(ToChinese(content));
                break;
            case Button button when button.Content is string content:
                button.Content = Text(ToChinese(content));
                break;
            case Control control when control.ToolTip is string tip:
                control.ToolTip = Text(ToChinese(tip));
                break;
        }

        if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                ApplyElement(VisualTreeHelper.GetChild(element, index), visited);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(element))
        {
            if (child is DependencyObject dependencyChild)
                ApplyElement(dependencyChild, visited);
        }
    }

    private static string ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (ChineseText.ContainsKey(value))
            return value;

        return EnglishText.FirstOrDefault(pair => string.Equals(pair.Value, value, StringComparison.Ordinal)).Key
            ?? value;
    }

    private static IReadOnlyDictionary<string, string> BuildChineseText()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in EnglishText)
            result[pair.Value] = pair.Key;
        return result;
    }
}
