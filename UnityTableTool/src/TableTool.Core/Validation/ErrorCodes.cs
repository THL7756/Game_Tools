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
    public const string TableMergeInvalid = "TABLE_MERGE_ERROR";
}
