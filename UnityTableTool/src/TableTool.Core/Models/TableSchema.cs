namespace TableTool.Core.Models;

public sealed record TableSchema(
    string Name,
    IReadOnlyList<FieldSchema> Fields,
    string PrimaryKey,
    bool IsSingleton = false,
    IReadOnlyList<FieldSchema>? ValidationFields = null)
{
    public IReadOnlyList<FieldSchema> AllFields =>
        ValidationFields is null ? Fields : Fields.Concat(ValidationFields).ToArray();
}
