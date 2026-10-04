using System.Text.Json;
using System.Text.Json.Serialization;
using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed class JsonTableExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string Export(TableDocument document)
    {
        var payload = new
        {
            formatVersion = 1,
            tableName = document.Schema.Name,
            schemaHash = SchemaHasher.Compute(document.Schema),
            isSingleton = document.Schema.IsSingleton,
            primaryKey = document.Schema.IsSingleton ? null : document.Schema.PrimaryKey,
            fields = document.Schema.Fields.Select(field => new
            {
                name = field.Name,
                type = field.Type.BaseType + new string('(', field.Type.Dimensions) + new string(')', field.Type.Dimensions),
                target = field.Target.ToString(),
                defaultValue = field.DefaultValue
            }).ToArray(),
            rows = document.Rows.Where(row => !row.IsTest).Select(row => row.RawValues.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal)).ToArray()
        };

        return JsonSerializer.Serialize(payload, Options);
    }
}
