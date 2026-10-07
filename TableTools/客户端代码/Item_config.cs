// 用途：Item 客户端表格数据与 Unity JsonUtility 读取包装
// 最近修改日期：2026-10-07

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TableTools.Generated
{
    [Serializable]
    public sealed class ItemConfigRow
    {
        [FormerlySerializedAs("id")] public int Id;
        [FormerlySerializedAs("name")] public string Name;
        [FormerlySerializedAs("price")] public int Price;
    }

    [Serializable]
    public sealed class ItemConfig
    {
        [FormerlySerializedAs("Rows")] public ItemConfigRow[] Rows = Array.Empty<ItemConfigRow>();

        public static ItemConfig LoadFromJson(string Json)
        {
            return JsonUtility.FromJson<ItemConfig>(Json);
        }
    }
}
