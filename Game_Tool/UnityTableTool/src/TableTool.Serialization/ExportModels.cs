namespace TableTool.Serialization;

public sealed record ExportOptions(
    string OutputDirectory,
    bool GenerateCode = true,
    bool GenerateJson = true,
    bool GenerateBytes = true,
    bool GenerateManifest = true);

public sealed record ExportResult(
    IReadOnlyList<string> Files,
    string SchemaHash);
