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
            targets)
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
        ExportTarget codeTargets = ExportTarget.Client)
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
}

public sealed record ExportResult(
    IReadOnlyList<string> Files,
    string SchemaHash);
