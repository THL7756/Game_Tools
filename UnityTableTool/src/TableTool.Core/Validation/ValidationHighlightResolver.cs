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
    ValidationHighlightLevel HighestLevel)
{
    public static ValidationHighlightMap Empty { get; } = new(
        new Dictionary<string, ValidationHighlight>(),
        new Dictionary<string, ValidationHighlight>(),
        ValidationHighlightLevel.None);
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

        foreach (var issue in documentIssues)
        {
            var level = ToLevel(issue.Severity);
            if (level == ValidationHighlightLevel.None)
                continue;

            var highlight = new ValidationHighlight(level, issue.Code, issue.Message);
            if (issue.SourceRow is int sourceRow && !string.IsNullOrWhiteSpace(issue.FieldName))
            {
                var key = CellKey(sourceRow, issue.FieldName);
                cells[key] = KeepHighest(cells.GetValueOrDefault(key), highlight);
            }
            else if (!string.IsNullOrWhiteSpace(issue.FieldName))
            {
                fields[issue.FieldName] = KeepHighest(fields.GetValueOrDefault(issue.FieldName), highlight);
            }
        }

        return new ValidationHighlightMap(cells, fields, GetHighestLevel(documentIssues));
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
