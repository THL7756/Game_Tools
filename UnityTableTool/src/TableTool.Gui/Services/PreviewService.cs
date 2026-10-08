// 用途：生成配置表预览、字段标题和校验高亮映射。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Core.Models;
using TableTool.Core.Validation;
using TableTool.Gui.Views;

namespace TableTool.Gui.Services;

public sealed record PreviewField(
    string Name,
    string Header,
    string TypeLabel,
    string HighlightLevel,
    string HighlightTooltip);

public sealed class PreviewRow
{
    private readonly IReadOnlyDictionary<string, string> values;
    private readonly IReadOnlyDictionary<string, string> cellLevels;
    private readonly IReadOnlyDictionary<string, string> cellTooltips;

    public PreviewRow(
        int sourceRow,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> cellLevels,
        IReadOnlyDictionary<string, string> cellTooltips)
    {
        SourceRow = sourceRow;
        this.values = values;
        this.cellLevels = cellLevels;
        this.cellTooltips = cellTooltips;
    }

    internal int SourceRow { get; }
    public IReadOnlyDictionary<string, string> CellLevels => cellLevels;
    public IReadOnlyDictionary<string, string> CellTooltips => cellTooltips;
    public string? this[string fieldName] => values.GetValueOrDefault(fieldName);
}

public sealed record PreviewViewModel(
    string Title,
    string ModifiedText,
    string SourceText,
    IReadOnlyList<string> SheetNames,
    IReadOnlyList<PreviewField> Fields,
    IReadOnlyList<PreviewRow> Rows,
    IReadOnlyList<ValidationIssue> Issues,
    string IssueSummary);

public sealed class PreviewService
{
    public PreviewViewModel Create(
        TableModel table,
        IReadOnlyList<ValidationIssue> batchIssues,
        ArraySeparatorOptions? separators = null)
    {
        var document = table.CurrentSheet.Document;
        var ownIssues = (document.ParseIssues ?? [])
            .Concat(new TableValidator(separators).Validate(document));
        var issues = ownIssues
            .Concat(batchIssues.Where(issue =>
                string.Equals(issue.SourceName, document.SourceName, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .OrderBy(issue => issue.Severity)
            .ThenBy(issue => issue.SourceRow ?? 0)
            .ThenBy(issue => issue.SourceColumn ?? 0)
            .ToArray();
        var highlights = ValidationHighlightResolver.Resolve(document, issues);

        var previewFields = document.Schema.Fields;
        var fields = previewFields.Select(field =>
        {
            var highlight = highlights.Fields.GetValueOrDefault(field.Name);
            return new PreviewField(
                field.Name,
                field.Name,
                GetTypeLabel(field, document.Schema),
                ToHighlightName(highlight?.Level ?? ValidationHighlightLevel.None),
                highlight is null ? string.Empty : $"{highlight.Code}：{highlight.Reason}");
        }).ToArray();

        var rows = document.Rows
            .Where(row => !row.IsTest)
            .Select(row =>
            {
                var values = previewFields.ToDictionary(
                    field => field.Name,
                    field =>
                    {
                        var raw = row.RawValues.GetValueOrDefault(field.Name);
                        return string.IsNullOrWhiteSpace(raw)
                            ? field.DefaultValue ?? string.Empty
                            : raw;
                    },
                    StringComparer.OrdinalIgnoreCase);
                var cellLevels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var cellTooltips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in previewFields)
                {
                    var key = ValidationHighlightResolver.CellKey(row.SourceRow, field.Name);
                    var highlight = highlights.Cells.GetValueOrDefault(key);
                    if (highlight is null)
                        continue;
                    cellLevels[field.Name] = ToHighlightName(highlight.Level);
                    cellTooltips[field.Name] = $"{highlight.Code}：{highlight.Reason}";
                }

                return new PreviewRow(row.SourceRow, values, cellLevels, cellTooltips);
            })
            .ToArray();

        var errors = issues.Count(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);
        var warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        var sheetNames = table.Sheets
            .Select(sheet => sheet.SheetName)
            .ToArray();

        return new PreviewViewModel(
            table.DisplayName,
            string.Format("修改于 {0}", GetModifiedTime(table.SourcePath).ToString("HH:mm")),
            $"{GetRelativeSource(table.SourcePath)} · {table.CurrentSheet.SheetName}",
            sheetNames,
            fields,
            rows,
            issues,
            $"{errors} 错误 / {warnings} 警告");
    }

    public static string ToHighlightName(ValidationHighlightLevel level) => level switch
    {
        ValidationHighlightLevel.Error => "Error",
        ValidationHighlightLevel.Warning => "Warning",
        _ => "None"
    };

    private static string GetTypeLabel(FieldSchema field, TableSchema schema)
    {
        var suffix = string.Concat(Enumerable.Repeat("()", field.Type.Dimensions));
        var type = field.Type.BaseType + suffix;
        if (!schema.IsSingleton && field.Name.Equals(schema.PrimaryKey, StringComparison.OrdinalIgnoreCase))
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
