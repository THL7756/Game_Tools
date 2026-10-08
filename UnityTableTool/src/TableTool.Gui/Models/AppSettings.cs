// 用途：保存工具路径、界面主题、字体缩放、快捷键和滚动配置。
// 最近修改日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;

namespace TableTool.Gui.Models;

public enum AppearanceMode
{
    Light,
    Dark,
    System
}

public sealed class AppSettings
{
    public string ProjectRootDirectory { get; set; } = string.Empty;
    public string TableDirectory { get; set; } = string.Empty;
    public string ClientOutputDirectory { get; set; } = string.Empty;
    public string ServerOutputDirectory { get; set; } = string.Empty;
    public string ClientCodeOutputDirectory { get; set; } = string.Empty;
    public string ServerCodeOutputDirectory { get; set; } = string.Empty;
    public string BuildScriptPath { get; set; } = string.Empty;
    public AppearanceMode AppearanceMode { get; set; } = AppearanceMode.Dark;
    public string AccentColor { get; set; } = "#4D8DF7";
    public string BackgroundColor { get; set; } = "#17191D";
    public string ForegroundColor { get; set; } = "#E2E6ED";
    public string FontFamilyName { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 13;
    public double Zoom { get; set; } = 100;
    public string Language { get; set; } = "zh-CN";
    public string BuildShortcut { get; set; } = "Ctrl+B";
    public string RefreshShortcut { get; set; } = "Ctrl+R";
    public int VerticalWheelScrollStep { get; set; } = 12;
    public int HorizontalWheelScrollStep { get; set; } = 12;
    public ExportTarget OutputTargets { get; set; } = ExportTarget.Both;
    public ExportTarget CodeTargets { get; set; } = ExportTarget.Both;
    public List<string> Favorites { get; set; } = [];
    public List<string> RecentTables { get; set; } = [];

    public AppSettings Clone() => new()
    {
        ProjectRootDirectory = ProjectRootDirectory,
        TableDirectory = TableDirectory,
        ClientOutputDirectory = ClientOutputDirectory,
        ServerOutputDirectory = ServerOutputDirectory,
        ClientCodeOutputDirectory = ClientCodeOutputDirectory,
        ServerCodeOutputDirectory = ServerCodeOutputDirectory,
        BuildScriptPath = BuildScriptPath,
        AppearanceMode = AppearanceMode,
        AccentColor = AccentColor,
        BackgroundColor = BackgroundColor,
        ForegroundColor = ForegroundColor,
        FontFamilyName = FontFamilyName,
        FontSize = FontSize,
        Zoom = Zoom,
        Language = Language,
        BuildShortcut = BuildShortcut,
        RefreshShortcut = RefreshShortcut,
        VerticalWheelScrollStep = VerticalWheelScrollStep,
        HorizontalWheelScrollStep = HorizontalWheelScrollStep,
        OutputTargets = OutputTargets,
        CodeTargets = CodeTargets,
        Favorites = [.. Favorites],
        RecentTables = [.. RecentTables]
    };
}
