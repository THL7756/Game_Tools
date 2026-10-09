// 用途：定义数据和代码导出的目录、目标、格式开关及数组分隔符选项。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed record ExportOptions
{
    public ExportOptions(
        string dataOutputDirectory,
        string codeOutputDirectory,
        bool generateCode = true,
        bool generateJson = true,
        bool generateBytes = true,
        ExportTarget targets = ExportTarget.Client)
        : this(
            dataOutputDirectory,
            dataOutputDirectory,
            codeOutputDirectory,
            codeOutputDirectory,
            generateCode,
            generateJson,
            generateBytes,
            targets,
            targets,
            ArraySeparatorOptions.Default)
    {
    }

    public ExportOptions(
        string clientDataOutputDirectory,
        string serverDataOutputDirectory,
        string clientCodeOutputDirectory,
        string serverCodeOutputDirectory,
        bool generateCode = true,
        bool generateJson = true,
        bool generateBytes = true,
        ExportTarget targets = ExportTarget.Client,
        ExportTarget codeTargets = ExportTarget.Client,
        ArraySeparatorOptions? arraySeparators = null)
    {
        ClientDataOutputDirectory = clientDataOutputDirectory;
        ServerDataOutputDirectory = serverDataOutputDirectory;
        ClientCodeOutputDirectory = clientCodeOutputDirectory;
        ServerCodeOutputDirectory = serverCodeOutputDirectory;
        GenerateCode = generateCode;
        GenerateJson = generateJson;
        GenerateBytes = generateBytes;
        Targets = targets;
        CodeTargets = codeTargets;
        ArraySeparators = arraySeparators ?? ArraySeparatorOptions.Default;
    }

    public string ClientDataOutputDirectory { get; }
    public string ServerDataOutputDirectory { get; }
    public string ClientCodeOutputDirectory { get; }
    public string ServerCodeOutputDirectory { get; }
    public bool GenerateCode { get; }
    public bool GenerateJson { get; }
    public bool GenerateBytes { get; }
    public ExportTarget Targets { get; }
    public ExportTarget CodeTargets { get; }
    public ArraySeparatorOptions ArraySeparators { get; }
}

public sealed record ExportResult(
    IReadOnlyList<string> Files,
    string SchemaHash);
