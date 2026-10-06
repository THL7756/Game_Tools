// 用途：读取、保存和恢复用户配置。
// 最近修改日期：2026-10-06

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

    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnityTableTool",
        "settings.json");

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
        }
        catch
        {
        }

        return CreateDefault();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
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
}
