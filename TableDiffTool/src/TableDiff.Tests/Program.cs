// 用途：执行 TableDiff.Core 的可重复集成测试。
// 编写时间：2026-10-05。
// 作者：Codex。
using TableDiff.Core.IO;
using TableDiff.Core.Models;
using TableDiff.Core.Services;

var root = Path.Combine(Path.GetTempPath(), "TableDiffTests_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var left = Path.Combine(root, "left.xlsx");
var right = Path.Combine(root, "right.xlsx");
var reverseLeft = Path.Combine(root, "reverse-left.xlsx");
var reverseRight = Path.Combine(root, "reverse-right.xlsx");

try
{
    CreateWorkbook(left, ("1001", "Sword", "10"), ("1002", "Shield", "20"));
    CreateWorkbook(right, ("1001", "Sword+", "12"), ("1003", "Potion", "5"));

    var diff = ExcelDiffEngine.Compare(left, right, new ExcelDiffOptions { KeyColumn = "id" });
    Assert(diff.Entries.Any(entry => entry.RowKey == "1001" && entry.ColumnName == "name" && entry.Kind == DiffKind.Modified), "modified cell missing");
    Assert(diff.Entries.Any(entry => entry.RowKey == "1002" && entry.Kind == DiffKind.Removed), "removed row missing");
    Assert(diff.Entries.Any(entry => entry.RowKey == "1003" && entry.Kind == DiffKind.Added), "added row missing");

    File.Copy(left, reverseLeft);
    File.Copy(right, reverseRight);
    var forward = ExcelPatchApplier.Apply(left, right, diff, ApplyDirection.LeftToRight, diff.Entries);
    Assert(forward.Succeeded, "left-to-right apply failed");
    Assert(ExcelDiffEngine.Compare(left, right, new ExcelDiffOptions { KeyColumn = "id" }).Entries.Count == 0, "left-to-right result differs");

    var reverseDiff = ExcelDiffEngine.Compare(reverseLeft, reverseRight, new ExcelDiffOptions { KeyColumn = "id" });
    var backward = ExcelPatchApplier.Apply(reverseLeft, reverseRight, reverseDiff, ApplyDirection.RightToLeft, reverseDiff.Entries);
    Assert(backward.Succeeded, "right-to-left apply failed");
    Assert(ExcelDiffEngine.Compare(reverseLeft, reverseRight, new ExcelDiffOptions { KeyColumn = "id" }).Entries.Count == 0, "right-to-left result differs");
    Console.WriteLine("All TableDiff tests passed.");
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

static void CreateWorkbook(string path, params (string Id, string Name, string Value)[] rows)
{
    var sheet = new ExcelSheetData("Items", 0, ["id", "name", "value"], "id");
    for (var index = 0; index < rows.Length; index++)
    {
        sheet.Rows.Add(new ExcelRowData(rows[index].Id, index + 1, new Dictionary<string, string?>
        {
            ["id"] = rows[index].Id,
            ["name"] = rows[index].Name,
            ["value"] = rows[index].Value
        }));
    }

    var workbook = new ExcelWorkbookData();
    workbook.Sheets.Add(sheet);
    XlsxPackageWriter.Write(path, workbook);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
