// 用途：读取、保存和恢复用户配置。
// 最近修改日期：2026-10-06
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
                    return settings;
            }

            // 兼容上一版保存在 LocalAppData 的配置，首次启动时迁移到工具目录。
            if (File.Exists(LegacySettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(LegacySettingsFilePath), JsonOptions);
                if (settings is not null)
                {
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
            BuildScriptPath = string.Empty
        };
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
