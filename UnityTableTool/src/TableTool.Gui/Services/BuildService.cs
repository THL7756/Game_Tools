// 用途：执行选中配置表的关联展开、校验和 JSON/C# 分端打表。
// 最近修改日期：2026-10-06

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
        ExportTarget target)
    {
        var stopwatch = Stopwatch.StartNew();
        var logs = new List<BuildLogEntry>();
        var allDocuments = allTables
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document))
            .ToArray();
        var selectedDocuments = TableSelectionResolver.Expand(allDocuments, selectedSourceNames).ToArray();
        if (selectedDocuments.Length == 0)
        {
            logs.Add(Entry("INFO", "未选择配置表。"));
            return new BuildResult(false, [], logs, 0, 0, 0, stopwatch.Elapsed);
        }

        var merged = TableDocumentMerger.Merge(selectedDocuments).ToArray();
        var issues = merged.SelectMany(document => new TableValidator().Validate(document)).ToList();
        issues.AddRange(GetMissingReferenceIssues(allDocuments, selectedDocuments));
        var targetText = target switch
        {
            ExportTarget.Both => "客户端 + 服务器",
            ExportTarget.Client => "客户端",
            _ => "服务器"
        };
        logs.Add(Entry("INFO", $"开始打表：{merged.Length} 张逻辑表 → {targetText}"));

        if (issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
        {
            foreach (var issue in issues.Where(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
                logs.Add(Entry("FAIL", $"{ValidationIssueFormatter.Headline(issue)}：{ValidationIssueFormatter.Detail(issue)}"));
            stopwatch.Stop();
            return new BuildResult(false, issues, logs, merged.Length, 0, 0, stopwatch.Elapsed);
        }

        var rowCount = merged.Sum(document => document.Rows.Count(row => !row.IsTest));
        logs.Add(Entry("PASS", string.Join(" · ", merged.Select(document => $"{document.Schema.Name} {document.Rows.Count(row => !row.IsTest)} 行"))));
        var warningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        if (warningCount > 0)
        {
            foreach (var issue in issues.Where(issue => issue.Severity == ValidationSeverity.Warning))
                logs.Add(Entry("WARN", $"{ValidationIssueFormatter.Headline(issue)}：{ValidationIssueFormatter.Detail(issue)}"));
            logs.Add(Entry("WARN", $"检查通过，保留 {warningCount} 条提示警告"));
        }

        try
        {
            var options = new ExportOptions(
                settings.ClientOutputDirectory,
                settings.ServerOutputDirectory,
                Path.Combine(settings.ProjectRootDirectory, "Code"),
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: target);
            var exportResult = new ExportService().ExportAll(merged, options);
            if (target.HasFlag(ExportTarget.Client))
                logs.Add(Entry("PASS", $"客户端：{CountFilesUnder(exportResult.Files, settings.ClientOutputDirectory)} 个 JSON 文件写入"));
            if (target.HasFlag(ExportTarget.Server))
                logs.Add(Entry("PASS", $"服务器：{CountFilesUnder(exportResult.Files, settings.ServerOutputDirectory)} 个 JSON 文件写入"));
            logs.Add(Entry("PASS", $"C#：{CountFilesWithExtension(exportResult.Files, ".cs", Path.Combine(settings.ProjectRootDirectory, "Code"))} 个文件写入 Code"));
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

    private static IEnumerable<ValidationIssue> GetMissingReferenceIssues(
        IReadOnlyList<TableDocument> allDocuments,
        IReadOnlyList<TableDocument> selectedDocuments)
    {
        foreach (var name in TableSelectionResolver.FindMissingReferences(allDocuments, selectedDocuments))
        {
            yield return new ValidationIssue(
                "REFERENCE_MISSING",
                ValidationSeverity.Warning,
                $"引用表 {name} 未找到。",
                "当前选择",
                Suggestion: "补全引用表，或移除对应引用字段。");
        }
    }

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
