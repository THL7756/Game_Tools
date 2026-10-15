// 用途：验证多语言只导出键、独立语言表、嵌套引用和 JSON/UTB1 读取。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System.Text;
using System.Text.Json;
using Company.UnityTableRuntime;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class LocalizationExportTests
{
    [Theory]
    [InlineData("localization", "greeting", "\"greeting\"")]
    [InlineData("localization()", "greeting#item_name", "[\"greeting\",\"item_name\"]")]
    [InlineData("localization()()", "greeting#item_name|greeting", "[[\"greeting\",\"item_name\"],[\"greeting\"]]")]
    [InlineData("localization()()()", "greeting|item_name;greeting#item_name", "[[[\"greeting\"],[\"item_name\"]],[[\"greeting\",\"item_name\"]]]")]
    [InlineData("beans(label#localization|count#int)", "greeting#2", "{\"label\":\"greeting\",\"count\":2}")]
    [InlineData("map(int)(string)(beans(label#localization|count#int))", "1|text#greeting#2", "{\"1\":{\"text\":\"text\",\"bean\":{\"label\":\"greeting\",\"count\":2}}}")]
    public void ExportsOnlyKeysAtEverySupportedNestingLevel(string type, string raw, string expected)
    {
        var document = CreateReferenceDocument(type, raw);
        using var json = JsonDocument.Parse(new JsonTableExporter().Export(document));
        var actual = json.RootElement.GetProperty("rows")[0].GetProperty("label");
        Assert.Equal(expected, JsonSerializer.Serialize(actual));

        var table = BinaryTableDataReader.Read(new BinaryTableExporter().Export(document));
        Assert.True(table.TryGet("1", out var row));
        Assert.NotNull(table.View(row).Read<object>("label"));
    }

    [Fact]
    public void ExportsLocalizationOncePerTargetAndPreservesTargetFiltering()
    {
        var root = Path.Combine(Path.GetTempPath(), "UnityTableLocalization-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reference = CreateReferenceDocument("localization", "greeting");
            var localization = LocalizationRuntimeTests.CreateLocalizationDocument();
            var documents = TableSelectionResolver.Expand([reference, localization], [reference.SourceName]);
            var result = new ExportService().ExportAll(documents, new ExportOptions(
                Path.Combine(root, "client"), Path.Combine(root, "server"),
                Path.Combine(root, "code-client"), Path.Combine(root, "code-server"),
                generateCode: false, generateBytes: false, targets: ExportTarget.Both));

            Assert.Equal(4, result.Files.Count);
            foreach (var target in new[] { "client", "server" })
            {
                var referenceJson = File.ReadAllText(Path.Combine(root, target, "Items.json"));
                using var referencePayload = JsonDocument.Parse(referenceJson);
                Assert.Equal("greeting", referencePayload.RootElement.GetProperty("rows")[0].GetProperty("label").GetString());
                Assert.DoesNotContain("translations", referenceJson);
                Assert.DoesNotContain("Hello", referenceJson);

                using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, target, "localization.json")));
                var row = catalog.RootElement.GetProperty("rows")[0];
                Assert.Equal("Hello", row.GetProperty("en-US").GetString());
                Assert.Equal(target == "client", row.TryGetProperty("zh-CN", out _));
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RuntimeConvertsKeyStringsAndNestedKeysToStrongTypes()
    {
        var scalar = ReadReference("localization", "greeting").Read<KeyReference>("label");
        Assert.Equal("greeting", scalar.Key);

        var array = ReadReference("localization()()", "greeting#item_name|greeting").Read<KeyReference[][]>("label");
        Assert.Equal("item_name", array[0][1].Key);
        var bean = ReadReference("beans(label#localization|count#int)", "greeting#2").Read<LabelBean>("label");
        Assert.Equal("greeting", bean.Label.Key);
        var map = ReadReference("map(int)(string)(beans(label#localization|count#int))", "1|text#greeting#2")
            .Read<Dictionary<int, LabelEntry>>("label");
        Assert.Equal("greeting", map[1].Bean.Label.Key);
        Assert.Equal(2, map[1].Bean.Count);
    }

    [Theory]
    [InlineData("localization", "{\"key\":\"greeting\",\"translations\":{\"en-US\":\"Hello\"}}")]
    [InlineData("beans(label#localization)", "{\"label\":{\"key\":\"greeting\"}}")]
    [InlineData("localization()", "[{\"key\":\"greeting\"}]")]
    public void RejectsOldLocalizationObjects(string type, string raw)
    {
        using var value = JsonDocument.Parse(raw);
        var payload = new
        {
            tableName = "Items",
            primaryKey = "id",
            fields = new[] { new { name = "label", type } },
            rows = new[] { new { id = "1", label = value.RootElement } }
        };
        var document = JsonTableDataReader.Read(JsonSerializer.SerializeToUtf8Bytes(payload));
        Assert.True(document.TryGet("1", out var row));
        var view = document.View(row);
        Assert.Throws<FormatException>(() => view.Read<object>("label"));
    }

    private static TableRowView ReadReference(string type, string raw)
    {
        var table = JsonTableDataReader.Read(Encoding.UTF8.GetBytes(new JsonTableExporter().Export(CreateReferenceDocument(type, raw))));
        Assert.True(table.TryGet("1", out var row));
        return table.View(row);
    }

    private static TableDocument CreateReferenceDocument(string type, string raw) => new(
        "items.xlsx::Items",
        new TableSchema("Items",
        [
            new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
            new FieldSchema(2, "label", string.Empty, TypeDescriptor.Parse(type), FieldTarget.Both, null)
        ], "id"),
        [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1", ["label"] = raw })]);

    public sealed class KeyReference
    {
        public string Key { get; set; } = string.Empty;
    }

    public sealed class LabelBean
    {
        public KeyReference Label { get; set; } = null!;
        public int Count { get; set; }
    }

    public sealed class LabelEntry
    {
        public string Text { get; set; } = string.Empty;
        public LabelBean Bean { get; set; } = null!;
    }
}
