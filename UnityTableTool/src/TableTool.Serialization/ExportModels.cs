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
        : this(dataOutputDirectory, dataOutputDirectory, codeOutputDirectory, generateCode, generateJson, generateBytes, targets)
    {
    }

    public ExportOptions(
        string clientDataOutputDirectory,
        string serverDataOutputDirectory,
        string codeOutputDirectory,
        bool generateCode = true,
        bool generateJson = true,
        bool generateBytes = true,
        ExportTarget targets = ExportTarget.Client)
    {
        ClientDataOutputDirectory = clientDataOutputDirectory;
        ServerDataOutputDirectory = serverDataOutputDirectory;
        CodeOutputDirectory = codeOutputDirectory;
        GenerateCode = generateCode;
        GenerateJson = generateJson;
        GenerateBytes = generateBytes;
        Targets = targets;
    }

    public string ClientDataOutputDirectory { get; }
    public string ServerDataOutputDirectory { get; }
    public string CodeOutputDirectory { get; }
    public bool GenerateCode { get; }
    public bool GenerateJson { get; }
    public bool GenerateBytes { get; }
    public ExportTarget Targets { get; }
}

public sealed record ExportResult(
    IReadOnlyList<string> Files,
    string SchemaHash);
