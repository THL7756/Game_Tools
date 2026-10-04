using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public sealed class TableSchemaParser
{
    private static readonly string[] HeaderLabels =
    [
        "字段说明", "说明", "字段类型", "类型", "字段名", "名称", "客户端服务器", "客户端/服务器", "默认值"
    ];

    public TableDocument Parse(RawTableGrid grid)
    {
        if (grid.Rows.Count < 6)
            throw new FormatException("A table must contain six header rows.");

        var tableName = ParseTableName(grid.Rows[0]);
        var offset = HasHeaderLabel(grid.Rows[3]) ? 1 : 0;
        var descriptions = SliceHeader(grid.Rows[1], offset);
        var types = SliceHeader(grid.Rows[2], offset);
        var names = SliceHeader(grid.Rows[3], offset);
        var targets = SliceHeader(grid.Rows[4], offset);
        var defaults = SliceHeader(grid.Rows[5], offset);
        var fields = new List<FieldSchema>();

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i]?.Trim() ?? string.Empty;
            if (ShouldIgnoreColumn(name))
                continue;

            var typeText = Get(types, i)?.Trim() ?? string.Empty;
            var type = TypeDescriptor.Parse(typeText);
            var target = ParseTarget(Get(targets, i));
            fields.Add(new FieldSchema(
                i + offset,
                name,
                Get(descriptions, i)?.Trim() ?? string.Empty,
                type,
                target,
                EmptyToNull(Get(defaults, i))));
        }

        if (fields.Count == 0)
            throw new FormatException("The table must contain at least one named field.");

        var rows = new List<TableRow>();
        for (var rowIndex = 6; rowIndex < grid.Rows.Count; rowIndex++)
        {
            var sourceRow = grid.Rows[rowIndex];
            var marker = sourceRow.Count > 0 ? sourceRow[0]?.Trim() ?? string.Empty : string.Empty;
            if (marker.StartsWith("##", StringComparison.Ordinal))
                continue;

            var isTest = marker.StartsWith("#test", StringComparison.OrdinalIgnoreCase)
                || marker.StartsWith("#ceshi", StringComparison.OrdinalIgnoreCase);
            var valueOffset = marker.StartsWith("#", StringComparison.Ordinal)
                || (offset == 1 && string.IsNullOrWhiteSpace(marker))
                ? 1
                : 0;
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var index = field.SourceColumn - offset + valueOffset;
                values[field.Name] = index >= 0 && index < sourceRow.Count ? sourceRow[index]?.Trim() : null;
            }

            if (values.Values.All(string.IsNullOrWhiteSpace))
                continue;
            rows.Add(new TableRow(rowIndex + 1, isTest, values));
        }

        return new TableDocument(grid.SourceName, new TableSchema(tableName, fields, fields[0].Name), rows);
    }

    private static string ParseTableName(IReadOnlyList<string?> row)
    {
        var cell = row.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        if (!cell.StartsWith("table:", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("The first row must start with 'table:'.");
        var name = cell[6..].Trim();
        if (name.Length == 0)
            throw new FormatException("Table name cannot be empty.");
        return name;
    }

    private static bool HasHeaderLabel(IReadOnlyList<string?> row) =>
        row.Count > 0 && HeaderLabels.Contains(row[0]?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string?> SliceHeader(IReadOnlyList<string?> row, int offset) =>
        row.Skip(Math.Min(offset, row.Count)).ToArray();

    private static string? Get(IReadOnlyList<string?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ShouldIgnoreColumn(string name) =>
        string.IsNullOrWhiteSpace(name)
        || name.StartsWith("##", StringComparison.Ordinal)
        || name.StartsWith("#test", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("#ceshi", StringComparison.OrdinalIgnoreCase);

    private static FieldTarget ParseTarget(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "cs" => FieldTarget.Both,
        "c" => FieldTarget.Client,
        "s" => FieldTarget.Server,
        _ => throw new FormatException($"Unknown field target '{value}'.")
    };
}
