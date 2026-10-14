// 用途：定义配置表数据和代码导出的客户端、服务器目标标志。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TableTool.Core.Models;

[Flags]
public enum ExportTarget
{
    None = 0,
    Client = 1,
    Server = 2,
    Both = Client | Server
}
