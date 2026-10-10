// 用途：汇总当前勾选表及其关联表的批量校验结果。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

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
        IEnumerable<string> selectedSourceNames,
        ArraySeparatorOptions? separators = null)
    {
        var selectedDocuments = TableSelectionResolver.Expand(allDocuments, selectedSourceNames).ToArray();
        var issues = selectedDocuments
            .SelectMany(document => document.ParseIssues ?? [])
            .ToList();
        issues.AddRange(new TableValidator(separators).Validate(selectedDocuments));
        issues.AddRange(TableDocumentMerger.FindSchemaConflicts(selectedDocuments));
        issues.AddRange(TableReferenceValidator.Validate(allDocuments, selectedDocuments, separators));

        return new TableValidationBatch(
            selectedDocuments,
            issues
                .Distinct()
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.SourceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(issue => issue.SourceRow ?? 0)
                .ThenBy(issue => issue.SourceColumn ?? 0)
                .ToArray());
    }
}
