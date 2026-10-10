// 用途：生成配置表预览、字段标题和校验高亮映射。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-09
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Core.Models;
using TableTool.Core.Validation;
using TableTool.Gui.Views;

namespace TableTool.Gui.Services;

public sealed record PreviewField(
    string Key,
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
    public string? this[string key] => values.GetValueOrDefault(key);
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
    private sealed record PreviewContext(
        string Title,
        string ModifiedText,
        string SourceText,
        IReadOnlyList<string> SheetNames,
        IReadOnlyList<ValidationIssue> Issues);

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
        var context = new PreviewContext(
            table.DisplayName,
            string.Format("修改于 {0}", GetModifiedTime(table.SourcePath).ToString("HH:mm")),
            $"{GetRelativeSource(table.SourcePath)} · {table.CurrentSheet.LogicalTableName}",
            table.Sheets.Select(sheet => sheet.SheetName).ToArray(),
            issues);

        return document.Schema.IsSingleton
            ? CreateSingletonPreview(document, highlights, context)
            : CreateTablePreview(document, highlights, context);
    }

    private static PreviewViewModel CreateTablePreview(
        TableDocument document,
        ValidationHighlightMap highlights,
        PreviewContext context)
    {
        var previewFields = document.Schema.Fields
            .OrderBy(field => field.Name.Equals(document.Schema.PrimaryKey, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(field => field.SourceColumn)
            .ToArray();
        var fields = previewFields.Select(field =>
        {
            var highlight = highlights.Fields.GetValueOrDefault(field.Name);
            return new PreviewField(
                field.Name,
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
                    var highlight = highlights.Cells.GetValueOrDefault(
                        ValidationHighlightResolver.CellKey(row.SourceRow, field.Name));
                    if (highlight is null)
                        continue;
                    cellLevels[field.Name] = ToHighlightName(highlight.Level);
                    cellTooltips[field.Name] = $"{highlight.Code}：{highlight.Reason}";
                }

                return new PreviewRow(row.SourceRow, values, cellLevels, cellTooltips);
            })
            .ToArray();

        return BuildViewModel(context, fields, rows);
    }

    private static PreviewViewModel CreateSingletonPreview(
        TableDocument document,
        ValidationHighlightMap highlights,
        PreviewContext context)
    {
        var definitions = document.Schema.Fields
            .Where(field => !field.IsTest
                && !field.Name.Equals("values", StringComparison.OrdinalIgnoreCase)
                && !field.Name.Equals("默认值", StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.NameRow)
            .ToArray();
        if (definitions.Length == 0)
            return BuildViewModel(context, [], []);

        var singletonValues = document.Rows
            .FirstOrDefault(row => !row.IsTest)?.RawValues
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var displayKeys = new[] { "id", "type", "data", "cs" };

        string DefinitionValue(FieldSchema field, string key) => key switch
        {
            "id" => field.Name,
            "type" => GetSingletonTypeLabel(field),
            "data" => singletonValues.GetValueOrDefault(field.Name) ?? string.Empty,
            "cs" => GetTargetLabel(field.Target),
            _ => string.Empty
        };

        var fields = displayKeys.Select(key => new PreviewField(
            key,
            key,
            key,
            string.Empty,
            "None",
            string.Empty)).ToArray();

        var rows = definitions.Select(definition =>
        {
            var values = displayKeys.ToDictionary(
                key => key,
                key => DefinitionValue(definition, key),
                StringComparer.OrdinalIgnoreCase);
            var sourceHighlight = highlights.Cells.GetValueOrDefault(
                    ValidationHighlightResolver.CellKey(definition.NameRow, definition.Name))
                ?? highlights.Fields.GetValueOrDefault(definition.Name);
            var cellLevels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cellTooltips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (sourceHighlight is not null)
            {
                foreach (var key in displayKeys)
                {
                    cellLevels[key] = ToHighlightName(sourceHighlight.Level);
                    cellTooltips[key] = $"{sourceHighlight.Code}：{sourceHighlight.Reason}";
                }
            }

            return new PreviewRow(definition.NameRow, values, cellLevels, cellTooltips);
        }).ToArray();

        return BuildViewModel(context, fields, rows);
    }

    private static PreviewViewModel BuildViewModel(
        PreviewContext context,
        IReadOnlyList<PreviewField> fields,
        IReadOnlyList<PreviewRow> rows)
    {
        var errors = context.Issues.Count(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);
        var warnings = context.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        return new PreviewViewModel(
            context.Title,
            context.ModifiedText,
            context.SourceText,
            context.SheetNames,
            fields,
            rows,
            context.Issues,
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

    private static string GetSingletonTypeLabel(FieldSchema field) =>
        field.Type.BaseType + string.Concat(Enumerable.Repeat("()", field.Type.Dimensions));

    private static string GetTargetLabel(FieldTarget target) => target switch
    {
        FieldTarget.Client => "c",
        FieldTarget.Server => "s",
        _ => "cs"
    };

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
