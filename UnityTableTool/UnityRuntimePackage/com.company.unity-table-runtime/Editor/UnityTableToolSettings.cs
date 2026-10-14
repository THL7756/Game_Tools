// 用途：保存 Unity Editor 轻量打表工具的源表、输出目录和校验设置。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

#nullable enable

using System.IO;
using TableTool.Core.Models;
using UnityEngine;

namespace Company.UnityTableTool.Editor;

[CreateAssetMenu(
    fileName = "UnityTableToolSettings",
    menuName = "UnityTableTool/Table Tool Settings")]
public sealed class UnityTableToolSettings : ScriptableObject
{
    [Header("源表")]
    [Tooltip("相对于 Unity 项目根目录的源表目录，也可以填写绝对路径。")]
    public string tableDirectory = "Assets/Tables";

    [Header("数据输出")]
    public string clientDataDirectory = "Assets/Resources/Tables";
    public string serverDataDirectory = "../TableDataServer";

    [Header("代码输出")]
    public string clientCodeDirectory = "Assets/Scripts/GeneratedTables";
    public string serverCodeDirectory = "../TableCodeServer";

    [Header("输出目标")]
    public bool generateClientData = true;
    public bool generateServerData;
    public bool generateClientCode = true;
    public bool generateServerCode;

    [Header("数组分隔符")]
    public string arrayInnerSeparator = "#";
    public string arrayMiddleSeparator = "|";
    public string arrayOuterSeparator = ";";

    public ExportTarget DataTargets =>
        (generateClientData ? ExportTarget.Client : ExportTarget.None)
        | (generateServerData ? ExportTarget.Server : ExportTarget.None);

    public ExportTarget CodeTargets =>
        (generateClientCode ? ExportTarget.Client : ExportTarget.None)
        | (generateServerCode ? ExportTarget.Server : ExportTarget.None);

    public ArraySeparatorOptions ArraySeparators => new(
        arrayInnerSeparator,
        arrayMiddleSeparator,
        arrayOuterSeparator);

    public string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return Path.GetFullPath(configuredPath);

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, configuredPath));
    }
}
