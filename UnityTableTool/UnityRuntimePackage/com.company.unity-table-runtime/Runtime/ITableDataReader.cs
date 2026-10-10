#nullable enable
using System;

namespace Company.UnityTableRuntime
{
    public interface ITableDataReader
    {
        bool CanRead(byte[] data);
        TableRuntimeDocument Read(byte[] data);
    }
}
