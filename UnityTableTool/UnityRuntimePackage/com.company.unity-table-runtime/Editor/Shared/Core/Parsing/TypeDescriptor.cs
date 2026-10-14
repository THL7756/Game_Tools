// 用途：包装工具端和 Unity Runtime 共用的字段类型 AST。
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

namespace TableTool.Core.Parsing;

public sealed record BeanFieldDescriptor(string Name, TypeDescriptor Type);

public sealed record TypeDescriptor(
    string BaseType,
    int Dimensions,
    IReadOnlyList<string>? EnumValues = null,
    bool IsValid = true,
    string? RawText = null,
    IReadOnlyList<BeanFieldDescriptor>? BeanFields = null,
    TypeDescriptor? KeyType = null,
    TypeDescriptor? ValueType = null,
    TypeDescriptor? BeanType = null)
{

    public static TypeDescriptor Parse(string text)
    {
        var parsed = RuntimeTypeDescriptor.Parse(text);
        return FromRuntime(parsed);
    }

    public static TypeDescriptor Invalid(string text) =>
        new("unknown", 0, null, false, text);

    public bool IsEnum => BaseType.Equals("enum", StringComparison.OrdinalIgnoreCase);
    public bool IsLocalization => BaseType.Equals("localization", StringComparison.OrdinalIgnoreCase);
    public bool IsBeans => BaseType.Equals("beans", StringComparison.OrdinalIgnoreCase);
    public bool IsMap => BaseType.Equals("map", StringComparison.OrdinalIgnoreCase);
    public bool IsComplex => IsLocalization || IsBeans || IsMap;

    public string DisplayText => IsEnum
        ? $"enum({string.Join('|', EnumValues ?? [])})" + string.Concat(Enumerable.Repeat("()", Dimensions))
        : IsBeans
            ? $"beans({string.Join('|', (BeanFields ?? []).Select(field => $"{field.Name}#{field.Type.DisplayText}"))})" + string.Concat(Enumerable.Repeat("()", Dimensions))
            : IsMap
                ? $"map({KeyType?.DisplayText})({ValueType?.DisplayText})({BeanType?.DisplayText})" + string.Concat(Enumerable.Repeat("()", Dimensions))
                : BaseType + string.Concat(Enumerable.Repeat("()", Dimensions));

    public RuntimeTypeDescriptor ToRuntime() => new(
        BaseType,
        Dimensions,
        EnumValues,
        IsValid,
        RawText,
        BeanFields?.Select(field => new RuntimeBeanField(field.Name, field.Type.ToRuntime())).ToArray(),
        KeyType?.ToRuntime(),
        ValueType?.ToRuntime(),
        BeanType?.ToRuntime());

    private static TypeDescriptor FromRuntime(RuntimeTypeDescriptor parsed) => new(
        parsed.BaseType,
        parsed.Dimensions,
        parsed.EnumValues,
        parsed.IsValid,
        parsed.RawText,
        parsed.BeanFields?.Select(field => new BeanFieldDescriptor(field.Name, FromRuntime(field.Type))).ToArray(),
        parsed.KeyType is null ? null : FromRuntime(parsed.KeyType),
        parsed.ValueType is null ? null : FromRuntime(parsed.ValueType),
        parsed.BeanType is null ? null : FromRuntime(parsed.BeanType));
}
