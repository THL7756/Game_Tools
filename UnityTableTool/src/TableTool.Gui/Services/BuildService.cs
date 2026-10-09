// 用途：执行选中配置表的关联展开、校验和 JSON/C# 分端打表。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10

// 作者：Codex（按用户需求修改）

using System.Diagnostics;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using TableTool.Gui.Models;
using TableTool.Serialization;

namespace TableTool.Gui.Services;

public sealed record BuildLogEntry(DateTime Time, string Level, string Message);

public sealed record BuildResult(
    bool Success,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<BuildLogEntry> Logs,
    int TableCount,
    int RowCount,
    int FileCount,
    TimeSpan Elapsed);

public sealed class BuildService
{
    public BuildResult Build(
        AppSettings settings,
        IReadOnlyList<TableModel> allTables,
        IEnumerable<string> selectedSourceNames,
        ExportTarget dataTarget,
        ExportTarget codeTarget)
    {
        var stopwatch = Stopwatch.StartNew();
        var logs = new List<BuildLogEntry>();
        if (dataTarget == ExportTarget.None && codeTarget == ExportTarget.None)
        {
            logs.Add(Entry("INFO", "未选择输出范围。"));
            return new BuildResult(false, [], logs, 0, 0, 0, stopwatch.Elapsed);
        }

        var allDocuments = allTables
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document))
            .ToArray();
        var selectedDocuments = TableSelectionResolver.Expand(allDocuments, selectedSourceNames).ToArray();
        if (selectedDocuments.Length == 0)
        {
            logs.Add(Entry("INFO", "未选择配置表。"));
            return new BuildResult(false, [], logs, 0, 0, 0, stopwatch.Elapsed);
        }

        TableDocument[] merged;
        var schemaConflicts = TableDocumentMerger.FindSchemaConflicts(selectedDocuments);
        if (schemaConflicts.Count > 0)
        {
            foreach (var issue in schemaConflicts)
                logs.Add(Entry("FAIL", ValidationIssueFormatter.LogText(issue)));
            stopwatch.Stop();
            return new BuildResult(false, schemaConflicts, logs, selectedDocuments.Length, 0, 0, stopwatch.Elapsed);
        }
        try
        {
            merged = TableDocumentMerger.Merge(selectedDocuments).ToArray();
        }
        catch (FormatException)
        {
            var mergeIssues = TableDocumentMerger.FindSchemaConflicts(selectedDocuments);
            var issue = mergeIssues.FirstOrDefault() ?? new ValidationIssue(
                ErrorCodes.TableMergeInvalid,
                ValidationSeverity.Error,
                "同名表结构不一致。",
                selectedDocuments[0].SourceName,
                Suggestion: "检查同名表的字段、类型、默认值和测试列是否一致。");
            logs.Add(Entry("FAIL", ValidationIssueFormatter.LogText(issue)));
            stopwatch.Stop();
            return new BuildResult(false, mergeIssues.Count == 0 ? [issue] : mergeIssues, logs, selectedDocuments.Length, 0, 0, stopwatch.Elapsed);
        }
        var issues = merged.SelectMany(document => (document.ParseIssues ?? []).Concat(new TableValidator(
            new ArraySeparatorOptions(
                settings.ArrayInnerSeparator,
                settings.ArrayMiddleSeparator,
                settings.ArrayOuterSeparator)).Validate(document))).ToList();
        issues.AddRange(TableDocumentMerger.FindSchemaConflicts(selectedDocuments));
        if (codeTarget != ExportTarget.None)
        {
            foreach (var document in merged)
            foreach (var field in document.Schema.Fields.Where(field => field.Type.IsEnum))
            foreach (var value in field.Type.EnumValues ?? [])
            {
                if (CSharpExporter.IsValidEnumMember(value))
                    continue;
                issues.Add(new ValidationIssue(
                    ErrorCodes.EnumCSharpInvalid,
                    ValidationSeverity.Error,
                    $"枚举字段“{field.Name}”的成员“{value}”不是合法的 C# 标识符。",
                    document.SourceName,
                    field.TypeRow,
                    field.SourceColumn + 1,
                    field.Name,
                    Suggestion: "请修改 enum 成员名称后再生成 C#。"));
            }
        }
        issues.AddRange(TableReferenceValidator.Validate(
            allDocuments,
            selectedDocuments,
            new ArraySeparatorOptions(
                settings.ArrayInnerSeparator,
                settings.ArrayMiddleSeparator,
                settings.ArrayOuterSeparator)));
        logs.Add(Entry("INFO", $"开始打表：{merged.Length} 张逻辑表 → 数据：{TargetText(dataTarget)} · 代码：{TargetText(codeTarget)}"));

        if (issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
        {
            foreach (var issue in issues.Where(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
                logs.Add(Entry("FAIL", ValidationIssueFormatter.LogText(issue)));
            stopwatch.Stop();
            return new BuildResult(false, issues, logs, merged.Length, 0, 0, stopwatch.Elapsed);
        }

        var rowCount = merged.Sum(document => document.Rows.Count(row => !row.IsTest));
        logs.Add(Entry("PASS", string.Join(" · ", merged.Select(document => $"{document.Schema.Name} {document.Rows.Count(row => !row.IsTest)} 行"))));
        var warningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        if (warningCount > 0)
        {
            foreach (var issue in issues.Where(issue => issue.Severity == ValidationSeverity.Warning))
                logs.Add(Entry("WARN", ValidationIssueFormatter.LogText(issue)));
            logs.Add(Entry("WARN", $"检查通过，保留 {warningCount} 条提示警告"));
        }

        try
        {
            var options = new ExportOptions(
                settings.ClientOutputDirectory,
                settings.ServerOutputDirectory,
                settings.ClientCodeOutputDirectory,
                settings.ServerCodeOutputDirectory,
                generateCode: codeTarget != ExportTarget.None,
                generateJson: true,
                generateBytes: false,
                targets: dataTarget,
                codeTargets: codeTarget,
                arraySeparators: new ArraySeparatorOptions(
                    settings.ArrayInnerSeparator,
                    settings.ArrayMiddleSeparator,
                    settings.ArrayOuterSeparator));
            var exportResult = new ExportService().ExportAll(merged, options);
            if (dataTarget.HasFlag(ExportTarget.Client))
                logs.Add(Entry("PASS", $"客户端：{CountFilesUnder(exportResult.Files, settings.ClientOutputDirectory)} 个 JSON 文件写入"));
            if (dataTarget.HasFlag(ExportTarget.Server))
                logs.Add(Entry("PASS", $"服务器：{CountFilesUnder(exportResult.Files, settings.ServerOutputDirectory)} 个 JSON 文件写入"));
            if (codeTarget.HasFlag(ExportTarget.Client))
                logs.Add(Entry("PASS", $"客户端 C#：{CountFilesWithExtension(exportResult.Files, ".cs", settings.ClientCodeOutputDirectory)} 个文件写入 {settings.ClientCodeOutputDirectory}"));
            if (codeTarget.HasFlag(ExportTarget.Server))
                logs.Add(Entry("PASS", $"服务器 C#：{CountFilesWithExtension(exportResult.Files, ".cs", settings.ServerCodeOutputDirectory)} 个文件写入 {settings.ServerCodeOutputDirectory}"));
            stopwatch.Stop();
            logs.Add(Entry("DONE", $"完成：{rowCount} 行 · {exportResult.Files.Count} 个文件 · 耗时 {stopwatch.Elapsed.TotalSeconds:F1}s"));
            return new BuildResult(true, issues, logs, merged.Length, rowCount, exportResult.Files.Count, stopwatch.Elapsed);
        }
        catch (Exception error)
        {
            stopwatch.Stop();
            logs.Add(Entry("FAIL", error.Message));
            return new BuildResult(false, issues, logs, merged.Length, rowCount, 0, stopwatch.Elapsed);
        }
    }

    private static string TargetText(ExportTarget target) => target switch
    {
        ExportTarget.Both => "客户端 + 服务器",
        ExportTarget.Client => "客户端",
        ExportTarget.Server => "服务器",
        _ => "无"
    };

    private static BuildLogEntry Entry(string level, string message) => new(DateTime.Now, level, message);

    private static int CountFilesUnder(IEnumerable<string> files, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return files.Count(file => Path.GetFullPath(file).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountFilesWithExtension(IEnumerable<string> files, string extension, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return files.Count(file =>
            string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase)
            && Path.GetFullPath(file).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
