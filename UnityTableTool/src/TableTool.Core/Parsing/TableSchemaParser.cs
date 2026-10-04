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
        if (grid.Rows.Count == 0)
            throw new FormatException("A table must contain at least one row.");
        if (grid.Rows.Count < (IsSingleton(grid.Rows[0]) ? 5 : 6))
            throw new FormatException("A table must contain six header rows.");

        var tableName = ParseTableName(grid.Rows[0]);
        var isSingleton = IsSingleton(grid.Rows[0]);
        if (isSingleton)
            return ParseSingleton(grid, tableName);

        var offset = HasHeaderLabel(grid.Rows[3]) ? 1 : 0;
        var descriptions = SliceHeader(grid.Rows[1], offset);
        var types = SliceHeader(grid.Rows[2], offset);
        var names = SliceHeader(grid.Rows[3], offset);
        var targets = SliceHeader(grid.Rows[4], offset);
        var defaults = SliceHeader(grid.Rows[5], offset);
        var validationFields = new List<FieldSchema>();
        var fields = new List<FieldSchema>();

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i]?.Trim() ?? string.Empty;
            if (ShouldSkipColumn(name))
                continue;
            var isTestColumn = IsTestColumn(name);

            var typeText = Get(types, i)?.Trim() ?? string.Empty;
            var type = TypeDescriptor.Parse(typeText);
            var target = ParseTarget(Get(targets, i));
            var field = new FieldSchema(
                i + offset,
                name,
                Get(descriptions, i)?.Trim() ?? string.Empty,
                type,
                target,
                EmptyToNull(Get(defaults, i)));
            (isTestColumn ? validationFields : fields).Add(field);
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
            foreach (var field in fields.Concat(validationFields))
            {
                var index = field.SourceColumn - offset + valueOffset;
                values[field.Name] = index >= 0 && index < sourceRow.Count ? sourceRow[index]?.Trim() : null;
            }

            if (values.Values.All(string.IsNullOrWhiteSpace))
                continue;
            rows.Add(new TableRow(rowIndex + 1, isTest, values));
        }

        return new TableDocument(grid.SourceName, new TableSchema(tableName, fields, fields[0].Name, false, validationFields), rows);
    }

    private static TableDocument ParseSingleton(RawTableGrid grid, string tableName)
    {
        var semanticRowIndex = FindSingletonSemanticRow(grid.Rows);
        if (semanticRowIndex < 0)
            throw new FormatException("A singleton table must contain semantic columns 'id', 'type' and 'data'.");

        var semanticRow = grid.Rows[semanticRowIndex];
        var idColumn = FindSemanticColumn(semanticRow, "id");
        var typeColumn = FindSemanticColumn(semanticRow, "type");
        var dataColumn = FindSemanticColumn(semanticRow, "data");
        var descriptionColumn = FindSemanticColumn(semanticRow, "desc");
        var targetColumn = FindTargetColumn(grid.Rows, semanticRowIndex, semanticRow);
        var defaultTarget = FieldTarget.Both;
        var fields = new List<FieldSchema>();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var rows = new List<TableRow>();

        for (var rowIndex = semanticRowIndex + 1; rowIndex < grid.Rows.Count; rowIndex++)
        {
            var row = grid.Rows[rowIndex];
            var id = Get(row, idColumn)?.Trim() ?? string.Empty;
            if (id.StartsWith("##", StringComparison.Ordinal))
                continue;
            if (id.StartsWith("#test", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("#ceshi", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(id)
                && string.IsNullOrWhiteSpace(Get(row, GetOptional(typeColumn))?.Trim())
                && string.IsNullOrWhiteSpace(Get(row, GetOptional(dataColumn))?.Trim()))
            {
                if (targetColumn >= 0 && !string.IsNullOrWhiteSpace(Get(row, targetColumn)))
                    defaultTarget = ParseTarget(Get(row, targetColumn));
                continue;
            }
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (values.ContainsKey(id))
                throw new FormatException($"Duplicate singleton field '{id}'.");

            var typeText = Get(row, typeColumn)?.Trim() ?? string.Empty;
            var data = EmptyToNull(Get(row, dataColumn));
            var type = TypeDescriptor.Parse(typeText);
            var target = targetColumn >= 0 && !string.IsNullOrWhiteSpace(Get(row, targetColumn))
                ? ParseTarget(Get(row, targetColumn))
                : defaultTarget;
            fields.Add(new FieldSchema(
                dataColumn,
                id,
                descriptionColumn >= 0 ? Get(row, descriptionColumn)?.Trim() ?? string.Empty : string.Empty,
                type,
                target,
                null));
            values[id] = data;
        }

        if (fields.Count == 0)
            throw new FormatException("A singleton table must contain at least one data field.");

        rows.Add(new TableRow(semanticRowIndex + 2, false, values));
        return new TableDocument(grid.SourceName, new TableSchema(tableName, fields, fields[0].Name, true), rows);
    }

    private static int FindSingletonSemanticRow(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var limit = Math.Min(rows.Count, 10);
        for (var rowIndex = 1; rowIndex < limit; rowIndex++)
        {
            var row = rows[rowIndex];
            if (FindSemanticColumn(row, "id") >= 0
                && FindSemanticColumn(row, "type") >= 0
                && FindSemanticColumn(row, "data") >= 0)
                return rowIndex;
        }
        return -1;
    }

    private static int FindSemanticColumn(IReadOnlyList<string?> row, string semanticName)
    {
        for (var index = 0; index < row.Count; index++)
        {
            var value = row[index]?.Trim() ?? string.Empty;
            if (value.Equals(semanticName, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private static int FindTargetColumn(
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int semanticRowIndex,
        IReadOnlyList<string?> semanticRow)
    {
        var semanticTarget = FindSemanticColumn(semanticRow, "target");
        if (semanticTarget >= 0)
            return semanticTarget;

        for (var column = 0; column < semanticRow.Count; column++)
        {
            for (var rowIndex = 1; rowIndex < semanticRowIndex; rowIndex++)
            {
                var value = rows[rowIndex].Count > column ? rows[rowIndex][column]?.Trim() ?? string.Empty : string.Empty;
                if (IsTargetLabel(value))
                    return column;
            }
        }
        return -1;
    }

    private static bool IsTargetLabel(string value) => value.Equals("前后端", StringComparison.OrdinalIgnoreCase)
        || value.Equals("客户端服务器", StringComparison.OrdinalIgnoreCase)
        || value.Equals("客户端/服务器", StringComparison.OrdinalIgnoreCase)
        || value.Equals("clientserver", StringComparison.OrdinalIgnoreCase)
        || value.Equals("target", StringComparison.OrdinalIgnoreCase);

    private static int GetOptional(int index) => index < 0 ? int.MaxValue : index;

    private static string ParseTableName(IReadOnlyList<string?> row)
    {
        var lines = row.SelectMany(value => (value ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .ToArray();
        var tableLine = lines.FirstOrDefault(value => value.StartsWith("table:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        if (tableLine.Length == 0)
            throw new FormatException("The first row must start with 'table:'.");
        var name = tableLine[6..].Trim();
        var metadataIndex = name.IndexOfAny(['\r', '\n']);
        if (metadataIndex >= 0)
            name = name[..metadataIndex].Trim();
        var pipeIndex = name.IndexOf('|');
        if (pipeIndex >= 0)
            name = name[..pipeIndex].Trim();
        if (name.Contains(" type:single", StringComparison.OrdinalIgnoreCase))
            name = name[..name.IndexOf(" type:single", StringComparison.OrdinalIgnoreCase)].Trim();
        if (name.Length == 0)
            throw new FormatException("Table name cannot be empty.");
        return name;
    }

    private static bool IsSingleton(IReadOnlyList<string?> row) =>
        row.Any(value => value?.Contains("type:single", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("type=single", StringComparison.OrdinalIgnoreCase) == true);

    private static bool HasHeaderLabel(IReadOnlyList<string?> row) =>
        row.Count > 0 && HeaderLabels.Contains(row[0]?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string?> SliceHeader(IReadOnlyList<string?> row, int offset) =>
        row.Skip(Math.Min(offset, row.Count)).ToArray();

    private static string? Get(IReadOnlyList<string?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ShouldSkipColumn(string name) =>
        string.IsNullOrWhiteSpace(name) || name.StartsWith("##", StringComparison.Ordinal);

    private static bool IsTestColumn(string name) =>
        name.StartsWith("#test", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("#ceshi", StringComparison.OrdinalIgnoreCase);

    private static FieldTarget ParseTarget(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "cs" => FieldTarget.Both,
        "c" => FieldTarget.Client,
        "s" => FieldTarget.Server,
        _ => throw new FormatException($"Unknown field target '{value}'.")
    };

}
