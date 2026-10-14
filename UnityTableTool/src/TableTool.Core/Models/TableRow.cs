// 用途：保存配置表数据行、原始单元格值和来源坐标。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public sealed record TableRow(
    int SourceRow,
    bool IsTest,
    IReadOnlyDictionary<string, string?> RawValues,
    string? SourceName = null,
    IReadOnlyDictionary<int, string?>? RawCells = null,
    IReadOnlyDictionary<string, string?>? RawFieldValues = null)
{
    public string? GetValue(FieldSchema field)
    {
        if (RawFieldValues is not null && RawFieldValues.TryGetValue(field.Identity, out var fieldValue))
            return fieldValue;
        if (RawCells is not null && RawCells.TryGetValue(field.SourceColumn, out var cell))
            return cell;
        return RawValues.GetValueOrDefault(field.Name);
    }
}
