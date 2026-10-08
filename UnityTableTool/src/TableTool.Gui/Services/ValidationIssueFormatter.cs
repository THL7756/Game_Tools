// 用途：把校验问题格式化为统一的来源、位置和说明字段。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Core.Models;

namespace TableTool.Gui.Services;

public static class ValidationIssueFormatter
{
    public static string Level(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Fatal => "致命",
        ValidationSeverity.Error => "错误",
        ValidationSeverity.Warning => "警告",
        _ => "信息"
    };

    public static string Source(ValidationIssue issue)
    {
        var parts = SplitSource(issue.SourceName);
        return parts.Sheet.Length == 0 ? parts.File : $"{parts.File} · {parts.Sheet}";
    }

    public static string Location(ValidationIssue issue) =>
        issue.SourceRow is int row && issue.SourceColumn is int column
            ? $"{ColumnName(column)}{row}"
            : string.IsNullOrWhiteSpace(issue.FieldName) ? "表级" : issue.FieldName;

    public static string Headline(ValidationIssue issue) =>
        $"{Source(issue)} · {Location(issue)} · {issue.Code}";

    public static string Detail(ValidationIssue issue) =>
        issue.Suggestion is null ? issue.Message : $"{issue.Message} {issue.Suggestion}";

    private static (string File, string Sheet) SplitSource(string sourceName)
    {
        var source = sourceName ?? string.Empty;
        var parts = source.Split("::", 2, StringSplitOptions.None);
        var fileName = Path.GetFileName(parts[0]);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = parts[0];

        var sheet = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : string.Empty;
        return (fileName, sheet);
    }

    private static string ColumnName(int column)
    {
        if (column < 1)
            return "?";

        var result = string.Empty;
        var value = column;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }

        return result;
    }
}
