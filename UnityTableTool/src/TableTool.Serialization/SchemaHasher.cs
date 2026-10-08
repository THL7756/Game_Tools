// 用途：根据表结构生成稳定的 schema 哈希。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.Security.Cryptography;
using System.Text;
using TableTool.Core.Models;

namespace TableTool.Serialization;

public static class SchemaHasher
{
    public static string Compute(TableSchema schema)
    {
        var canonical = $"singleton={schema.IsSingleton}\n" + string.Join("\n", schema.Fields.Select(field =>
            $"{field.Name}|{field.Type.BaseType}|{field.Type.Dimensions}|{field.Target}|{field.DefaultValue ?? string.Empty}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
