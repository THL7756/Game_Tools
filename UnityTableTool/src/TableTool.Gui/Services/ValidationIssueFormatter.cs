// 用途：把校验问题格式化为统一的来源、位置和说明字段。
// 用途：将校验问题转换为中文来源、单元格定位和日志文本。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.IO;
using TableTool.Core.Models;

namespace TableTool.Gui.Services;

public static class ValidationIssueFormatter
{
    public static string Level(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Fatal => "致命",
        ValidationSeverity.Error => "错误",
        ValidationSeverity.Warning => "警告",
        _ => "信息"
    };

    public static string Source(ValidationIssue issue)
    {
        var parts = SplitSource(issue.SourceName);
        return parts.Sheet.Length == 0 ? parts.File : $"{parts.File}·{parts.Sheet}";
    }

    public static string Location(ValidationIssue issue) =>
        issue.SourceRow is int row && issue.SourceColumn is int column
            ? $"{ColumnName(column)}{row}"
            : string.IsNullOrWhiteSpace(issue.FieldName) ? "表级" : issue.FieldName;

    public static string Headline(ValidationIssue issue) =>
        $"{Source(issue)}·{Location(issue)}";

    public static string Description(ValidationIssue issue) => issue.Message switch
    {
        null or "" => issue.Code switch
        {
            "FIELD_ARRAY_SEPARATOR_INVALID" => "数组分隔符或数组层级不正确。",
            "FIELD_DEFAULT_INVALID" => "字段默认值不符合字段类型。",
            "PRIMARY_KEY_MISSING" => "主键不能为空。",
            "PRIMARY_KEY_DUPLICATE" => "主键重复。",
            "FIELD_TARGET_INVALID" => "客户端/服务器标记不正确。",
            "ENUM_VALUE_INVALID" => "枚举值不在允许范围内。",
            "TABLE_REFERENCE_MISSING" => "引用的逻辑表不存在。",
            "TABLE_MERGE_ERROR" => "同名表的字段、类型、默认值或测试列定义不一致。",
            _ => "配置表校验未通过。"
        },
        _ => issue.Message
    };

    public static string Detail(ValidationIssue issue) =>
        string.IsNullOrWhiteSpace(issue.Suggestion)
            ? Description(issue)
            : $"{Description(issue)} {LocalizeSuggestion(issue)}";

    public static string LogText(ValidationIssue issue) =>
        $"{Headline(issue)}\r\n问题：{Detail(issue)}";

    private static string LocalizeSuggestion(ValidationIssue issue) => issue.Code switch
    {
        "FIELD_TYPE_UNKNOWN" => "请检查字段类型定义。",
        "FIELD_ARRAY_SEPARATOR_INVALID" => "请检查数组分隔符和维度。",
        "FIELD_DEFAULT_INVALID" => "请按字段类型填写默认值。",
        "PRIMARY_KEY_MISSING" => "请填写主键或设置有效默认值。",
        "PRIMARY_KEY_DUPLICATE" => "请修改重复的主键值。",
        "FIELD_TARGET_INVALID" => "只能填写空值、c、s 或 cs。",
        "ENUM_VALUE_INVALID" => "请使用 enum 声明中的成员。",
        "ENUM_CSHARP_IDENTIFIER_INVALID" => "请使用合法的 C# 枚举成员名称。",
        "TABLE_REFERENCE_MISSING" => "请补全引用表或移除引用字段。",
        "TABLE_MERGE_ERROR" => "请让同名表对应字段定义保持一致。",
        _ => "请检查对应单元格。"
    };

    private static (string File, string Sheet) SplitSource(string sourceName)
    {
        var source = sourceName ?? string.Empty;
        var parts = source.Split("::", 2, StringSplitOptions.None);
        var fileName = Path.GetFileName(parts[0]);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = parts[0];

        var sheet = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : string.Empty;
        return (fileName, sheet);
    }

    private static string ColumnName(int column)
    {
        if (column < 1)
            return "?";

        var result = string.Empty;
        var value = column;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }

        return result;
    }
}
