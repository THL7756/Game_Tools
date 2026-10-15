// 用途：复用已加载表的主键和字段索引查询多语言文本，避免逐行遍历或复制翻译字典。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

#nullable enable

using System;

namespace Company.UnityTableRuntime
{
    public sealed partial class TableManager
    {
        public string? GetLocalizationText(string key, string language)
        {
            var row = Get("localization", key);
            var document = tables["localization"].Document;
            if (!document.Fields.TryGetValue(language, out var field)
                || field.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
                return null;

            var text = document.View(row).GetValue(field.Name);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
