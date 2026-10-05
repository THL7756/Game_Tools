// 用途：表示工作表中的一行结构化数据。
// 编写时间：2026-10-05。
// 作者：Codex。
namespace TableDiff.Core.Models;

public sealed class ExcelRowData
{
    public ExcelRowData(string key, int rowIndex, IReadOnlyDictionary<string, string?> values)
    {
        Key = key;
        RowIndex = rowIndex;
        Values = new Dictionary<string, string?>(values, StringComparer.Ordinal);
    }

    public string Key { get; }

    public int RowIndex { get; }

    public Dictionary<string, string?> Values { get; }
}
