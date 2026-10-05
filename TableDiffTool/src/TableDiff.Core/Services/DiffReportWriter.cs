// 用途：将 Excel Diff 结果写成稳定的 JSON 或文本报告。
// 编写时间：2026-10-05。
// 作者：Codex。
using System.Text.Json;
using System.Text.Json.Serialization;
using TableDiff.Core.Models;

namespace TableDiff.Core.Services;

public static class DiffReportWriter
{
    public static string ToJson(ExcelDiffResult result)
    {
        var report = new
        {
            result.LeftPath,
            result.RightPath,
            Added = result.AddedCount,
            Removed = result.RemovedCount,
            Modified = result.ModifiedCount,
            Entries = result.Entries
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public static string ToText(ExcelDiffResult result)
    {
        var lines = new List<string>
        {
            $"Added: {result.AddedCount}, Removed: {result.RemovedCount}, Modified: {result.ModifiedCount}"
        };
        lines.AddRange(result.Entries.Select(entry => $"[{entry.Kind}] {entry.SheetName}/{entry.RowKey}/{entry.ColumnName}: {entry.LeftValue} -> {entry.RightValue}"));
        return string.Join(Environment.NewLine, lines);
    }
}
