// 用途：保存工具路径、界面主题和字体缩放配置。
// 最近修改日期：2026-10-06

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
    public string BuildScriptPath { get; set; } = string.Empty;
    public AppearanceMode AppearanceMode { get; set; } = AppearanceMode.Dark;
    public string AccentColor { get; set; } = "#4D8DF7";
    public string BackgroundColor { get; set; } = "#17191D";
    public string ForegroundColor { get; set; } = "#E2E6ED";
    public string FontFamilyName { get; set; } = "Noto Sans SC";
    public double FontSize { get; set; } = 13;
    public double Zoom { get; set; } = 100;
    public List<string> Favorites { get; set; } = [];
    public List<string> RecentTables { get; set; } = [];

    public AppSettings Clone() => new()
    {
        ProjectRootDirectory = ProjectRootDirectory,
        TableDirectory = TableDirectory,
        ClientOutputDirectory = ClientOutputDirectory,
        ServerOutputDirectory = ServerOutputDirectory,
        BuildScriptPath = BuildScriptPath,
        AppearanceMode = AppearanceMode,
        AccentColor = AccentColor,
        BackgroundColor = BackgroundColor,
        ForegroundColor = ForegroundColor,
        FontFamilyName = FontFamilyName,
        FontSize = FontSize,
        Zoom = Zoom,
        Favorites = [.. Favorites],
        RecentTables = [.. RecentTables]
    };
}
