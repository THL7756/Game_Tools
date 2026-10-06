// 用途：加载可扩展语言包，并把界面文本切换到当前语言。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.IO;
using System.Text.Json;
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
    private static readonly Dictionary<string, string> EnglishText = new(StringComparer.Ordinal)
    {
        ["游戏开发工具箱"] = "Game Dev Toolkit",
        ["文件"] = "File", ["编辑"] = "Edit", ["查看"] = "View", ["使用文档"] = "Documentation",
        ["打开配置表目录"] = "Open table directory", ["打开文档目录"] = "Open documentation", ["重新扫描"] = "Rescan", ["退出"] = "Exit",
        ["最小化"] = "Minimize", ["最大化"] = "Maximize", ["关闭"] = "Close",
        ["撤销"] = "Undo", ["恢复"] = "Redo", ["复制"] = "Copy", ["剪切"] = "Cut", ["粘贴"] = "Paste",
        ["全选"] = "Select all", ["搜索"] = "Search", ["清空选择"] = "Clear selection",
        ["白天"] = "Light", ["晚上"] = "Dark", ["跟随系统"] = "System", ["设置"] = "Settings",
        ["页面模式"] = "Theme", ["工作台"] = "Workbench", ["配置工具"] = "Table tool", ["项目助手"] = "Project assistant",
        ["素材工具"] = "Asset tool", ["音频工具"] = "Audio tool", ["提示词仓库"] = "Prompt library",
        ["配置表预览"] = "Table preview", ["刷新表列表"] = "Refresh tables", ["收藏表"] = "Favorite tables",
        ["最近表"] = "Recent tables", ["所有表"] = "All tables", ["搜索表名或文件名"] = "Search table or file",
        ["表问题"] = "Table issues", ["构建日志"] = "Build log",
        ["复制日志"] = "Copy log", ["清空"] = "Clear", ["只读预览"] = "Read-only preview",
        ["校验通过，可直接输出"] = "Validation passed; ready to export", ["模块待补充"] = "Module coming soon",
        ["待补充"] = "Coming soon", ["设置 / 视觉风格"] = "Settings / Appearance", ["视觉风格"] = "Appearance",
        ["项目管理、路径检查和常用操作将在这里提供。"] = "Project management, path checks and common actions will be provided here.",
        ["素材浏览、整理和批处理功能待补充。"] = "Asset browsing, organization and batch processing are coming soon.",
        ["音频检查、转换和预览功能待补充。"] = "Audio inspection, conversion and preview are coming soon.",
        ["提示词分类、搜索和复用功能待补充。"] = "Prompt categorization, search and reuse are coming soon.",
        ["调整工作台外观，让开发环境更顺手。"] = "Adjust the workbench appearance for a smoother workflow.",
        ["与顶部模式切换保持同步"] = "Synchronized with the top theme switch",
        ["使用预设主题，或自定义下方颜色。"] = "Use a preset theme or customize the colors below.",
        ["用于选中状态、主要按钮和链接。"] = "Used for selections, primary buttons and links.",
        ["工作区的基础背景颜色。"] = "Base background color for the workspace.",
        ["正文、表格数据与主要标签的文字颜色。"] = "Text color for body content, table data and labels.",
        ["界面字体、字号、缩放比例"] = "Interface font, size and zoom",
        ["代码与数值使用 JetBrains Mono"] = "Code and values use JetBrains Mono",
        ["管理配置表、输出目录与打表脚本。此处设置仅对当前项目生效。"] = "Manage tables, output directories and build scripts for this project.",
        ["所有相对路径的基准目录"] = "Base directory for relative paths",
        ["读取 .xlsx、.xls、.csv、.tsv 配置表"] = "Reads .xlsx, .xls, .csv and .tsv tables",
        ["客户端勾选时，将 JSON 文件写入此目录"] = "Client JSON files are written here",
        ["服务器勾选时，将 JSON 文件写入此目录"] = "Server JSON files are written here",
        ["可选；当前打表由工具内置 JSON 和 C# 导出完成"] = "Optional; the built-in exporter generates JSON and C#",
        ["版本 2.4.0"] = "Version 2.4.0",
        ["配置表扫描、校验与 JSON 打表工具。"] = "Table scanning, validation and JSON build tool.",
        ["UTF-8 · v2.4.0"] = "UTF-8 · v2.4.0",
        ["选择目录"] = "Choose directory", ["选择打表脚本"] = "Choose build script",
        ["项目路径"] = "Project paths", ["快捷键"] = "Shortcuts", ["关于"] = "About",
        ["保存设置"] = "Save settings", ["恢复默认"] = "Restore defaults", ["搜索表名"] = "Search tables",
        ["滚轮步长"] = "Wheel step",
        ["上下滚轮步长"] = "Vertical wheel step",
        ["左右滚轮步长"] = "Horizontal wheel step",
        ["主题"] = "Theme", ["强调色"] = "Accent color", ["背景色"] = "Background color", ["前景色"] = "Text color",
        ["字体设置"] = "Font settings", ["项目根目录"] = "Project root", ["配置表目录"] = "Table directory",
        ["客户端输出目录"] = "Client output", ["服务器输出目录"] = "Server output", ["打表脚本"] = "Build script",
        ["浏览…"] = "Browse…", ["开始打表"] = "Build tables", ["输出范围"] = "Output targets",
        ["客户端"] = "Client", ["服务器"] = "Server", ["打表"] = "Build", ["打表日志"] = "Build log", ["最近一次"] = "Latest",
        ["清空日志"] = "Clear log", ["打开源文件"] = "Open source file", ["在 Excel 中打开"] = "Open in Excel", ["刷新内容"] = "Refresh content",
        ["收起"] = "Collapse", ["展开"] = "Expand",
        ["打表成功"] = "Build succeeded", ["打表失败"] = "Build failed", ["确定"] = "OK",
        ["选择强调色"] = "Choose accent color", ["选择背景色"] = "Choose background color", ["选择前景色"] = "Choose text color",
        ["例如 Ctrl+Shift+K"] = "For example Ctrl+Shift+K",
        ["打表成功，共输出 {0} 个文件。"] = "Build succeeded: {0} output file(s).",
        ["打表失败，请展开日志查看详细信息。"] = "Build failed; expand the log for details.",
        ["打表未完成，请检查日志"] = "Build incomplete; check the log.",
        ["请先选择需要刷新的表文件。"] = "Select a table file to refresh first.",
        ["部分文件读取失败，本次打表使用已保留的内存数据。"] = "Some files could not be read; this build uses retained in-memory data.",
        ["收藏"] = "Favorite", ["里程碑计划"] = "Milestone plan",
        ["已同步 {0}"] = "Synced {0}", ["收藏表 {0}"] = "Favorite tables {0}",
        ["修改于 {0}"] = "Modified {0}",
        ["最近表 {0}"] = "Recent tables {0}", ["所有表 {0}"] = "All tables {0}",
        ["已勾选 {0} / {1}"] = "Selected {0} / {1}", ["只读预览 · {0} 个字段"] = "Read-only preview · {0} fields",
        ["{0} 错误 / {1} 警告"] = "{0} errors / {1} warnings",
        ["0 错误"] = "0 errors", ["0 警告"] = "0 warnings",
        ["错误"] = "Error", ["警告"] = "Warning",
        ["表读取失败"] = "Table read failed",
        ["存在无法读取的表，已停止打表"] = "Some tables could not be read; build stopped",
        ["{0} 条警告不阻断输出"] = "{0} warning(s) do not block export",
        ["打表完成：{0} 个输出文件"] = "Build complete: {0} output file(s)",
        ["0 错误 / 0 警告"] = "0 errors / 0 warnings"
    };

    private static readonly IReadOnlyDictionary<string, string> ChineseText = BuildChineseText();
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> AdditionalTranslations = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<LanguageOption> LoadOptions()
    {
        AdditionalTranslations.Clear();
        var directory = Path.Combine(AppContext.BaseDirectory, "Languages");
        EnsureBuiltInLanguageFiles(directory);
        if (!Directory.Exists(directory))
            return [new("zh-CN", "简体中文"), new("en-US", "English")];

        var options = new List<LanguageOption>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var option = JsonSerializer.Deserialize<LanguageOption>(File.ReadAllText(path), JsonOptions);
                if (option is not null && !string.IsNullOrWhiteSpace(option.Code) && !string.IsNullOrWhiteSpace(option.DisplayName))
                {
                    option = AttachBuiltInTranslations(option);
                    options.Add(option);
                    if (option.Translations is { Count: > 0 })
                        AdditionalTranslations[option.Code] = option.Translations;
                }
            }
            catch (JsonException)
            {
                // 单个语言包损坏时跳过，确保工具仍可启动。
            }
        }

        return options.Count == 0
            ? [new("zh-CN", "简体中文"), new("en-US", "English")]
            : options.OrderBy(option => option.Code, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static LanguageOption AttachBuiltInTranslations(LanguageOption option)
    {
        if (option.Translations is { Count: > 0 })
            return option;

        if (string.Equals(option.Code, "en-US", StringComparison.OrdinalIgnoreCase))
            return option with { Translations = EnglishText };

        if (string.Equals(option.Code, "zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            var chinese = EnglishText.Keys.ToDictionary(key => key, key => key, StringComparer.Ordinal);
            return option with { Translations = chinese };
        }

        return option;
    }

    private static void EnsureBuiltInLanguageFiles(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var chinese = EnglishText.Keys.ToDictionary(key => key, key => key, StringComparer.Ordinal);
            WriteLanguageFileIfMissing(directory, "zh-CN.json", new LanguageOption("zh-CN", "简体中文", chinese));
            WriteLanguageFileIfMissing(directory, "en-US.json", new LanguageOption("en-US", "English", EnglishText));
        }
        catch
        {
            // 只读安装目录仍可使用内置中英文回退，不影响启动。
        }
    }

    private static void WriteLanguageFileIfMissing(string directory, string fileName, LanguageOption option)
    {
        var path = Path.Combine(directory, fileName);
        var hasTranslations = false;
        if (File.Exists(path))
        {
            try
            {
                hasTranslations = JsonSerializer.Deserialize<LanguageOption>(File.ReadAllText(path), JsonOptions)?.Translations is { Count: > 0 };
            }
            catch (JsonException)
            {
                // 损坏的内置语言包会在启动时恢复为完整默认包。
            }
        }

        if (!hasTranslations)
            File.WriteAllText(path, JsonSerializer.Serialize(option, JsonOptions));
    }

    public static string CurrentCode { get; private set; } = "zh-CN";

    private static IReadOnlyDictionary<string, string> BuildChineseText()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in EnglishText)
            result[pair.Value] = pair.Key;
        return result;
    }

    // WPF 模板在生成和切换页签时可能短暂提供 null 文本；语言刷新必须保持幂等，不能让空键进入字典。
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        return string.Equals(CurrentCode, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? ChineseText.TryGetValue(value, out var chinese) ? chinese : value
            : string.Equals(CurrentCode, "en-US", StringComparison.OrdinalIgnoreCase)
                ? EnglishText.TryGetValue(value, out var english) ? english : value
                : AdditionalTranslations.TryGetValue(CurrentCode, out var translations)
                    && translations.TryGetValue(value, out var translated) ? translated : value;
    }

    public static string Format(string? template, params object[] args) => string.Format(Text(template), args);

    public static void Apply(DependencyObject root, string code)
    {
        CurrentCode = code;
        try
        {
            ApplyElement(root, new HashSet<DependencyObject>());
        }
        catch (Exception error)
        {
            // 语言切换可能发生在控件模板尚未生成完成的时机；记录后保留当前窗口。
            TableTool.Gui.App.LogUiError("遍历语言切换控件失败", error);
        }
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
            case ComboBoxItem comboBoxItem when comboBoxItem.Content is string comboContent:
                comboBoxItem.Content = Text(ToChinese(comboContent));
                break;
            case Button button when button.Content is string buttonContent:
                button.Content = Text(ToChinese(buttonContent));
                break;
            case Control control when control.ToolTip is string tip:
                control.ToolTip = Text(ToChinese(tip));
                break;
        }

        if (element is System.Windows.Media.Visual || element is System.Windows.Media.Media3D.Visual3D)
        {
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            {
                try
                {
                    ApplyElement(System.Windows.Media.VisualTreeHelper.GetChild(element, index), visited);
                }
                catch (Exception error)
                {
                    // 模板切换时子节点可能瞬间失效，跳过该节点即可。
                    TableTool.Gui.App.LogUiError("更新语言控件文本失败", error);
                }
            }
        }

        try
        {
            foreach (var child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyChild)
                    ApplyElement(dependencyChild, visited);
            }
        }
        catch (Exception error)
        {
            TableTool.Gui.App.LogUiError("更新语言逻辑节点失败", error);
        }
    }

    private static string ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        return EnglishText.ContainsKey(value) ? value
            : ChineseText.TryGetValue(value, out var chinese) ? chinese
            : AdditionalTranslations.Values.SelectMany(dictionary => dictionary)
                .FirstOrDefault(pair => string.Equals(pair.Value, value, StringComparison.Ordinal)) is { } translated
                    ? translated.Key
                    : value;
    }
}
