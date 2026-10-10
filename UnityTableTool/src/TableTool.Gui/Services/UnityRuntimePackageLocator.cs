// 用途：解析并校验 Unity Runtime Package 的共享校验代码目录。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Gui.Models;

namespace TableTool.Gui.Services;

public static class UnityRuntimePackageLocator
{
    private const string PackageName = "com.company.unity-table-runtime";

    public static string GetPackageDirectory(AppSettings settings)
    {
        var configured = settings.UnityRuntimePackageDirectory;
        if (string.IsNullOrWhiteSpace(configured))
            configured = Path.Combine(settings.ProjectRootDirectory, "UnityRuntimePackage");
        return Path.GetFullPath(configured);
    }

    public static string GetSharedSourceDirectory(AppSettings settings) =>
        Path.Combine(GetPackageDirectory(settings), PackageName, "Runtime", "Shared");

    public static string? Validate(AppSettings settings)
    {
        var packageDirectory = GetPackageDirectory(settings);
        var sharedDirectory = GetSharedSourceDirectory(settings);
        if (!Directory.Exists(packageDirectory))
            return $"Unity包目录不存在：{packageDirectory}";
        if (!Directory.Exists(sharedDirectory))
            return $"Unity包缺少共享校验代码目录：{sharedDirectory}";
        return null;
    }
}
