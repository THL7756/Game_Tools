// 用途：保存配置表中的多语言键，翻译文本统一通过 TableManager 查询。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

namespace Company.UnityTableRuntime.Shared
{
    public sealed class RuntimeLocalizationValue
    {
        public RuntimeLocalizationValue(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}
