// 用途：生成配置表预览、字段标题和校验问题。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.Data;
using System.IO;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;

namespace TableTool.Gui.Services;

public sealed record PreviewField(string Name, string Header, string TypeLabel);

public sealed record PreviewViewModel(
    string Title,
    string ModifiedText,
    string SourceText,
    IReadOnlyList<string> SheetNames,
    IReadOnlyList<PreviewField> Fields,
    DataTable Data,
    IReadOnlyList<ValidationIssue> Issues,
    string IssueSummary);

public sealed class PreviewService
{
    public PreviewViewModel Create(TableModel table, IReadOnlyList<TableModel> allTables)
    {
        var document = table.Document;
        var fields = document.Schema.Fields.Select(field => new PreviewField(
            field.Name,
            field.Name,
            GetTypeLabel(field, document.Schema))).ToArray();

        var data = new DataTable("Preview");
        foreach (var field in fields)
            data.Columns.Add(field.Name, typeof(string));

        foreach (var row in document.Rows.Where(row => !row.IsTest))
        {
            var values = fields.Select(field =>
            {
                var raw = row.RawValues.TryGetValue(field.Name, out var value) ? value : null;
                return string.IsNullOrWhiteSpace(raw)
                    ? document.Schema.Fields.First(item => item.Name == field.Name).DefaultValue ?? string.Empty
                    : raw;
            }).ToArray();
            data.Rows.Add(values);
        }

        var issues = new List<ValidationIssue>(new TableValidator().Validate(document));
        var selectedDocuments = allTables
            .Where(item => item.IsSelected)
            .SelectMany(item => item.Sheets.Select(sheet => sheet.Document))
            .ToArray();
        if (selectedDocuments.Length > 0)
        {
            var missing = TableSelectionResolver.FindMissingReferences(
                allTables.Select(item => item.Document),
                selectedDocuments);
            foreach (var name in missing)
            {
                issues.Add(new ValidationIssue(
                    ErrorCodes.TableReferenceMissing,
                    ValidationSeverity.Warning,
                    $"引用表 {name} 未找到。",
                    document.SourceName,
                    Suggestion: "补全引用表，或移除对应引用字段。"));
            }
        }

        var errors = issues.Count(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);
        var warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        var sheetNames = table.Sheets
            .Select(sheet => sheet.SheetName)
            .ToArray();

        return new PreviewViewModel(
            table.DisplayName,
            LanguageManager.Format("修改于 {0}", GetModifiedTime(table.SourcePath).ToString("HH:mm")),
            $"{GetRelativeSource(table.SourcePath)} · {table.CurrentSheet.SheetName}",
            sheetNames,
            fields,
            data,
            issues,
            $"{errors} 错误 / {warnings} 警告");
    }

    private static string GetTypeLabel(FieldSchema field, TableSchema schema)
    {
        var suffix = string.Concat(Enumerable.Repeat("()", field.Type.Dimensions));
        var type = field.Type.BaseType + suffix;
        if (field.Name.Equals(schema.PrimaryKey, StringComparison.OrdinalIgnoreCase))
            return $"{type} · 主键";
        if (field.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            return $"{type} · 引用";
        return type;
    }

    private static DateTime GetModifiedTime(string path) =>
        File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now;

    private static string GetRelativeSource(string path)
    {
        try
        {
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), path).Replace('\\', '/');
        }
        catch
        {
            return path.Replace('\\', '/');
        }
    }
}
