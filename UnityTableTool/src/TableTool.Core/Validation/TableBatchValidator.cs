using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Core.Validation;

public sealed record TableValidationBatch(
    IReadOnlyList<TableDocument> Documents,
    IReadOnlyList<ValidationIssue> Issues);

public static class TableBatchValidator
{
    public static TableValidationBatch Validate(
        IReadOnlyList<TableDocument> allDocuments,
        IEnumerable<string> selectedSourceNames)
    {
        var selectedDocuments = TableSelectionResolver.Expand(allDocuments, selectedSourceNames).ToArray();
        var issues = selectedDocuments
            .SelectMany(document => new TableValidator().Validate(document))
            .ToList();

        foreach (var missing in TableSelectionResolver.FindMissingReferenceDetails(allDocuments, selectedDocuments))
        {
            var field = selectedDocuments
                .FirstOrDefault(document => string.Equals(document.SourceName, missing.SourceName, StringComparison.OrdinalIgnoreCase))
                ?.Schema.Fields.FirstOrDefault(item =>
                    string.Equals(item.Name, missing.FieldName, StringComparison.OrdinalIgnoreCase));

            issues.Add(new ValidationIssue(
                ErrorCodes.TableReferenceMissing,
                ValidationSeverity.Warning,
                $"引用表 {missing.ReferencedTableName} 未找到。",
                missing.SourceName,
                SourceColumn: field?.SourceColumn + 1,
                FieldName: missing.FieldName,
                Key: missing.ReferencedTableName,
                Suggestion: "补全引用表，或移除对应引用字段。"));
        }

        return new TableValidationBatch(
            selectedDocuments,
            issues
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(issue => issue.SourceRow ?? 0)
                .ThenBy(issue => issue.SourceColumn ?? 0)
                .ToArray());
    }
}
