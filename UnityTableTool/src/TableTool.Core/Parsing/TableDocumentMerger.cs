using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public static class TableDocumentMerger
{
    public static IReadOnlyList<TableDocument> Merge(IEnumerable<TableDocument> documents)
    {
        var ordered = documents.ToArray();
        var merged = new List<TableDocument>();
        foreach (var group in ordered.GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase))
        {
            var shards = group.ToArray();
            if (shards.Length == 1)
            {
                merged.Add(shards[0]);
                continue;
            }

            var schema = shards[0].Schema;
            foreach (var shard in shards.Skip(1))
            {
                if (!HasSameSchema(schema, shard.Schema))
                    throw new FormatException(
                        $"Table '{group.Key}' has incompatible schemas across sheets or files: '{shards[0].SourceName}' and '{shard.SourceName}'.");
            }

            var rows = shards.SelectMany(shard => shard.Rows).ToArray();
            var sourceName = string.Join(" + ", shards.Select(shard => shard.SourceName).Distinct(StringComparer.OrdinalIgnoreCase));
            merged.Add(new TableDocument(sourceName, schema, rows));
        }

        return merged;
    }

    private static bool HasSameSchema(TableSchema left, TableSchema right) =>
        left.IsSingleton == right.IsSingleton
        && string.Equals(left.PrimaryKey, right.PrimaryKey, StringComparison.OrdinalIgnoreCase)
        && left.Fields.Count == right.Fields.Count
        && left.Fields.Zip(right.Fields).All(pair => HasSameField(pair.First, pair.Second));

    private static bool HasSameField(FieldSchema left, FieldSchema right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal)
        && left.Type == right.Type
        && left.Target == right.Target
        && string.Equals(left.DefaultValue, right.DefaultValue, StringComparison.Ordinal);
}
