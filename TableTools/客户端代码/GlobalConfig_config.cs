// 用途：GlobalConfig 客户端表格数据与 Unity JsonUtility 读取包装
// 最近修改日期：2026-10-07

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TableTools.Generated
{
    [Serializable]
    public sealed class GlobalConfigConfig
    {
        [FormerlySerializedAs("team_num")] public int TeamNum;
        [FormerlySerializedAs("team_num1")] public int TeamNum1;

        public static GlobalConfigConfig LoadFromJson(string Json)
        {
            return JsonUtility.FromJson<GlobalConfigConfig>(Json);
        }
    }
}
