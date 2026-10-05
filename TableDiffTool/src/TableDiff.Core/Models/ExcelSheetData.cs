// 用途：表示一个 Excel 工作表的可比较数据。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ExcelSheetData
{
    public ExcelSheetData(string name, int headerRowIndex, IReadOnlyList<string> headers, string keyColumn)
    {
        Name = name;
        HeaderRowIndex = headerRowIndex;
        Headers = headers.ToArray();
        KeyColumn = keyColumn;
    }

    public string Name { get; }

    public int HeaderRowIndex { get; }

    public IReadOnlyList<string> Headers { get; }

    public string KeyColumn { get; }

    public List<ExcelRowData> Rows { get; } = [];
}
