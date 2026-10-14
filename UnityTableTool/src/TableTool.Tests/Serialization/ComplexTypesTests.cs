// 用途：验证 localization、beans、map 的类型解析、结构化 JSON 导出和 Runtime 读取。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System.Text;
using System.Text.Json;
using Company.UnityTableRuntime;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class ComplexTypesTests
{
    [Fact]
    public void ParsesBeansAndMapWithExplicitMemberTypes()
    {
        var beanType = TypeDescriptor.Parse("beans(item_id#int|count#int)");
        var bean = ValueParser.Parse("2001#10", beanType);
        Assert.Equal("RuntimeBeanValue", bean.GetType().Name);
        var beanFields = (System.Collections.Generic.IReadOnlyDictionary<string, object?>)bean.GetType().GetProperty("Fields")!.GetValue(bean)!;
        Assert.Equal(2001, beanFields["item_id"]);
        Assert.Equal(10, beanFields["count"]);

        var mapType = TypeDescriptor.Parse("map(int)(string)(beans(item_id#int|count#int))");
        var map = ValueParser.Parse("1|文本#2001#10;2|说明#2002#20", mapType);
        Assert.Equal("RuntimeMapValue", map.GetType().Name);
        var entries = (System.Collections.IEnumerable)map.GetType().GetProperty("Entries")!.GetValue(map)!;
        Assert.Equal(2, entries.Cast<object>().Count());
    }

    [Fact]
    public void ParsesEscapedMapTextAndNestedBeans()
    {
        var nestedType = TypeDescriptor.Parse("beans(payload#beans(left#int|right#int)|label#string)");
        var nested = ValueParser.Parse("1#2#带\\#分隔符", nestedType);
        var outerFields = (IReadOnlyDictionary<string, object?>)nested.GetType().GetProperty("Fields")!.GetValue(nested)!;
        var payload = outerFields["payload"]!;
        var payloadFields = (IReadOnlyDictionary<string, object?>)payload.GetType().GetProperty("Fields")!.GetValue(payload)!;
        Assert.Equal(1, payloadFields["left"]);
        Assert.Equal(2, payloadFields["right"]);
        Assert.Equal("带#分隔符", outerFields["label"]);

        var mapType = TypeDescriptor.Parse("map(int)(string)(beans(item_id#int|count#int))");
        var map = ValueParser.Parse(
            "1|文本\\#说明#2001#10",
            mapType);
        var entries = (System.Collections.IEnumerable)map.GetType().GetProperty("Entries")!.GetValue(map)!;
        var firstEntry = entries.Cast<object>().Single();
        Assert.Equal("文本#说明", firstEntry.GetType().GetProperty("Text")!.GetValue(firstEntry));
        var bean = firstEntry.GetType().GetProperty("Bean")!.GetValue(firstEntry)!;
        var beanFields = (IReadOnlyDictionary<string, object?>)bean.GetType().GetProperty("Fields")!.GetValue(bean)!;
        Assert.Equal(2001, beanFields["item_id"]);
    }

    [Fact]
    public void RejectsInvalidComplexTypeAndEscapes()
    {
        Assert.Throws<FormatException>(() => TypeDescriptor.Parse("map(int)(int)(beans(item_id#int))"));
        Assert.Throws<FormatException>(() => TypeDescriptor.Parse("beans(item_id#int|item_id#int)"));
        Assert.Throws<FormatException>(() => ValueParser.Parse("a\\q", TypeDescriptor.Parse("string")));
        Assert.Throws<FormatException>(() => ValueParser.Parse(
            "1|文本#2001#10;1|重复#2002#20",
            TypeDescriptor.Parse("map(int)(string)(beans(item_id#int|count#int))")));
    }

    [Fact]
    public void ExportsComplexValuesAsStructuredJsonAndRuntimeReadsThem()
    {
        var localization = CreateLocalizationDocument();
        var document = CreateComplexDocument();
        var catalog = ComplexValueSerializer.BuildLocalizationCatalog([localization, document]);
        var json = new JsonTableExporter().Export(document, null, catalog);
        using var parsed = JsonDocument.Parse(json);
        var row = parsed.RootElement.GetProperty("rows")[0];
        Assert.Equal("greeting", row.GetProperty("label").GetProperty("key").GetString());
        Assert.Equal("你好", row.GetProperty("label").GetProperty("translations").GetProperty("zh-CN").GetString());
        Assert.Equal(10, row.GetProperty("items").GetProperty("1").GetProperty("bean").GetProperty("count").GetInt32());

        var table = JsonTableDataReader.Read(Encoding.UTF8.GetBytes(json));
        Assert.True(table.TryGet("1", out var raw));
        var view = table.View(raw);
        Assert.Contains("greeting", view.GetRaw("label"));
        Assert.Contains("\"1\"", view.GetRaw("items"));
        var runtimeMap = view.Read<object>("items");
        var runtimeEntries = (System.Collections.IEnumerable)runtimeMap.GetType().GetProperty("Entries")!.GetValue(runtimeMap)!;
        var runtimeEntry = runtimeEntries.Cast<object>().First();
        var runtimeBean = runtimeEntry.GetType().GetProperty("Bean")!.GetValue(runtimeEntry)!;
        var runtimeFields = (IReadOnlyDictionary<string, object?>)runtimeBean.GetType().GetProperty("Fields")!.GetValue(runtimeBean)!;
        Assert.Equal(10, runtimeFields["count"]);
    }

    [Fact]
    public void GeneratesStrongTypesForComplexFields()
    {
        var output = CSharpExporter.Export(CreateComplexDocument(), includeRuntimeAdapter: true);
        Assert.Contains("public sealed class LabelValue", output);
        Assert.Contains("public sealed class ItemsEntry", output);
        Assert.Contains("Dictionary<int, ItemsEntry>", output);
        Assert.Contains("RuntimeMapValue", output);
    }

    private static TableDocument CreateLocalizationDocument() => new(
        "localization.xlsx::localization",
        new TableSchema(
            "localization",
            [
                new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null),
                new FieldSchema(2, "zh-CN", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null),
                new FieldSchema(3, "en-US", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null)
            ],
            "id"),
        [new TableRow(7, false, new Dictionary<string, string?>
        {
            ["id"] = "greeting",
            ["zh-CN"] = "你好",
            ["en-US"] = "Hello"
        })]);

    private static TableDocument CreateComplexDocument() => new(
        "complex.xlsx::Items",
        new TableSchema(
            "Items",
            [
                new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
                new FieldSchema(2, "label", string.Empty, TypeDescriptor.Parse("localization"), FieldTarget.Both, null),
                new FieldSchema(3, "items", string.Empty, TypeDescriptor.Parse("map(int)(string)(beans(item_id#int|count#int))"), FieldTarget.Both, null)
            ],
            "id"),
        [new TableRow(7, false, new Dictionary<string, string?>
        {
            ["id"] = "1",
            ["label"] = "greeting",
            ["items"] = "1|文本#2001#10;2|说明#2002#20"
        })]);
}
