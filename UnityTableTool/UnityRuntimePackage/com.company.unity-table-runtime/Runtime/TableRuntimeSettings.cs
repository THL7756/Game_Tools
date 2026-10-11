#nullable enable
// 用途：保存 Unity Runtime 客户端表加载偏好和字节加载回调。
// 编写日期：2026-10-11
// 作者：Codex（按用户需求修改）

using System;

namespace Company.UnityTableRuntime
{
    public sealed class TableRuntimeSettings
    {
        public bool PreferJsonInDevelopment { get; set; }
        public Func<string, byte[]>? LoadBytes { get; set; }
    }
}
