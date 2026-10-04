namespace TableTool.Core.Models;

public sealed record TableDocument(
    string SourceName,
    TableSchema Schema,
    IReadOnlyList<TableRow> Rows);
