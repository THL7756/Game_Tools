#nullable enable
using System;

namespace Company.UnityTableRuntime
{
    public sealed class TableRuntimeSettings
    {
        public bool PreferJsonInDevelopment { get; set; }
        public Func<string, byte[]>? LoadBytes { get; set; }
    }
}
