// 用途：将 Excel Diff 变更按指定方向安全写回目标工作簿。
// 编写时间：2026-10-05。
// 作者：Codex。
using TableDiff.Core.IO;
using TableDiff.Core.Models;

namespace TableDiff.Core.Services;

public static class ExcelPatchApplier
{
    public static ApplyResult Apply(string leftPath, string rightPath, ExcelDiffResult diff, ApplyDirection direction, IEnumerable<DiffEntry> selectedEntries)
    {
        var targetPath = direction == ApplyDirection.LeftToRight ? rightPath : leftPath;
        var entries = selectedEntries.ToArray();
        var targetData = ExcelWorkbookLoader.Load(targetPath, new ExcelDiffOptions
        {
            KeyColumn = entries.FirstOrDefault()?.KeyColumn
        });
        var conflicts = new List<string>();

        foreach (var entry in entries)
        {
            var targetSheet = targetData.Sheets.FirstOrDefault(sheet => sheet.Name == entry.SheetName);
            var sourceRow = direction == ApplyDirection.LeftToRight ? entry.LeftRow : entry.RightRow;
            var expectedRow = direction == ApplyDirection.LeftToRight ? entry.RightRow : entry.LeftRow;
            if (targetSheet is null)
            {
                targetSheet = new ExcelSheetData(entry.SheetName, 0, sourceRow.Keys.ToArray(), entry.KeyColumn);
                targetData.Sheets.Add(targetSheet);
            }

            var targetRow = targetSheet.Rows.FirstOrDefault(row => row.Key == entry.RowKey);
            if (entry.Kind == DiffKind.Modified)
            {
                if (targetRow is null || targetRow.Values.GetValueOrDefault(entry.ColumnName) != expectedRow.GetValueOrDefault(entry.ColumnName))
                {
                    conflicts.Add($"{entry.SheetName}/{entry.RowKey}/{entry.ColumnName}");
                    continue;
                }

                targetRow.Values[entry.ColumnName] = sourceRow.GetValueOrDefault(entry.ColumnName);
            }
            else if (entry.Kind == DiffKind.Added)
            {
                if (direction == ApplyDirection.LeftToRight)
                {
                    if (targetRow is null || !RowEquals(targetRow, expectedRow))
                    {
                        conflicts.Add($"{entry.SheetName}/{entry.RowKey}");
                        continue;
                    }

                    targetSheet.Rows.Remove(targetRow);
                }
                else
                {
                    if (targetRow is not null)
                    {
                        conflicts.Add($"{entry.SheetName}/{entry.RowKey}");
                        continue;
                    }

                    targetSheet.Rows.Add(new ExcelRowData(entry.RowKey, targetSheet.Rows.Count + 1, sourceRow));
                }
            }
            else
            {
                if (direction == ApplyDirection.LeftToRight)
                {
                    if (targetRow is not null)
                    {
                        conflicts.Add($"{entry.SheetName}/{entry.RowKey}");
                        continue;
                    }

                    targetSheet.Rows.Add(new ExcelRowData(entry.RowKey, targetSheet.Rows.Count + 1, sourceRow));
                }
                else
                {
                    if (targetRow is null || !RowEquals(targetRow, expectedRow))
                    {
                        conflicts.Add($"{entry.SheetName}/{entry.RowKey}");
                        continue;
                    }

                    targetSheet.Rows.Remove(targetRow);
                }
            }
        }

        if (conflicts.Count > 0)
        {
            return new ApplyResult { TargetPath = targetPath, BackupPath = string.Empty, Conflicts = conflicts };
        }

        var backupPath = targetPath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(targetPath, backupPath, overwrite: false);
        var tempPath = targetPath + ".tmp_" + Guid.NewGuid().ToString("N") + ".xlsx";
        try
        {
            XlsxPackageWriter.Write(tempPath, targetData);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return new ApplyResult { TargetPath = targetPath, BackupPath = backupPath, AppliedCount = entries.Length };
    }

    private static bool RowEquals(ExcelRowData row, IReadOnlyDictionary<string, string?> expected)
    {
        return expected.All(pair => row.Values.GetValueOrDefault(pair.Key) == pair.Value);
    }
}
