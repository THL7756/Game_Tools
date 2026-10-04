namespace TableTool.Core.Models;

public sealed record TableRow(
    int SourceRow,
    bool IsTest,
    IReadOnlyDictionary<string, string?> RawValues);
