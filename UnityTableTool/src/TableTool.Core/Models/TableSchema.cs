// 用途：描述逻辑配置表的字段集合、主键和单例属性。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Models;

public sealed record TableSchema(
    string Name,
    IReadOnlyList<FieldSchema> Fields,
    string PrimaryKey,
    bool IsSingleton = false,
    IReadOnlyList<FieldSchema>? ValidationFields = null)
{
    public IReadOnlyList<FieldSchema> AllFields =>
        ValidationFields is null ? Fields : Fields.Concat(ValidationFields).ToArray();
}
