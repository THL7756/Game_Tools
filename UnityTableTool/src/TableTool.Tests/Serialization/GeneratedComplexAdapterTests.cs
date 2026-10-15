// 用途：实际编译生成的嵌套复杂类型，并验证 JSON/UTB1 适配器读取和复杂数组。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System.Collections;
using System.Text;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Company.UnityTableRuntime;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class GeneratedComplexAdapterTests
{
    private const string MapType = "map(int)(string)(beans(count#int))";
    private const string MapValue = "{\"1\":{\"text\":\"entry\",\"bean\":{\"count\":10}}}";

    [Fact]
    public void MapBeanUsesThePublicEntryTypeScope()
    {
        foreach (var row in ReadBothFormats(Document(("value", MapType, MapValue))))
        {
            var entry = ((IDictionary)Field(row, "Value"))[1]!;
            Assert.Equal("GeneratedData+ValueEntry+BeanBean", Field(entry, "Bean").GetType().FullName);
            Assert.Equal(10, Field(Field(entry, "Bean"), "Count"));
            Assert.Equal("entry", Field(entry, "Text"));
        }
    }

    [Fact]
    public void NestedBeanMapAndLocalizationReadersKeepTheirOwnerScopes()
    {
        var type = "beans(payload#beans(label#localization|items#map(int)(string)(beans(count#int))))";
        var raw = "{\"payload\":{\"label\":\"greeting\",\"items\":" + MapValue + "}}";
        foreach (var row in ReadBothFormats(Document(("value", type, raw))))
        {
            var payload = Field(Field(row, "Value"), "Payload");
            Assert.Equal("greeting", Field(Field(payload, "Label"), "Key"));
            Assert.Equal(10, Field(Field(((IDictionary)Field(payload, "Items"))[1]!, "Bean"), "Count"));
        }
    }

    [Fact]
    public void ReadsMapInsideMapEntryAndItsBean()
    {
        var type = "map(int)(string)(beans(items#" + MapType + "()))";
        var raw = "{\"2\":{\"text\":\"greeting\",\"bean\":{\"items\":[" + MapValue + "]}}}";
        foreach (var row in ReadBothFormats(Document(("value", type, raw))))
        {
            var entry = ((IDictionary)Field(row, "Value"))[2]!;
            Assert.Equal("greeting", Field(entry, "Text"));
            var items = (Array)Field(Field(entry, "Bean"), "Items");
            var nested = ((IDictionary)items.GetValue(0)!)[1]!;
            Assert.Equal(10, Field(Field(nested, "Bean"), "Count"));
        }
    }

    [Fact]
    public void SameChildNamesInSeparateBranchesUseSeparateReaders()
    {
        var textMap = "map(int)(string)(beans(count#string))";
        var document = Document(("left", MapType, MapValue),
            ("right", textMap, "{\"1\":{\"text\":\"other\",\"bean\":{\"count\":\"ten\"}}}"),
            ("first", "beans(payload#beans(count#int))", "{\"payload\":{\"count\":10}}"),
            ("second", "beans(payload#beans(count#string))", "{\"payload\":{\"count\":\"ten\"}}"));
        foreach (var row in ReadBothFormats(document))
        {
            Assert.Equal(10, Field(Field(((IDictionary)Field(row, "Left"))[1]!, "Bean"), "Count"));
            Assert.Equal("ten", Field(Field(((IDictionary)Field(row, "Right"))[1]!, "Bean"), "Count"));
            Assert.Equal(10, Field(Field(Field(row, "First"), "Payload"), "Count"));
            Assert.Equal("ten", Field(Field(Field(row, "Second"), "Payload"), "Count"));
        }
    }

    [Theory]
    [InlineData("localization", 1)]
    [InlineData("localization", 2)]
    [InlineData("localization", 3)]
    [InlineData("beans(count#int)", 1)]
    [InlineData("beans(count#int)", 2)]
    [InlineData("beans(count#int)", 3)]
    [InlineData(MapType, 1)]
    [InlineData(MapType, 2)]
    [InlineData(MapType, 3)]
    public void ReadsComplexArraysAtEverySupportedDimension(string baseType, int dimensions)
    {
        var raw = baseType == "localization" ? "\"greeting\"" : baseType == MapType ? MapValue : "{\"count\":10}";
        for (var dimension = 0; dimension < dimensions; dimension++)
            raw = "[" + raw + "]";
        var type = baseType + string.Concat(Enumerable.Repeat("()", dimensions));
        foreach (var row in ReadBothFormats(Document(("value", type, raw))))
        {
            var value = Field(row, "Value");
            for (var dimension = 0; dimension < dimensions; dimension++)
                value = Assert.IsAssignableFrom<Array>(value).GetValue(0)!;
            if (baseType == MapType)
                value = Field(((IDictionary)value)[1]!, "Bean");
            Assert.Equal(baseType == "localization" ? (object)"greeting" : 10,
                Field(value, baseType == "localization" ? "Key" : "Count"));
        }
    }

    [Fact]
    public void ReadsArraysInsideNestedBeans()
    {
        var type = "beans(items#" + MapType + "()|labels#localization()())";
        var raw = "{\"items\":[" + MapValue + "],\"labels\":[[\"greeting\"]]}";
        foreach (var row in ReadBothFormats(Document(("value", type, raw))))
        {
            var value = Field(row, "Value");
            var map = (IDictionary)((Array)Field(value, "Items")).GetValue(0)!;
            Assert.Equal(10, Field(Field(map[1]!, "Bean"), "Count"));
            var labels = (Array)((Array)Field(value, "Labels")).GetValue(0)!;
            Assert.Equal("greeting", Field(labels.GetValue(0)!, "Key"));
        }
    }

    [Fact]
    public void ReportedGeneratedFilesCompileWithMinimalUnityTypeStubs()
    {
        var root = GeneratedCodeCompiler.FindRepositoryRoot();
        GeneratedCodeCompiler.Compile(File.ReadAllText(Path.Combine(root, "Code", "typesData.cs")),
            File.ReadAllText(Path.Combine(root, "Code", "ComplexExamplesData.cs")), UnityTypeStubs.Source);
    }

    private static object Field(object value, string name) => value.GetType().GetField(name)!.GetValue(value)!;

    private static IEnumerable<object> ReadBothFormats(TableDocument document)
    {
        var assembly = GeneratedCodeCompiler.Compile(CSharpExporter.Export(document, includeRuntimeAdapter: true));
        var adapter = assembly.GetType(document.Schema.Name + "TableAdapter")!.GetMethod("FromRow")!;
        var payloads = new[] { Encoding.UTF8.GetBytes(new JsonTableExporter().Export(document)), new BinaryTableExporter().Export(document) };
        foreach (var payload in payloads)
        {
            var manager = new TableManager();
            manager.Load(document.Schema.Name, payload);
            yield return manager.Get(document.Schema.Name, "1", row => adapter.Invoke(null, [row])!);
        }
    }

    private static TableDocument Document(params (string Name, string Type, string Raw)[] values)
    {
        var fields = new[] { new FieldSchema(0, "id", "", TypeDescriptor.Parse("int"), FieldTarget.Both, null) }
            .Concat(values.Select((value, index) => new FieldSchema(index + 1, value.Name, "", TypeDescriptor.Parse(value.Type), FieldTarget.Both, null)))
            .ToArray();
        var row = values.ToDictionary(value => value.Name, value => (string?)value.Raw);
        row["id"] = "1";
        return new TableDocument("generated.xlsx::Generated", new TableSchema("Generated", fields, "id"), [new TableRow(7, false, row)]);
    }
}
