using TableTool.Core.Parsing;

namespace TableTool.Core.Models;

public enum FieldTarget
{
    Both,
    Client,
    Server
}

public sealed record FieldSchema(
    int SourceColumn,
    string Name,
    string Description,
    TypeDescriptor Type,
    FieldTarget Target,
    string? DefaultValue);
