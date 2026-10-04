namespace TableTool.Core.Models;

public sealed record TableSchema(
    string Name,
    IReadOnlyList<FieldSchema> Fields,
    string PrimaryKey,
    bool IsSingleton = false);
