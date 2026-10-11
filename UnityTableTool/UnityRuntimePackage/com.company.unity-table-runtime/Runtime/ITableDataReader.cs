#nullable enable
// 用途：定义 Unity Runtime 数据读取器的统一读取与格式探测接口。
// 编写日期：2026-10-11
// 作者：Codex（按用户需求修改）

using System;

namespace Company.UnityTableRuntime
{
    public interface ITableDataReader
    {
        bool CanRead(byte[] data);
        TableRuntimeDocument Read(byte[] data);
    }
}
