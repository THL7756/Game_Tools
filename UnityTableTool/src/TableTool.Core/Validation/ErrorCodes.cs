// 用途：集中定义配置表校验问题代码。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

namespace TableTool.Core.Validation;

public static class ErrorCodes
{
    public const string FieldTypeUnknown = "FIELD_TYPE_UNKNOWN";
    public const string FieldArraySeparatorInvalid = "FIELD_ARRAY_SEPARATOR_INVALID";
    public const string FieldDefaultInvalid = "FIELD_DEFAULT_INVALID";
    public const string PrimaryKeyDuplicate = "PRIMARY_KEY_DUPLICATE";
    public const string PrimaryKeyMissing = "PRIMARY_KEY_MISSING";
    public const string FieldTargetInvalid = "FIELD_TARGET_INVALID";
    public const string SingletonRowCountInvalid = "SINGLETON_ROW_COUNT_INVALID";
    public const string TableReferenceMissing = "TABLE_REFERENCE_MISSING";
    public const string TableReferenceKeyMissing = "TABLE_REFERENCE_KEY_MISSING";
    public const string TableReferenceTypeMismatch = "TABLE_REFERENCE_TYPE_MISMATCH";
    public const string TableMergeInvalid = "TABLE_MERGE_ERROR";
    public const string FieldNameDuplicate = "FIELD_NAME_DUPLICATE";
    public const string FieldNameMissing = "FIELD_NAME_MISSING";
    public const string EnumValueInvalid = "ENUM_VALUE_INVALID";
    public const string EnumCSharpInvalid = "ENUM_CSHARP_IDENTIFIER_INVALID";
    public const string TableNotFormal = "TABLE_NOT_FORMAL";
}
