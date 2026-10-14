// 用途：保存一个正式配置表 Sheet 的来源、结构、数据和解析诊断。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TableTool.Core.Models;

public sealed record TableDocument(
    string SourceName,
    TableSchema Schema,
    IReadOnlyList<TableRow> Rows,
    IReadOnlyList<ValidationIssue>? ParseIssues = null);
