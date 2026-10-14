// 用途：解析工具端和 Unity Runtime 共用的字段类型 AST，包括基础类型、数组、enum、localization、beans 和 map。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Company.UnityTableRuntime.Shared
{
    public sealed class RuntimeArraySeparators
    {
        public RuntimeArraySeparators(string inner = "#", string middle = "|", string outer = ";")
        {
            Inner = inner;
            Middle = middle;
            Outer = outer;
        }

        public string Inner { get; }
        public string Middle { get; }
        public string Outer { get; }
        public static RuntimeArraySeparators Default { get; } = new();

        public bool IsValid => IsSingle(Inner)
            && IsSingle(Middle)
            && IsSingle(Outer)
            && Inner != Middle
            && Inner != Outer
            && Middle != Outer
            && Inner != ","
            && Middle != ","
            && Outer != ",";

        private static bool IsSingle(string value) =>
            value is not null && value.Length == 1 && !char.IsWhiteSpace(value[0]);
    }

    public sealed class RuntimeBeanField
    {
        public RuntimeBeanField(string name, RuntimeTypeDescriptor type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public RuntimeTypeDescriptor Type { get; }
    }

    public sealed class RuntimeTypeDescriptor
    {
        private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "int", "long", "float", "double", "bool", "string",
            "Vector2", "Vector3", "Vector4", "Color", "Quaternion", "localization"
        };

        public RuntimeTypeDescriptor(
            string baseType,
            int dimensions,
            IReadOnlyList<string>? enumValues = null,
            bool isValid = true,
            string? rawText = null,
            IReadOnlyList<RuntimeBeanField>? beanFields = null,
            RuntimeTypeDescriptor? keyType = null,
            RuntimeTypeDescriptor? valueType = null,
            RuntimeTypeDescriptor? beanType = null)
        {
            BaseType = baseType;
            Dimensions = dimensions;
            EnumValues = enumValues;
            IsValid = isValid;
            RawText = rawText;
            BeanFields = beanFields;
            KeyType = keyType;
            ValueType = valueType;
            BeanType = beanType;
        }

        public string BaseType { get; }
        public int Dimensions { get; }
        public IReadOnlyList<string>? EnumValues { get; }
        public bool IsValid { get; }
        public string? RawText { get; }
        public IReadOnlyList<RuntimeBeanField>? BeanFields { get; }
        public RuntimeTypeDescriptor? KeyType { get; }
        public RuntimeTypeDescriptor? ValueType { get; }
        public RuntimeTypeDescriptor? BeanType { get; }
        public bool IsEnum => BaseType.Equals("enum", StringComparison.OrdinalIgnoreCase);
        public bool IsLocalization => BaseType.Equals("localization", StringComparison.OrdinalIgnoreCase);
        public bool IsBeans => BaseType.Equals("beans", StringComparison.OrdinalIgnoreCase);
        public bool IsMap => BaseType.Equals("map", StringComparison.OrdinalIgnoreCase);
        public bool IsComplex => IsLocalization || IsBeans || IsMap;

        public string DisplayText
        {
            get
            {
                var core = IsEnum
                    ? $"enum({string.Join('|', EnumValues ?? Array.Empty<string>())})"
                    : IsBeans
                        ? $"beans({string.Join('|', (BeanFields ?? Array.Empty<RuntimeBeanField>()).Select(field => $"{field.Name}#{field.Type.DisplayText}"))})"
                        : IsMap
                            ? $"map({KeyType?.DisplayText})({ValueType?.DisplayText})({BeanType?.DisplayText})"
                            : BaseType;
                return core + string.Concat(Enumerable.Repeat("()", Dimensions));
            }
        }

        public static RuntimeTypeDescriptor Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new FormatException("Type cannot be empty.");

            var parser = new TypeExpressionParser(text.Trim());
            var parsed = parser.ParseType();
            parser.EnsureEnd();
            return parsed;
        }

        public static RuntimeTypeDescriptor Invalid(string text) =>
            new("unknown", 0, null, false, text);

        public IEnumerable<(string FieldName, RuntimeTypeDescriptor Type)> EnumerateNamedTypes()
        {
            if (IsBeans)
            {
                foreach (var field in BeanFields ?? Array.Empty<RuntimeBeanField>())
                    yield return (field.Name, field.Type);
            }

            if (IsMap && BeanType is not null)
            {
                foreach (var item in BeanType.EnumerateNamedTypes())
                    yield return item;
            }
        }

        private sealed class TypeExpressionParser
        {
            private readonly string text;
            private int index;

            public TypeExpressionParser(string text) => this.text = text;

            public RuntimeTypeDescriptor ParseType()
            {
                var core = ParseCore();
                var dimensions = 0;
                while (StartsWith("()"))
                {
                    index += 2;
                    dimensions++;
                }

                if (dimensions > 3)
                    throw new FormatException("Array dimensions cannot exceed three.");
                return dimensions == 0 ? core : CloneWithDimensions(core, dimensions);
            }

            public void EnsureEnd()
            {
                SkipWhitespace();
                if (index != text.Length)
                    throw new FormatException($"Unexpected type text near '{text[index..]}'.");
            }

            private RuntimeTypeDescriptor ParseCore()
            {
                SkipWhitespace();
                if (StartsWith("enum(", ignoreCase: true))
                    return ParseEnum();
                if (StartsWith("beans(", ignoreCase: true))
                    return ParseBeans();
                if (StartsWith("map(", ignoreCase: true))
                    return ParseMap();

                var start = index;
                while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_'))
                    index++;
                if (start == index)
                    throw new FormatException($"Unknown type near '{text[index..]}'.");
                var name = text[start..index];
                var canonical = SupportedTypes.FirstOrDefault(type => type.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (canonical is null)
                    throw new FormatException($"Unknown type '{name}'.");
                return new RuntimeTypeDescriptor(canonical, 0);
            }

            private RuntimeTypeDescriptor ParseEnum()
            {
                var content = ReadParenthesized("enum");
                var values = content.Split('|').Select(value => value.Trim()).ToArray();
                if (values.Length == 0
                    || values.Any(value => value.Length == 0)
                    || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    throw new FormatException("Enum type must contain unique non-empty values separated by '|'.");
                return new RuntimeTypeDescriptor("enum", 0, values);
            }

            private RuntimeTypeDescriptor ParseBeans()
            {
                var content = ReadParenthesized("beans");
                var parts = SplitTopLevel(content, '|');
                if (parts.Count == 0 || parts.Any(string.IsNullOrWhiteSpace))
                    throw new FormatException("beans(...) must contain at least one field.");

                var fields = new List<RuntimeBeanField>();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in parts)
                {
                    var separator = FindTopLevel(part, '#');
                    if (separator <= 0 || separator >= part.Length - 1)
                        throw new FormatException("Each beans field must use fieldName#type.");
                    var name = part[..separator].Trim();
                    if (name.Length == 0 || !names.Add(name))
                        throw new FormatException($"beans field '{name}' is empty or duplicated.");
                    var typeText = part[(separator + 1)..].Trim();
                    fields.Add(new RuntimeBeanField(name, ParseNested(typeText)));
                }
                return new RuntimeTypeDescriptor("beans", 0, beanFields: fields);
            }

            private RuntimeTypeDescriptor ParseMap()
            {
                var key = ParseNested(ReadParenthesized("map"));
                var value = ParseParenthesizedType();
                var bean = ParseParenthesizedType();
                SkipWhitespace();
                if (!value.BaseType.Equals("string", StringComparison.OrdinalIgnoreCase) || value.Dimensions != 0)
                    throw new FormatException("map value section must be string.");
                if (!bean.IsBeans || bean.Dimensions != 0)
                    throw new FormatException("map bean section must be beans(...).");
                return new RuntimeTypeDescriptor("map", 0, keyType: key, valueType: value, beanType: bean);
            }

            private RuntimeTypeDescriptor ParseParenthesizedType()
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != '(')
                    throw new FormatException("map(...) must contain exactly three parenthesized type sections.");
                var content = ReadParenthesized(null);
                return ParseNested(content);
            }

            private RuntimeTypeDescriptor ParseNested(string nested)
            {
                var parser = new TypeExpressionParser(nested.Trim());
                var parsed = parser.ParseType();
                parser.EnsureEnd();
                return parsed;
            }

            private string ReadParenthesized(string? name)
            {
                if (name is not null)
                {
                    SkipWhitespace();
                    if (!StartsWith(name + "(", ignoreCase: true))
                        throw new FormatException($"Expected {name}(...).");
                    index += name.Length;
                }

                SkipWhitespace();
                if (index >= text.Length || text[index] != '(')
                    throw new FormatException("Missing '(' in type expression.");
                index++;
                var start = index;
                var depth = 1;
                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '(')
                        depth++;
                    else if (text[index] == ')')
                        depth--;
                    index++;
                }
                if (depth != 0)
                    throw new FormatException("Unclosed parentheses in type expression.");
                return text[start..(index - 1)];
            }

            private bool StartsWith(string value, bool ignoreCase = false) =>
                index + value.Length <= text.Length
                && string.Compare(text, index, value, 0, value.Length,
                    ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;

            private void SkipWhitespace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }
        }

        private static RuntimeTypeDescriptor CloneWithDimensions(RuntimeTypeDescriptor source, int dimensions) =>
            new(source.BaseType, dimensions, source.EnumValues, source.IsValid, source.RawText,
                source.BeanFields, source.KeyType, source.ValueType, source.BeanType);

        private static List<string> SplitTopLevel(string text, char separator)
        {
            var result = new List<string>();
            var start = 0;
            var depth = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '(')
                    depth++;
                else if (text[index] == ')')
                    depth--;
                else if (text[index] == separator && depth == 0)
                {
                    result.Add(text[start..index]);
                    start = index + 1;
                }
            }
            result.Add(text[start..]);
            return result;
        }

        private static int FindTopLevel(string text, char separator)
        {
            var depth = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '(')
                    depth++;
                else if (text[index] == ')')
                    depth--;
                else if (text[index] == separator && depth == 0)
                    return index;
            }
            return -1;
        }
    }
}
