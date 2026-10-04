using System;

namespace Company.UnityTableRuntime;

public sealed class TableRuntimeSettings
{
    public bool PreferJsonInDevelopment { get; init; }
    public Func<string, byte[]>? LoadBytes { get; init; }
}
