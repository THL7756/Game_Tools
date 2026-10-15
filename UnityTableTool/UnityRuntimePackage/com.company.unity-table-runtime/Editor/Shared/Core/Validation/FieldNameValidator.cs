// 用途：统一检查普通字段及嵌套子字段中的连字符，并保留定义单元格坐标。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Core.Validation
{
    public static class FieldNameValidator
    {
        public static IReadOnlyList<ValidationIssue> Validate(
            FieldSchema field,
            string sourceName,
            int? nameColumn = null,
            int? typeColumn = null)
        {
            var issues = new List<ValidationIssue>();
            AddIssue(field.Name, field.Name, field.NameRow, nameColumn ?? field.SourceColumn + 1);
            ValidateType(field.Type, field.Name);
            return issues;

            void AddIssue(string name, string path, int row, int column)
            {
                if (name.IndexOf('-') < 0)
                    return;

                issues.Add(new ValidationIssue(
                    ErrorCodes.FieldNameInvalid,
                    ValidationSeverity.Error,
                    $"字段名“{path}”不能包含连字符“-”。",
                    sourceName,
                    row,
                    column,
                    field.Name,
                    Suggestion: "请将字段名中的连字符“-”改为下划线“_”。"));
            }

            void ValidateType(TypeDescriptor type, string path)
            {
                if (type.IsBeans)
                {
                    foreach (var child in type.BeanFields ?? Array.Empty<BeanFieldDescriptor>())
                    {
                        var childPath = path + "." + child.Name;
                        AddIssue(child.Name, childPath, field.TypeRow, typeColumn ?? field.SourceColumn + 1);
                        ValidateType(child.Type, childPath);
                    }
                }
                if (type.IsMap)
                {
                    ValidateType(type.KeyType!, path + ".key");
                    ValidateType(type.ValueType!, path + ".text");
                    ValidateType(type.BeanType!, path + ".bean");
                }
            }
        }
    }
}
