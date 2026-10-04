namespace TableTool.Core.Parsing;

public sealed record RawTableGrid(
    string SourceName,
    IReadOnlyList<IReadOnlyList<string?>> Rows);
