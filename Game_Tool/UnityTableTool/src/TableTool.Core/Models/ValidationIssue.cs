namespace TableTool.Core.Models;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string SourceName,
    int? SourceRow = null,
    int? SourceColumn = null,
    string? FieldName = null,
    string? Key = null,
    string? Suggestion = null);
