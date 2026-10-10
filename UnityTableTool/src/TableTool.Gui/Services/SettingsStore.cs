// 用途：读写工具设置，并校准路径、窗口尺寸和分隔符配置。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System.IO;
using System.Text.Json;
using TableTool.Gui.Models;

namespace TableTool.Gui.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string LegacySettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnityTableTool",
        "settings.json");

    public static string DataDirectoryPath => GetDataDirectory();

    public static string SettingsFilePath => Path.Combine(DataDirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath), JsonOptions);
                if (settings is not null)
                {
                    Normalize(settings);
                    Save(settings);
                    return settings;
                }
            }

            // 兼容上一版保存在 LocalAppData 的配置，首次启动时迁移到工具目录。
            if (File.Exists(LegacySettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(LegacySettingsFilePath), JsonOptions);
                if (settings is not null)
                {
                    Normalize(settings);
                    Save(settings);
                    return settings;
                }
            }
        }
        catch
        {
        }

        return CreateDefault();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DataDirectoryPath);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // 安装目录没有写权限时回退到 LocalAppData，确保设置仍能保存。
            Directory.CreateDirectory(Path.GetDirectoryName(LegacySettingsFilePath)!);
            File.WriteAllText(LegacySettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }

    public static AppSettings CreateDefault()
    {
        var root = FindProjectRoot();
        return new AppSettings
        {
            ProjectRootDirectory = root,
            TableDirectory = Path.Combine(root, "Data"),
            ClientOutputDirectory = Path.Combine(root, "Data_c"),
            ServerOutputDirectory = Path.Combine(root, "Data_s"),
            ClientCodeOutputDirectory = Path.Combine(root, "Code_c"),
            ServerCodeOutputDirectory = Path.Combine(root, "Code_s"),
            UnityRuntimePackageDirectory = Path.Combine(root, "UnityRuntimePackage"),
            BuildScriptPath = string.Empty
        };
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var root = string.IsNullOrWhiteSpace(settings.ProjectRootDirectory)
            ? FindProjectRoot()
            : settings.ProjectRootDirectory;
        if (string.IsNullOrWhiteSpace(settings.ClientCodeOutputDirectory))
            settings.ClientCodeOutputDirectory = Path.Combine(root, "Code_c");
        if (string.IsNullOrWhiteSpace(settings.ServerCodeOutputDirectory))
            settings.ServerCodeOutputDirectory = Path.Combine(root, "Code_s");
        if (string.IsNullOrWhiteSpace(settings.UnityRuntimePackageDirectory))
            settings.UnityRuntimePackageDirectory = Path.Combine(root, "UnityRuntimePackage");
        settings.VerticalWheelScrollStep = settings.VerticalWheelScrollStep is > 0 and <= 10 ? settings.VerticalWheelScrollStep : 3;
        settings.HorizontalWheelScrollStep = settings.HorizontalWheelScrollStep is > 0 and <= 10 ? settings.HorizontalWheelScrollStep : 3;
        settings.MainSidebarWidth = ClampLayoutSize(settings.MainSidebarWidth, 120, 104, 240);
        settings.WorkbenchTableListWidth = ClampLayoutSize(settings.WorkbenchTableListWidth, 215, 180, 360);
        settings.WorkbenchDetailHeight = ClampLayoutSize(settings.WorkbenchDetailHeight, 260, 120, 800);
        settings.WorkbenchIssuesHeight = ClampLayoutSize(settings.WorkbenchIssuesHeight, 220, 120, 800);
        settings.SettingsSidebarWidth = ClampLayoutSize(settings.SettingsSidebarWidth, 155, 140, 280);
        settings.LogFilterLevels = NormalizeList(settings.LogFilterLevels, ToolLogCategories.Levels);
        settings.LogFilterCategories = NormalizeList(settings.LogFilterCategories, ToolLogCategories.All);
        settings.LogCollapsedCategories = (settings.LogCollapsedCategories ?? [])
            .Where(ToolLogCategories.All.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        settings.LogPanelHeight = double.IsNaN(settings.LogPanelHeight) || double.IsInfinity(settings.LogPanelHeight)
            ? 320
            : Math.Clamp(settings.LogPanelHeight, 220, 560);
        if (double.IsNaN(settings.WindowWidth) || double.IsInfinity(settings.WindowWidth))
            settings.WindowWidth = 1024;
        if (double.IsNaN(settings.WindowHeight) || double.IsInfinity(settings.WindowHeight))
            settings.WindowHeight = 680;
        settings.WindowWidth = Math.Max(760, settings.WindowWidth);
        settings.WindowHeight = Math.Max(520, settings.WindowHeight);
        if (!new TableTool.Core.Models.ArraySeparatorOptions(
                settings.ArrayInnerSeparator,
                settings.ArrayMiddleSeparator,
                settings.ArrayOuterSeparator).IsValid)
        {
            settings.ArrayInnerSeparator = "#";
            settings.ArrayMiddleSeparator = "|";
            settings.ArrayOuterSeparator = ";";
        }
        return settings;
    }

    private static double ClampLayoutSize(double value, double fallback, double minimum, double maximum) =>
        double.IsNaN(value) || double.IsInfinity(value) || value <= 0 ? fallback : Math.Clamp(value, minimum, maximum);

    private static List<string> NormalizeList(IEnumerable<string>? values, IReadOnlyList<string> allowed)
    {
        var normalized = (values ?? [])
            .Where(value => allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Select(value => allowed.First(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return normalized.Count == 0 ? [.. allowed] : normalized;
    }

    private static string FindProjectRoot()
    {
        foreach (var candidate in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "Data")))
                    return current.FullName;
                current = current.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string GetDataDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ToolData");
        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityTableTool");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
