// 用途：递归遍历复杂字段类型，统一发现 beans/map 内部的表引用和 localization 引用。
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

namespace TableTool.Core.Parsing;

public sealed record ComplexTypeReference(
    string FieldName,
    TypeDescriptor Type,
    bool IsLocalization = false);

public static class ComplexTypeReferenceWalker
{
    public static IReadOnlyList<ComplexTypeReference> GetReferences(FieldSchema field)
    {
        var result = new List<ComplexTypeReference>();
        AddField(result, field.Name, field.Type);
        return result;
    }

    public static IReadOnlyList<ComplexTypeReference> GetReferences(TypeDescriptor type)
    {
        var result = new List<ComplexTypeReference>();
        AddType(result, string.Empty, type);
        return result;
    }

    private static void AddField(ICollection<ComplexTypeReference> result, string fieldName, TypeDescriptor type)
    {
        if (fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && fieldName.Length > 3)
            result.Add(new ComplexTypeReference(fieldName, type));
        AddType(result, fieldName, type);
    }

    private static void AddType(ICollection<ComplexTypeReference> result, string prefix, TypeDescriptor type)
    {
        if (type.IsLocalization)
        {
            result.Add(new ComplexTypeReference(prefix, type, true));
            return;
        }

        if (type.IsBeans)
        {
            foreach (var beanField in type.BeanFields ?? [])
            {
                var name = string.IsNullOrWhiteSpace(prefix) ? beanField.Name : $"{prefix}.{beanField.Name}";
                AddField(result, name, beanField.Type);
            }
            return;
        }

        if (type.IsMap && type.BeanType is not null)
            AddType(result, prefix, type.BeanType);
    }
}
