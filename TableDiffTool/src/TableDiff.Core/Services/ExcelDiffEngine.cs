// 用途：比较两个 Excel 工作簿并生成稳定的行级、单元格级变更。
// 编写时间：2026-10-05。
// 作者：Codex。
using TableDiff.Core.IO;
using TableDiff.Core.Models;

namespace TableDiff.Core.Services;

public static class ExcelDiffEngine
{
    public static ExcelDiffResult Compare(string leftPath, string rightPath, ExcelDiffOptions? options = null)
    {
        options ??= new ExcelDiffOptions();
        var left = ExcelWorkbookLoader.Load(leftPath, options);
        var right = ExcelWorkbookLoader.Load(rightPath, options);
        var result = new ExcelDiffResult
        {
            LeftPath = leftPath,
            RightPath = rightPath,
            LeftWorkbook = left,
            RightWorkbook = right
        };

        var sheetNames = left.Sheets.Select(sheet => sheet.Name)
            .Concat(right.Sheets.Select(sheet => sheet.Name))
            .Distinct(StringComparer.Ordinal);

        foreach (var sheetName in sheetNames)
        {
            CompareSheet(left.Sheets.FirstOrDefault(sheet => sheet.Name == sheetName), right.Sheets.FirstOrDefault(sheet => sheet.Name == sheetName), result.Entries, options);
        }

        return result;
    }

    private static void CompareSheet(ExcelSheetData? left, ExcelSheetData? right, ICollection<DiffEntry> entries, ExcelDiffOptions options)
    {
        var sheetName = left?.Name ?? right!.Name;
        var keyColumn = left?.KeyColumn ?? right!.KeyColumn;
        var headers = (left?.Headers ?? []).Concat(right?.Headers ?? []).Distinct(StringComparer.Ordinal).ToArray();
        var leftRows = left?.Rows.ToDictionary(row => row.Key, StringComparer.Ordinal) ?? new Dictionary<string, ExcelRowData>(StringComparer.Ordinal);
        var rightRows = right?.Rows.ToDictionary(row => row.Key, StringComparer.Ordinal) ?? new Dictionary<string, ExcelRowData>(StringComparer.Ordinal);

        foreach (var key in leftRows.Keys.Concat(rightRows.Keys).Distinct(StringComparer.Ordinal))
        {
            leftRows.TryGetValue(key, out var leftRow);
            rightRows.TryGetValue(key, out var rightRow);

            if (leftRow is null || rightRow is null)
            {
                entries.Add(new DiffEntry
                {
                    SheetName = sheetName,
                    KeyColumn = keyColumn,
                    RowKey = key,
                    ColumnName = "(row)",
                    Kind = leftRow is null ? DiffKind.Added : DiffKind.Removed,
                    LeftRow = leftRow?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal),
                    RightRow = rightRow?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal)
                });
                continue;
            }

            foreach (var header in headers)
            {
                leftRow.Values.TryGetValue(header, out var leftValue);
                rightRow.Values.TryGetValue(header, out var rightValue);
                var equal = options.CaseSensitive
                    ? string.Equals(leftValue, rightValue, StringComparison.Ordinal)
                    : string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase);

                if (!equal)
                {
                    entries.Add(new DiffEntry
                    {
                        SheetName = sheetName,
                        KeyColumn = keyColumn,
                        RowKey = key,
                        ColumnName = header,
                        Kind = DiffKind.Modified,
                        LeftValue = leftValue,
                        RightValue = rightValue,
                        LeftRow = new Dictionary<string, string?>(leftRow.Values, StringComparer.Ordinal),
                        RightRow = new Dictionary<string, string?>(rightRow.Values, StringComparer.Ordinal)
                    });
                }
            }
        }
    }
}
