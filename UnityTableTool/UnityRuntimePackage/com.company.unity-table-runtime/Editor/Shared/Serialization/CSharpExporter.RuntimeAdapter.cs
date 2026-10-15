// 用途：按完整嵌套类型路径生成 Runtime 适配器及复杂值、数组和 Unity 结构值读取方法。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Serialization
{
    public static partial class CSharpExporter
    {
        private static void AppendRuntimeAdapter(StringBuilder builder, TableDocument document)
        {
            var typeName = $"{document.Schema.Name}Data";
            builder.AppendLine();
            builder.AppendLine($"public static class {document.Schema.Name}TableAdapter");
            builder.AppendLine("{");
            builder.AppendLine($"    public static {typeName} FromRow(TableRowView row) => new {typeName}()");
            builder.AppendLine("    {");
            foreach (var field in document.Schema.Fields)
                builder.AppendLine($"        {ToPascal(field.Name)} = {ToReadExpression(field, typeName)},");
            builder.AppendLine("    };");

            foreach (var field in document.Schema.Fields.Where(field => field.Type.IsComplex))
                AppendComplexReaders(builder, field.Type, $"{typeName}.{ToComplexTypeName(field.Name, field.Type)}", "    ");

            foreach (var group in document.Schema.Fields.SelectMany(field => EnumerateUnityTypes(field.Type)).Distinct())
                AppendUnityReaders(builder, group.BaseType, group.Dimensions);
            builder.AppendLine("}");
        }

        private static string ToReadExpression(FieldSchema field, string ownerType)
        {
            var key = Escape(field.Name);
            if (field.Type.IsComplex)
            {
                var helper = ToReaderName($"{ownerType}.{ToComplexTypeName(field.Name, field.Type)}");
                return $"{helper}(row.Read<{ToRuntimeReadType(field.Type)}>(\"{key}\"))";
            }
            if (IsUnityType(field.Type.BaseType))
                return $"{ToUnityReaderName(field.Type)}(row.Read<{ToComponentArrayType(field.Type.Dimensions)}>(\"{key}\"))";
            return $"row.Read<{ToCSharpType(field.Type, field.Name)}>(\"{key}\")";
        }

        private static void AppendComplexReaders(StringBuilder builder, TypeDescriptor type, string typeName, string indent)
        {
            var helper = ToReaderName(typeName);
            var valueType = type.IsMap ? $"Dictionary<{ToCSharpType(type.KeyType!, "key")}, {typeName}>" : typeName;
            var targetType = valueType + string.Concat(Enumerable.Repeat("[]", type.Dimensions));
            builder.AppendLine();
            if (type.Dimensions > 0)
            {
                builder.AppendLine($"{indent}private static {targetType} {helper}({ToRuntimeReadType(type)} value) => value.Select(item => {helper}(item)).ToArray();");
                AppendComplexReaders(builder, WithoutOneDimension(type), typeName, indent);
                return;
            }

            if (type.IsLocalization)
            {
                builder.AppendLine($"{indent}private static {targetType} {helper}(RuntimeLocalizationValue value) => new {targetType} {{ Key = value.Key }};");
                return;
            }
            if (type.IsBeans)
            {
                builder.AppendLine($"{indent}private static {targetType} {helper}(RuntimeBeanValue value) => new {targetType}");
                builder.AppendLine($"{indent}{{");
                foreach (var field in type.BeanFields ?? Array.Empty<BeanFieldDescriptor>())
                {
                    var expression = ToValueReadExpression(field.Type, field.Name, $"value.Get(\"{Escape(field.Name)}\")", typeName);
                    builder.AppendLine($"{indent}    {ToPascal(field.Name)} = {expression},");
                }
                builder.AppendLine($"{indent}}};");
                foreach (var field in type.BeanFields ?? Array.Empty<BeanFieldDescriptor>())
                    if (field.Type.IsComplex)
                        AppendComplexReaders(builder, field.Type, $"{typeName}.{ToComplexTypeName(field.Name, field.Type)}", indent);
                return;
            }
            if (type.IsMap)
            {
                var keyType = ToCSharpType(type.KeyType!, "key");
                builder.AppendLine($"{indent}private static {targetType} {helper}(RuntimeMapValue value) => value.Entries.ToDictionary(");
                builder.AppendLine($"{indent}    item => RuntimeValueConverter.Convert<{keyType}>(item.Key, \"map.key\"),");
                builder.AppendLine($"{indent}    item => new {typeName}");
                builder.AppendLine($"{indent}    {{");
                builder.AppendLine($"{indent}        Key = RuntimeValueConverter.Convert<{keyType}>(item.Key, \"map.key\"),");
                builder.AppendLine($"{indent}        Text = {ToValueReadExpression(type.ValueType!, "text", "item.Text", typeName)},");
                builder.AppendLine($"{indent}        Bean = {ToValueReadExpression(type.BeanType!, "bean", "item.Bean", typeName)},");
                builder.AppendLine($"{indent}    }});");
                if (type.ValueType!.IsComplex)
                    AppendComplexReaders(builder, type.ValueType, $"{typeName}.{ToComplexTypeName("text", type.ValueType)}", indent);
                if (type.BeanType!.IsComplex)
                    AppendComplexReaders(builder, type.BeanType, $"{typeName}.{ToComplexTypeName("bean", type.BeanType)}", indent);
            }
        }

        private static string ToValueReadExpression(TypeDescriptor type, string fieldName, string raw, string ownerType)
        {
            if (type.IsComplex)
                return $"{ToReaderName($"{ownerType}.{ToComplexTypeName(fieldName, type)}")}(RuntimeValueConverter.Convert<{ToRuntimeReadType(type)}>({raw}, \"{Escape(fieldName)}\"))";
            if (IsUnityType(type.BaseType))
                return $"{ToUnityReaderName(type)}(RuntimeValueConverter.Convert<{ToComponentArrayType(type.Dimensions)}>({raw}, \"{Escape(fieldName)}\"))";
            return $"RuntimeValueConverter.Convert<{ToCSharpType(type, fieldName)}>({raw}, \"{Escape(fieldName)}\")";
        }

        private static void AppendUnityReaders(StringBuilder builder, string baseType, int dimensions)
        {
            if (dimensions > 0)
            {
                AppendUnityReaders(builder, baseType, dimensions - 1);
                var innerMethod = $"Read{baseType}{(dimensions == 1 ? string.Empty : (dimensions - 1).ToString())}";
                builder.AppendLine();
                builder.AppendLine($"    private static {ToUnityArrayType(baseType, dimensions)} Read{baseType}{dimensions}({ToComponentArrayType(dimensions)} value) => value.Select(item => {innerMethod}(item)).ToArray();");
                return;
            }

            var components = baseType switch
            {
                "Vector2" => "value[0], value[1]",
                "Vector3" => "value[0], value[1], value[2]",
                "Vector4" or "Quaternion" => "value[0], value[1], value[2], value[3]",
                "Color" => "value[0], value[1], value[2], value.Length > 3 ? value[3] : 1f",
                _ => throw new InvalidOperationException($"Unsupported Unity type '{baseType}'.")
            };
            builder.AppendLine();
            builder.AppendLine($"    private static {baseType} Read{baseType}(float[] value) => new {baseType}({components});");
        }

        // 路径分隔符和原有下划线分别编码，区分不同嵌套位置的同名类型。
        private static string ToReaderName(string typeName) => "Read" + typeName.Replace("_", "__").Replace(".", "_");
        private static string ToUnityReaderName(TypeDescriptor type) =>
            $"Read{type.BaseType}{(type.Dimensions == 0 ? string.Empty : type.Dimensions.ToString())}";

        private static string ToRuntimeReadType(TypeDescriptor type)
        {
            var baseType = type.IsLocalization ? "RuntimeLocalizationValue" : type.IsBeans ? "RuntimeBeanValue" : "RuntimeMapValue";
            return baseType + string.Concat(Enumerable.Repeat("[]", type.Dimensions));
        }

        private static TypeDescriptor WithoutOneDimension(TypeDescriptor type) => type with { Dimensions = type.Dimensions - 1 };
    }
}
