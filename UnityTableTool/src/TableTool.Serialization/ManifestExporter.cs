using System.Text.Json;
using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed class ManifestExporter
{
    public string Export(TableDocument document, IEnumerable<string> formats)
    {
        return JsonSerializer.Serialize(new
        {
            tableName = document.Schema.Name,
            isSingleton = document.Schema.IsSingleton,
            schemaHash = SchemaHasher.Compute(document.Schema),
            rowCount = document.Rows.Count(row => !row.IsTest),
            formats = formats.OrderBy(format => format, StringComparer.Ordinal).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
