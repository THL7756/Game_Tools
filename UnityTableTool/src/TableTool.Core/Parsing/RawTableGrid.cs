// 用途：保存读取器产生的原始 Sheet 网格和来源名称。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Parsing;

public sealed record RawTableGrid(
    string SourceName,
    IReadOnlyList<IReadOnlyList<string?>> Rows);
