// 用途：把校验问题转换为包含文件、工作表和 Excel 单元格位置的展示文本。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Core.Models;

namespace TableTool.Gui.Services;

public static class ValidationIssueFormatter
{
    public static string Location(ValidationIssue issue)
    {
        var source = issue.SourceName ?? string.Empty;
        var parts = source.Split("::", 2, StringSplitOptions.None);
        var fileName = Path.GetFileName(parts[0]);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = parts[0];

        var sheet = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? $"[{parts[1]}]"
            : string.Empty;
        var cell = issue.SourceRow is int row && issue.SourceColumn is int column
            ? $"{ColumnName(column)}{row}"
            : string.IsNullOrWhiteSpace(issue.FieldName) ? "表级" : issue.FieldName;

        return $"{fileName}{sheet}!{cell}";
    }

    public static string Headline(ValidationIssue issue) =>
        $"{Location(issue)} · {issue.Code}";

    public static string Detail(ValidationIssue issue) =>
        issue.Suggestion is null ? issue.Message : $"{issue.Message} {issue.Suggestion}";

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
