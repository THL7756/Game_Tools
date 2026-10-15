// 用途：兼容工具端的字段值解析入口，并委托给 Runtime Package 的共享实现。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Company.UnityTableRuntime.Shared;
using TableTool.Core.Models;

namespace TableTool.Core.Parsing
{
    public static class ValueParser
    {
        public static object Parse(
            string text,
            TypeDescriptor type,
            ArraySeparatorOptions? separators = null)
        {
            var sharedType = type.ToRuntime();
            var sharedSeparators = separators is null
                ? RuntimeArraySeparators.Default
                : new RuntimeArraySeparators(separators.Inner, separators.Middle, separators.Outer);
            return RuntimeValueParser.Parse(text, sharedType, sharedSeparators);
        }
    }
}
