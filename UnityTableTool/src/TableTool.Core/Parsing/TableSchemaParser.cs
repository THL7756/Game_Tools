// 用途：把原始 Sheet 解析为正式表文档，并保留字段级诊断坐标。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Validation;

namespace TableTool.Core.Parsing;

public sealed class TableSchemaParser
{
    private static readonly string[] HeaderLabels =
    [
        "字段说明", "说明", "字段类型", "类型", "字段名", "名称", "客户端服务器", "客户端/服务器", "默认值"
    ];

    public TableDocument Parse(RawTableGrid grid)
    {
        var result = ParseWithDiagnostics(grid);
        if (result.Document is null)
            throw new FormatException(result.Issues.FirstOrDefault()?.Message ?? "表格不是正式配置表。");
        return result.Document;
    }

    public TableParseResult ParseWithDiagnostics(RawTableGrid grid)
    {
        if (grid.Rows.Count == 0)
            return TableParseResult.Informal(grid.SourceName, "文件或 Sheet 为空。");

        var tableName = TryParseTableName(grid.Rows[0]);
        if (tableName is null)
            return TableParseResult.Informal(grid.SourceName, "缺少 table: 标记或表名为空。");
        var isSingleton = IsSingleton(grid.Rows[0]);
        var minimumRows = isSingleton ? 5 : 6;
        if (grid.Rows.Count < minimumRows)
            return TableParseResult.Informal(grid.SourceName, "正式表定义行不足。");

        if (isSingleton)
            return ParseSingletonWithDiagnostics(grid, tableName);

        var offset = HasHeaderLabel(grid.Rows[3]) ? 1 : 0;
        var descriptions = SliceHeader(grid.Rows[1], offset);
        var types = SliceHeader(grid.Rows[2], offset);
        var names = SliceHeader(grid.Rows[3], offset);
        var targets = SliceHeader(grid.Rows[4], offset);
        var defaults = SliceHeader(grid.Rows[5], offset);
        var validationFields = new List<FieldSchema>();
        var fields = new List<FieldSchema>();
        var issues = new List<ValidationIssue>();

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i]?.Trim() ?? string.Empty;
            if (ShouldSkipColumn(name))
                continue;
            var isTestColumn = IsTestColumn(name);

            var typeText = Get(types, i)?.Trim() ?? string.Empty;
            var type = ParseType(typeText, grid.SourceName, 3, i + offset, out var typeIssue);
            if (typeIssue is not null)
                issues.Add(typeIssue);
            var target = ParseTarget(Get(targets, i), grid.SourceName, 5, i + offset, out var targetIssue);
            if (targetIssue is not null)
                issues.Add(targetIssue);
            var field = new FieldSchema(
                i + offset,
                name,
                Get(descriptions, i)?.Trim() ?? string.Empty,
                type,
                target,
                EmptyToNull(Get(defaults, i)),
                IsTest: isTestColumn);
            (isTestColumn ? validationFields : fields).Add(field);
        }

        if (fields.Count == 0)
            return TableParseResult.Informal(grid.SourceName, "没有正式字段，不算正式表。");

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

        return new TableParseResult(
            new TableDocument(grid.SourceName, new TableSchema(tableName, fields, fields[0].Name, false, validationFields), rows, issues),
            issues,
            true);
    }

    private static TableParseResult ParseSingletonWithDiagnostics(RawTableGrid grid, string tableName)
    {
        var issues = new List<ValidationIssue>();
        var semanticRowIndex = FindSingletonSemanticRow(grid.Rows);
        if (semanticRowIndex < 0)
            return TableParseResult.Informal(grid.SourceName, "单例表缺少 id、type、data 语义列。");

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
                {
                    defaultTarget = ParseTarget(Get(row, targetColumn), grid.SourceName, rowIndex + 1, targetColumn, out var defaultTargetIssue);
                    if (defaultTargetIssue is not null)
                        issues.Add(defaultTargetIssue);
                }
                continue;
            }
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var typeText = Get(row, typeColumn)?.Trim() ?? string.Empty;
            var data = EmptyToNull(Get(row, dataColumn));
            var type = ParseType(typeText, grid.SourceName, rowIndex + 1, typeColumn, out var typeIssue);
            if (typeIssue is not null)
                issues.Add(typeIssue with { FieldName = id });
            ValidationIssue? targetIssue = null;
            var target = defaultTarget;
            if (targetColumn >= 0 && !string.IsNullOrWhiteSpace(Get(row, targetColumn)))
                target = ParseTarget(Get(row, targetColumn), grid.SourceName, rowIndex + 1, targetColumn, out targetIssue);
            if (targetIssue is not null)
                issues.Add(targetIssue with { FieldName = id });
            var existingIndex = fields.FindIndex(field => field.Name.Equals(id, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                var existing = fields[existingIndex];
                if (!string.Equals(existing.Type.DisplayText, type.DisplayText, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        ErrorCodes.TableMergeInvalid,
                        ValidationSeverity.Error,
                        $"单例字段“{id}”重复定义且类型不一致。",
                        grid.SourceName,
                        rowIndex + 1,
                        typeColumn + 1,
                        id));
                    continue;
                }
                fields[existingIndex] = existing with { Target = MergeTarget(existing.Target, target) };
                if (string.IsNullOrWhiteSpace(values[id]) && data is not null)
                    values[id] = data;
                continue;
            }

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
            return TableParseResult.Informal(grid.SourceName, "单例表没有正式数据字段。");

        rows.Add(new TableRow(semanticRowIndex + 2, false, values));
        return new TableParseResult(
            new TableDocument(grid.SourceName, new TableSchema(tableName, fields, fields[0].Name, true), rows, issues),
            issues,
            true);
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

    private static string? TryParseTableName(IReadOnlyList<string?> row)
    {
        var lines = row.SelectMany(value => (value ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .ToArray();
        var tableLine = lines.FirstOrDefault(value => value.StartsWith("table:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        if (tableLine.Length == 0)
            return null;
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
            return null;
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
        || name.StartsWith("#ceshi", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("test_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("ceshi_", StringComparison.OrdinalIgnoreCase);

    private static FieldTarget ParseTarget(
        string? value,
        string sourceName,
        int row,
        int column,
        out ValidationIssue? issue)
    {
        issue = null;
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "cs" => FieldTarget.Both,
            "c" => FieldTarget.Client,
            "s" => FieldTarget.Server,
            _ => InvalidTarget(value, sourceName, row, column, out issue)
        };
    }

    private static FieldTarget InvalidTarget(
        string? value,
        string sourceName,
        int row,
        int column,
        out ValidationIssue? issue)
    {
        issue = new ValidationIssue(
            ErrorCodes.FieldTargetInvalid,
            ValidationSeverity.Error,
            $"客户端/服务器标记“{value}”无效。",
            sourceName,
            row,
            column + 1,
            Suggestion: "只能填写空值、c、s 或 cs。");
        return FieldTarget.Both;
    }

    private static TypeDescriptor ParseType(
        string typeText,
        string sourceName,
        int row,
        int column,
        out ValidationIssue? issue)
    {
        try
        {
            issue = null;
            return TypeDescriptor.Parse(typeText);
        }
        catch (FormatException error)
        {
            issue = new ValidationIssue(
                ErrorCodes.FieldTypeUnknown,
                ValidationSeverity.Error,
                $"字段类型“{typeText}”无效。",
                sourceName,
                row,
                column + 1,
                Suggestion: error.Message);
            return TypeDescriptor.Invalid(typeText);
        }
    }

    private static FieldTarget MergeTarget(FieldTarget left, FieldTarget right) =>
        left == right ? left : FieldTarget.Both;

}
