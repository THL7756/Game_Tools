// 用途：描述配置表字段、字段类型、端目标和源表坐标。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using TableTool.Core.Parsing;

namespace TableTool.Core.Models;

public enum FieldTarget
{
    Both,
    Client,
    Server
}

public sealed record FieldSchema(
    int SourceColumn,
    string Name,
    string Description,
    TypeDescriptor Type,
    FieldTarget Target,
    string? DefaultValue,
    int NameRow = 4,
    int TypeRow = 3,
    int TargetRow = 5,
    int DefaultRow = 6,
    bool IsTest = false);
