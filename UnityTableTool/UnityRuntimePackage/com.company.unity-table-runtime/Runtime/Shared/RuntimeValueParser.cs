// 用途：提供工具端和 Unity Runtime 共用的标量、数组及复杂字段值解析，并保留结构化结果。
// 编写日期：2026-10-14
// 最近修改日期：2026-10-15
// 作者：Codex（按用户需求修改）

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Company.UnityTableRuntime.Shared
{
    public sealed class RuntimeBeanValue
    {
        public RuntimeBeanValue(IReadOnlyDictionary<string, object?> fields)
        {
            Fields = fields;
        }

        public IReadOnlyDictionary<string, object?> Fields { get; }

        public object? Get(string fieldName) => Fields.TryGetValue(fieldName, out var value) ? value : null;
    }

    public sealed class RuntimeMapEntry
    {
        public RuntimeMapEntry(object key, object? text, RuntimeBeanValue bean)
        {
            Key = key;
            Text = text;
            Bean = bean;
        }

        public object Key { get; }
        public object? Text { get; }
        public RuntimeBeanValue Bean { get; }
    }

    public sealed class RuntimeMapValue
    {
        public RuntimeMapValue(IReadOnlyList<RuntimeMapEntry> entries)
        {
            Entries = entries;
        }

        public IReadOnlyList<RuntimeMapEntry> Entries { get; }
    }

    public static class RuntimeValueParser
    {
        public static object Parse(
            string? text,
            RuntimeTypeDescriptor type,
            RuntimeArraySeparators? separators = null)
        {
            if (!type.IsValid)
                throw new FormatException($"字段类型 '{type.RawText ?? type.BaseType}' 无效。");
            separators ??= RuntimeArraySeparators.Default;
            if (!separators.IsValid)
                throw new FormatException("数组分隔符设置无效。");

            var value = text?.Trim() ?? string.Empty;
            if (type.IsComplex && type.Dimensions > 0 && value.StartsWith("[", StringComparison.Ordinal))
                return ParseStructuredJson(value, type, separators);
            if (type.Dimensions == 0)
                return ParseScalar(value, type, separators);
            return ParseLevel(value, type, separators, 0);
        }

        private static Array ParseLevel(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators, int level)
        {
            var separator = SeparatorFor(type.Dimensions, separators, level);
            var parts = SplitEscaped(
                text,
                separator,
                allowEmpty: false,
                preserveEscapes: true);
            if (parts.Count == 0)
                throw new FormatException($"Empty {type.BaseType} array.");

            var values = new object[parts.Count];
            for (var index = 0; index < parts.Count; index++)
                values[index] = level + 1 == type.Dimensions
                    ? ParseScalar(parts[index], type, separators)
                    : ParseLevel(parts[index], type, separators, level + 1);

            var result = CreateArray(type, level, values.Length);
            for (var index = 0; index < values.Length; index++)
                result.SetValue(values[index], index);
            return result;
        }

        private static object ParseScalar(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            if (type.IsComplex)
            {
                if (text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal))
                    return ParseStructuredJson(text, type, separators);
                return type.IsLocalization
                    ? ParseLocalization(text)
                    : type.IsBeans
                        ? ParseBeans(text, type, separators)
                        : ParseMap(text, type, separators);
            }

            if (text.Length == 0)
                throw new FormatException($"Empty {type.BaseType} value.");
            try
            {
                return type.BaseType.ToLowerInvariant() switch
                {
                    "int" => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    "long" => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    "float" => ParseFloating(text, "float", false),
                    "double" => ParseFloating(text, "double", true),
                    "bool" => ParseBool(text),
                    "string" => Unescape(text),
                    "enum" => ParseEnum(text, type),
                    "vector2" => ParseComponents(text, "Vector2", 2),
                    "vector3" => ParseComponents(text, "Vector3", 3),
                    "vector4" => ParseComponents(text, "Vector4", 4),
                    "color" => ParseColor(text),
                    "quaternion" => ParseComponents(text, "Quaternion", 4),
                    _ => throw new FormatException($"Unknown type '{type.BaseType}'.")
                };
            }
            catch (Exception error) when (error is FormatException or OverflowException or ArgumentException)
            {
                if (error is FormatException format
                    && format.Message.Contains("invalid for type", StringComparison.OrdinalIgnoreCase))
                    throw;
                throw new FormatException($"Value '{text}' is invalid for type '{type.BaseType}'.", error);
            }
        }

        private static RuntimeLocalizationValue ParseLocalization(string text)
        {
            text = Unescape(text);
            if (text.Length == 0)
                throw new FormatException("Localization key cannot be empty.");
            return new RuntimeLocalizationValue(text);
        }

        private static RuntimeBeanValue ParseBeans(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            var fields = type.BeanFields ?? Array.Empty<RuntimeBeanField>();
            var values = ParseDelimitedFields(text, fields, separators.Inner[0], separators);
            return new RuntimeBeanValue(values);
        }

        private static RuntimeMapValue ParseMap(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            if (type.KeyType is null || type.ValueType is null || type.BeanType is null)
                throw new FormatException("map type is incomplete.");
            var entries = new List<RuntimeMapEntry>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entryText in SplitEscaped(text, separators.Outer[0], allowEmpty: false, preserveEscapes: true))
            {
                var keySeparator = FindUnescaped(entryText, separators.Middle[0]);
                if (keySeparator <= 0 || keySeparator >= entryText.Length - 1)
                    throw new FormatException("map entry must contain key|value.");
                var keyText = entryText[..keySeparator];
                var valueText = entryText[(keySeparator + 1)..];
                var key = Parse(keyText, type.KeyType, separators);
                var beanFields = type.BeanType.BeanFields ?? Array.Empty<RuntimeBeanField>();
                var mapFields = new List<RuntimeBeanField>(beanFields.Count + 1)
                {
                    new RuntimeBeanField("text", type.ValueType)
                };
                mapFields.AddRange(beanFields);
                var parsedFields = ParseDelimitedFields(valueText, mapFields, separators.Inner[0], separators);
                var value = parsedFields["text"];
                var bean = new RuntimeBeanValue(beanFields.ToDictionary(
                    field => field.Name,
                    field => parsedFields[field.Name],
                    StringComparer.OrdinalIgnoreCase));
                var keyIdentity = Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!seenKeys.Add(keyIdentity))
                    throw new FormatException($"map key '{keyIdentity}' is duplicated.");
                entries.Add(new RuntimeMapEntry(key, value, bean));
            }
            return new RuntimeMapValue(entries);
        }

        private static object ParseStructuredJson(string text, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                return ReadJsonElement(document.RootElement, type, separators);
            }
            catch (JsonException error)
            {
                throw new FormatException("Complex field JSON is invalid.", error);
            }
        }

        private static object ReadJsonElement(JsonElement element, RuntimeTypeDescriptor type, RuntimeArraySeparators separators)
        {
            if (type.Dimensions > 0)
            {
                if (element.ValueKind != JsonValueKind.Array)
                    throw new FormatException("Complex array JSON must be an array.");
                var values = element.EnumerateArray()
                    .Select(item => ReadJsonElement(item, new RuntimeTypeDescriptor(type.BaseType, type.Dimensions - 1,
                        type.EnumValues, type.IsValid, type.RawText, type.BeanFields, type.KeyType, type.ValueType, type.BeanType), separators))
                    .ToArray();
                return values;
            }

            if (type.IsLocalization)
            {
                if (element.ValueKind != JsonValueKind.String)
                    throw new FormatException("Localization JSON must be a key string.");
                var key = element.GetString();
                if (string.IsNullOrWhiteSpace(key))
                    throw new FormatException("Localization JSON is missing key.");
                return new RuntimeLocalizationValue(key!);
            }

            if (type.IsBeans)
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Beans JSON must be an object.");
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in type.BeanFields ?? Array.Empty<RuntimeBeanField>())
                {
                    if (element.TryGetProperty(field.Name, out var property))
                        values[field.Name] = ReadJsonElement(property, field.Type, separators);
                }
                return new RuntimeBeanValue(values);
            }

            if (type.IsMap)
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Map JSON must be an object.");
                var entries = new List<RuntimeMapEntry>();
                var seenKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    var key = Parse(property.Name, type.KeyType!, separators);
                    var keyIdentity = Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!seenKeys.Add(keyIdentity))
                        throw new FormatException($"map key '{keyIdentity}' is duplicated.");
                    if (property.Value.ValueKind != JsonValueKind.Object)
                        throw new FormatException("Map entry JSON must be an object.");
                    var text = property.Value.TryGetProperty("text", out var textProperty)
                        ? ReadJsonElement(textProperty, type.ValueType!, separators)
                        : null;
                    var beanElement = property.Value.TryGetProperty("bean", out var beanProperty)
                        ? beanProperty
                        : default;
                    var bean = beanElement.ValueKind == JsonValueKind.Undefined
                        ? new RuntimeBeanValue(new Dictionary<string, object?>())
                        : (RuntimeBeanValue)ReadJsonElement(beanElement, type.BeanType!, separators);
                    entries.Add(new RuntimeMapEntry(key, text, bean));
                }
                return new RuntimeMapValue(entries);
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => Parse(element.GetString(), type, separators),
                JsonValueKind.Number => Parse(element.ToString(), type, separators),
                JsonValueKind.True or JsonValueKind.False => Parse(element.ToString(), type, separators),
                _ => throw new FormatException($"JSON value is invalid for type '{type.DisplayText}'.")
            };
        }

        private static object ParseFloating(string text, string baseType, bool isDouble)
        {
            if (isDouble)
            {
                var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
                return value;
            }
            var single = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(single) || float.IsInfinity(single))
                throw new FormatException($"Value '{text}' is invalid for type '{baseType}'.");
            return single;
        }

        private static double ParseDouble(string text)
        {
            var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new FormatException($"Value '{text}' is invalid for type 'double'.");
            return value;
        }

        private static object ParseComponents(string text, string baseType, int expectedCount)
        {
            var parts = SplitAndTrim(RemoveOptionalBrackets(text), ',');
            if (parts.Length != expectedCount || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Value '{text}' is invalid for type '{baseType}'; expected {expectedCount} comma-separated components.");
            return parts.Select(part => ParseDouble(part)).Select(value => (float)value).ToArray();
        }

        private static object ParseColor(string text)
        {
            var parts = SplitAndTrim(RemoveOptionalBrackets(text), ',');
            if (parts.Length is not (3 or 4) || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Value '{text}' is invalid for type 'Color'; expected three or four comma-separated components.");
            return parts.Select(part => (float)ParseDouble(part)).ToArray();
        }

        private static string RemoveOptionalBrackets(string value)
        {
            value = value.Trim();
            return value.Length >= 2
                && ((value[0] == '(' && value[^1] == ')')
                    || (value[0] == '[' && value[^1] == ']')
                    || (value[0] == '{' && value[^1] == '}'))
                ? value[1..^1].Trim()
                : value;
        }

        private static string[] SplitAndTrim(string value, char separator) =>
            value.Split(separator).Select(part => part.Trim()).ToArray();

        private static bool ParseBool(string text) => text.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new FormatException($"Value '{text}' is invalid for type 'bool'.")
        };

        private static string ParseEnum(string text, RuntimeTypeDescriptor type)
        {
            if (type.EnumValues?.Contains(text, StringComparer.Ordinal) == true)
                return text;
            throw new FormatException($"值 '{text}' 不在允许的 enum 值列表中。");
        }

        private static Type GetRuntimeType(string baseType) => baseType.ToLowerInvariant() switch
        {
            "int" => typeof(int),
            "long" => typeof(long),
            "float" => typeof(float),
            "double" => typeof(double),
            "bool" => typeof(bool),
            "string" or "enum" => typeof(string),
            "vector2" or "vector3" or "vector4" or "color" or "quaternion" => typeof(float[]),
            _ => typeof(object)
        };

        private static Array CreateArray(RuntimeTypeDescriptor type, int level, int length)
        {
            var elementType = GetRuntimeType(type.BaseType);
            for (var index = type.Dimensions - 1; index > level; index--)
                elementType = elementType.MakeArrayType();
            return Array.CreateInstance(elementType, length);
        }

        private static char SeparatorFor(int dimensions, RuntimeArraySeparators separators, int level)
        {
            if (dimensions is < 1 or > 3)
                throw new FormatException("Array dimensions must be between one and three.");
            return new[] { separators.Inner[0], separators.Middle[0], separators.Outer[0] }[dimensions - level - 1];
        }

        private static Dictionary<string, object?> ParseDelimitedFields(
            string text,
            IReadOnlyList<RuntimeBeanField> fields,
            char separator,
            RuntimeArraySeparators separators)
        {
            var parts = SplitEscaped(text, separator, allowEmpty: false, preserveEscapes: true);
            if (!TryParseDelimitedFields(parts, fields, separator, separators, 0, 0, out var values))
                throw new FormatException($"Complex value does not match {fields.Count} fields.");
            return values;
        }

        private static bool TryParseDelimitedFields(
            IReadOnlyList<string> parts,
            IReadOnlyList<RuntimeBeanField> fields,
            char separator,
            RuntimeArraySeparators separators,
            int fieldIndex,
            int partIndex,
            out Dictionary<string, object?> values)
        {
            values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (fieldIndex == fields.Count)
                return partIndex == parts.Count;

            var remainingFields = fields.Count - fieldIndex - 1;
            var maxEnd = parts.Count - remainingFields;
            for (var end = partIndex + 1; end <= maxEnd; end++)
            {
                var candidate = string.Join(separator, parts.Skip(partIndex).Take(end - partIndex));
                object parsed;
                try
                {
                    parsed = Parse(candidate, fields[fieldIndex].Type, separators);
                }
                catch (FormatException)
                {
                    continue;
                }

                if (!TryParseDelimitedFields(parts, fields, separator, separators, fieldIndex + 1, end, out var remainder))
                    continue;
                values[fields[fieldIndex].Name] = parsed;
                foreach (var pair in remainder)
                    values[pair.Key] = pair.Value;
                return true;
            }
            return false;
        }

        private static List<string> SplitEscaped(
            string text,
            char separator,
            bool allowEmpty,
            bool preserveEscapes = false)
        {
            var result = new List<string>();
            var builder = new System.Text.StringBuilder();
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (current == '\\')
                {
                    if (index + 1 >= text.Length || !"#|;\\".Contains(text[index + 1]))
                        throw new FormatException("Invalid escape sequence. Only \\#, \\|, \\; and \\\\ are supported.");
                    var escaped = text[++index];
                    if (preserveEscapes)
                        builder.Append('\\');
                    builder.Append(escaped);
                    continue;
                }
                if (current == separator)
                {
                    var part = builder.ToString();
                    if (!allowEmpty && part.Length == 0)
                        throw new FormatException("Empty element in complex value.");
                    result.Add(part);
                    builder.Clear();
                    continue;
                }
                builder.Append(current);
            }
            var final = builder.ToString();
            if (!allowEmpty && final.Length == 0)
                throw new FormatException("Empty element in complex value.");
            result.Add(final);
            return result;
        }

        private static int FindUnescaped(string text, char separator)
        {
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '\\')
                {
                    index++;
                    continue;
                }
                if (text[index] == separator)
                    return index;
            }
            return -1;
        }

        private static string Unescape(string text)
        {
            var parts = SplitEscaped(text, '\0', allowEmpty: true);
            return parts.Single();
        }
    }

    public static class RuntimeValueConverter
    {
        public static T Convert<T>(object? value, string fieldName) =>
            (T)Convert(value, typeof(T), fieldName);

        private static object Convert(object? value, Type targetType, string fieldName)
        {
            if (value is null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
                    return null!;
                throw new FormatException($"Field '{fieldName}' has no value for '{targetType.Name}'.");
            }
            if (targetType.IsInstanceOfType(value))
                return value;
            if (targetType.IsEnum && value is string enumText)
                return Enum.Parse(targetType, enumText, ignoreCase: false);
            if (targetType.IsArray && value is Array source)
            {
                var elementType = targetType.GetElementType()!;
                var output = Array.CreateInstance(elementType, source.Length);
                for (var index = 0; index < source.Length; index++)
                    output.SetValue(Convert(source.GetValue(index), elementType, fieldName), index);
                return output;
            }
            if (targetType.IsGenericType
                && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && value is RuntimeMapValue map)
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments()[1];
                var dictionary = (IDictionary)Activator.CreateInstance(targetType)!;
                foreach (var entry in map.Entries)
                {
                    var key = Convert(entry.Key, keyType, fieldName);
                    var item = Convert(entry, valueType, fieldName);
                    dictionary.Add(key, item);
                }
                return dictionary;
            }
            if (value is RuntimeMapEntry mapEntry && targetType != typeof(RuntimeMapEntry))
                return ConvertObject(mapEntry, targetType, fieldName);
            if (value is RuntimeBeanValue bean)
                return ConvertBean(bean, targetType, fieldName);
            if (value is RuntimeLocalizationValue localization)
                return ConvertLocalization(localization, targetType, fieldName);
            if (targetType == typeof(string))
                return value.ToString() ?? string.Empty;
            if (value is IConvertible)
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)!;
            throw new FormatException($"Field '{fieldName}' cannot be converted to '{targetType.Name}'.");
        }

        private static object ConvertBean(RuntimeBeanValue bean, Type targetType, string fieldName)
        {
            if (targetType == typeof(RuntimeBeanValue))
                return bean;
            var instance = Activator.CreateInstance(targetType)
                ?? throw new FormatException($"Cannot create complex field type '{targetType.Name}'.");
            foreach (var member in GetWritableMembers(targetType))
            {
                if (!bean.Fields.TryGetValue(member.Name, out var raw))
                    continue;
                member.SetValue(instance, Convert(raw, member.MemberType, $"{fieldName}.{member.Name}"));
            }
            return instance;
        }

        private static object ConvertLocalization(RuntimeLocalizationValue localization, Type targetType, string fieldName)
        {
            if (targetType == typeof(RuntimeLocalizationValue))
                return localization;
            var instance = Activator.CreateInstance(targetType)
                ?? throw new FormatException($"Cannot create localization type '{targetType.Name}'.");
            foreach (var member in GetWritableMembers(targetType))
            {
                if (member.Name.Equals("Key", StringComparison.OrdinalIgnoreCase))
                    member.SetValue(instance, localization.Key);
            }
            return instance;
        }

        private static object ConvertObject(object value, Type targetType, string fieldName)
        {
            var instance = Activator.CreateInstance(targetType)
                ?? throw new FormatException($"Cannot create map entry type '{targetType.Name}'.");
            foreach (var member in GetWritableMembers(targetType))
            {
                var raw = member.Name.Equals("Key", StringComparison.OrdinalIgnoreCase)
                    ? ((RuntimeMapEntry)value).Key
                    : member.Name.Equals("Text", StringComparison.OrdinalIgnoreCase)
                        ? ((RuntimeMapEntry)value).Text
                        : member.Name.Equals("Bean", StringComparison.OrdinalIgnoreCase)
                            ? ((RuntimeMapEntry)value).Bean
                            : null;
                if (raw is not null)
                    member.SetValue(instance, Convert(raw, member.MemberType, $"{fieldName}.{member.Name}"));
            }
            return instance;
        }

        private static IReadOnlyList<WritableMember> GetWritableMembers(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly)
                .Select(field => new WritableMember(field.Name, field.FieldType, (instance, value) => field.SetValue(instance, value)))
                .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite)
                    .Select(property => new WritableMember(property.Name, property.PropertyType, (instance, value) => property.SetValue(instance, value, null))))
                .ToArray();

        private sealed class WritableMember
        {
            public WritableMember(string name, Type memberType, Action<object, object?> setter)
            {
                Name = name;
                MemberType = memberType;
                Setter = setter;
            }

            public string Name { get; }
            public Type MemberType { get; }
            private Action<object, object?> Setter { get; }
            public void SetValue(object instance, object? value) => Setter(instance, value);
        }
    }
}
