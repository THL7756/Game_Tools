// 用途：生成配置表导出的清单文件，记录输出表和结构哈希。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

using TableTool.Core.Models;

namespace TableTool.Serialization
{
    public sealed class ManifestExporter
    {
        public string Export(TableDocument document, IEnumerable<string> formats)
        {
            return JsonSerializer.Serialize(new
            {
                tableName = document.Schema.Name,
                isSingleton = document.Schema.IsSingleton,
                schemaHash = SchemaHasher.Compute(document.Schema),
                rowCount = document.Rows.Count(row => !row.IsTest),
                formats = formats.OrderBy(format => format, StringComparer.Ordinal).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
