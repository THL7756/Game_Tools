// 用途：为 Unity Editor 窗口提供配置表扫描、规则校验和客户端/服务器打表 API。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using TableTool.Serialization;

namespace Company.UnityTableTool.Editor;

public sealed record UnityTableSheetEntry(
    string SourceName,
    string SheetName,
    TableDocument Document);

public sealed class UnityTableEntry
{
    public UnityTableEntry(
        string fileKey,
        string displayName,
        string sourcePath,
        IReadOnlyList<UnityTableSheetEntry> sheets)
    {
        FileKey = fileKey;
        DisplayName = displayName;
        SourcePath = sourcePath;
        Sheets = sheets;
        IsSelected = true;
    }

    public string FileKey { get; }
    public string DisplayName { get; }
    public string SourcePath { get; }
    public IReadOnlyList<UnityTableSheetEntry> Sheets { get; }
    public bool IsSelected { get; set; }
}

public sealed record UnityTableCatalog(
    IReadOnlyList<UnityTableEntry> Tables,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Infos);

public sealed record UnityTableBuildResult(
    bool Success,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Files,
    int TableCount,
    int RowCount);

public static class UnityTableToolEditorApi
{
    public static UnityTableCatalog LoadCatalog(UnityTableToolSettings settings)
    {
        var errors = new List<string>();
        var infos = new List<string>();
        var documents = new List<TableDocument>();
        var tableRoot = settings.ResolvePath(settings.tableDirectory);

        if (!Directory.Exists(tableRoot))
            return new UnityTableCatalog([], [$"源表目录不存在：{tableRoot}"], []);

        var grids = TableFileReader.ReadDirectory(
            tableRoot,
            (path, error) => errors.Add($"{Path.GetRelativePath(tableRoot, path)}：{error.Message}"));
        var parser = new TableSchemaParser();
        foreach (var grid in grids)
        {
            try
            {
                var parsed = parser.ParseWithDiagnostics(grid);
                if (parsed.Document is not null)
                    documents.Add(parsed.Document);
                else
                    infos.Add($"{grid.SourceName}：{parsed.Issues.FirstOrDefault()?.Message ?? "不是正式配置表"}");
            }
            catch (FormatException error)
            {
                errors.Add($"{grid.SourceName}：{error.Message}");
            }
        }

        var tables = documents
            .GroupBy(document => GetFileKey(document.SourceName), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new UnityTableEntry(
                group.Key,
                Path.GetFileNameWithoutExtension(group.Key),
                Path.GetFullPath(Path.Combine(tableRoot, group.Key.Replace('/', Path.DirectorySeparatorChar))),
                group.OrderBy(document => GetSheetName(document.SourceName), StringComparer.OrdinalIgnoreCase)
                    .Select(document => new UnityTableSheetEntry(
                        document.SourceName,
                        GetSheetName(document.SourceName),
                        document))
                    .ToArray()))
            .ToArray();

        return new UnityTableCatalog(tables, errors, infos);
    }

    public static UnityTableBuildResult Build(
        UnityTableToolSettings settings,
        UnityTableCatalog catalog)
    {
        var allDocuments = catalog.Tables
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document))
            .ToArray();
        if (catalog.Errors.Count > 0)
            return new UnityTableBuildResult(false, [], catalog.Errors, [], 0, 0);
        var selectedSourceNames = catalog.Tables
            .Where(table => table.IsSelected)
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document.SourceName))
            .ToArray();
        if (settings.DataTargets == ExportTarget.None && settings.CodeTargets == ExportTarget.None)
            return new UnityTableBuildResult(false, [], ["至少选择一个数据或代码输出目标。"], [], 0, 0);
        if (selectedSourceNames.Length == 0)
            return new UnityTableBuildResult(false, [], ["至少选择一张配置表。"], [], 0, 0);

        var validation = TableBatchValidator.Validate(
            allDocuments,
            selectedSourceNames,
            settings.ArraySeparators);
        var issues = validation.Issues;
        if (issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
            return new UnityTableBuildResult(false, issues, [], [], validation.Documents.Count, 0);

        try
        {
            var merged = TableDocumentMerger.Merge(validation.Documents).ToArray();
            var options = new ExportOptions(
                settings.ResolvePath(settings.clientDataDirectory),
                settings.ResolvePath(settings.serverDataDirectory),
                settings.ResolvePath(settings.clientCodeDirectory),
                settings.ResolvePath(settings.serverCodeDirectory),
                generateCode: settings.CodeTargets != ExportTarget.None,
                generateJson: settings.DataTargets != ExportTarget.None,
                generateBytes: false,
                targets: settings.DataTargets,
                codeTargets: settings.CodeTargets,
                arraySeparators: settings.ArraySeparators);
            var export = new ExportService().ExportAll(merged, options);
            var rowCount = merged.Sum(document => document.Rows.Count(row => !row.IsTest));
            return new UnityTableBuildResult(true, issues, [], export.Files, merged.Length, rowCount);
        }
        catch (Exception error)
        {
            return new UnityTableBuildResult(false, issues, [error.Message], [], validation.Documents.Count, 0);
        }
    }

    private static string GetFileKey(string sourceName)
    {
        var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0 ? sourceName[..separator] : sourceName;
    }

    private static string GetSheetName(string sourceName)
    {
        var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0
            ? sourceName[(separator + 2)..]
            : Path.GetFileNameWithoutExtension(sourceName);
    }
}
