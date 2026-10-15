// 用途：把校验问题映射到 Sheet、字段和预览单元格高亮。
// 编写日期：2026-10-08
// 作者：Codex。

using TableTool.Core.Models;

namespace TableTool.Core.Validation;

public enum ValidationHighlightLevel
{
    None,
    Warning,
    Error
}

public sealed record ValidationHighlight(
    ValidationHighlightLevel Level,
    string Code,
    string Reason);

public sealed record ValidationHighlightMap(
    IReadOnlyDictionary<string, ValidationHighlight> Cells,
    IReadOnlyDictionary<string, ValidationHighlight> Fields,
    ValidationHighlightLevel HighestLevel,
    IReadOnlyDictionary<int, ValidationHighlight>? Columns = null)
{
    public static ValidationHighlightMap Empty { get; } = new(
        new Dictionary<string, ValidationHighlight>(),
        new Dictionary<string, ValidationHighlight>(),
        ValidationHighlightLevel.None,
        new Dictionary<int, ValidationHighlight>());
}

public static class ValidationHighlightResolver
{
    public static ValidationHighlightMap Resolve(
        TableDocument document,
        IEnumerable<ValidationIssue> issues)
    {
        var documentIssues = issues
            .Where(issue => string.Equals(issue.SourceName, document.SourceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var cells = new Dictionary<string, ValidationHighlight>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, ValidationHighlight>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<int, ValidationHighlight>();

        foreach (var issue in documentIssues)
        {
            var level = ToLevel(issue.Severity);
            if (level == ValidationHighlightLevel.None)
                continue;

            var highlight = new ValidationHighlight(level, issue.Code, issue.Message);
            // 单例预览把名称、类型、值和端标记重排为一行，按定义行保留其字段定位。
            if (document.Schema.IsSingleton && issue.SourceRow is int definitionRow
                && !string.IsNullOrWhiteSpace(issue.FieldName))
            {
                var key = CellKey(definitionRow, issue.FieldName);
                cells[key] = KeepHighest(cells.GetValueOrDefault(key), highlight);
            }
            if (issue.SourceColumn is int sourceColumn && sourceColumn > 0)
            {
                var column = sourceColumn - 1;
                if (issue.SourceRow is int sourceRow)
                {
                    var key = CellKey(sourceRow, column);
                    cells[key] = KeepHighest(cells.GetValueOrDefault(key), highlight);
                }
                if (!document.Schema.IsSingleton || issue.SourceRow is null)
                    columns[column] = KeepHighest(columns.GetValueOrDefault(column), highlight);
            }
            else if (!string.IsNullOrWhiteSpace(issue.FieldName))
            {
                if (issue.SourceRow is int sourceRow)
                {
                    var key = CellKey(sourceRow, issue.FieldName);
                    cells[key] = KeepHighest(cells.GetValueOrDefault(key), highlight);
                }
                fields[issue.FieldName] = KeepHighest(fields.GetValueOrDefault(issue.FieldName), highlight);
            }
        }

        return new ValidationHighlightMap(cells, fields, GetHighestLevel(documentIssues), columns);
    }

    public static ValidationHighlightLevel GetHighestLevel(IEnumerable<ValidationIssue> issues)
    {
        var level = ValidationHighlightLevel.None;
        foreach (var issue in issues)
            level = Higher(level, ToLevel(issue.Severity));
        return level;
    }

    public static ValidationHighlightLevel GetHighestLevel(
        IEnumerable<ValidationIssue> issues,
        string sourceName) =>
        GetHighestLevel(issues.Where(issue =>
            string.Equals(issue.SourceName, sourceName, StringComparison.OrdinalIgnoreCase)));

    public static string CellKey(int sourceRow, string fieldName) => $"{sourceRow}:{fieldName}";

    public static string CellKey(int sourceRow, int sourceColumn) => $"{sourceRow}:column:{sourceColumn}";

    private static ValidationHighlight KeepHighest(
        ValidationHighlight? current,
        ValidationHighlight candidate) =>
        current is null || Higher(current.Level, candidate.Level) == candidate.Level
            ? candidate
            : current;

    private static ValidationHighlightLevel ToLevel(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Warning => ValidationHighlightLevel.Warning,
        ValidationSeverity.Error or ValidationSeverity.Fatal => ValidationHighlightLevel.Error,
        _ => ValidationHighlightLevel.None
    };

    private static ValidationHighlightLevel Higher(
        ValidationHighlightLevel left,
        ValidationHighlightLevel right) =>
        (ValidationHighlightLevel)Math.Max((int)left, (int)right);
}
