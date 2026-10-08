// 用途：汇总当前勾选表及其关联表的批量校验结果。
// 编写日期：2026-10-08
// 作者：Codex。

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

        try
        {
            TableDocumentMerger.Merge(selectedDocuments);
        }
        catch (FormatException error)
        {
            issues.Add(new ValidationIssue(
                ErrorCodes.TableMergeInvalid,
                ValidationSeverity.Error,
                error.Message,
                "当前选择",
                Suggestion: "检查同名表的字段、类型、默认值和测试列是否一致。"));
        }

        foreach (var missing in TableSelectionResolver.FindMissingReferenceDetails(allDocuments, selectedDocuments))
        {
            var field = selectedDocuments
                .FirstOrDefault(document => string.Equals(document.SourceName, missing.SourceName, StringComparison.OrdinalIgnoreCase))
                ?.Schema.Fields.FirstOrDefault(item =>
                    string.Equals(item.Name, missing.FieldName, StringComparison.OrdinalIgnoreCase));

            issues.Add(new ValidationIssue(
                ErrorCodes.TableReferenceMissing,
                ValidationSeverity.Error,
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
