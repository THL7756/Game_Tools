// 用途：表示一个可比较的 Excel 工作簿。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ExcelWorkbookData
{
    public List<ExcelSheetData> Sheets { get; } = [];
}
