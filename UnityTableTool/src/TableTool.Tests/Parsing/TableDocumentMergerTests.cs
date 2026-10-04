using TableTool.Core.Parsing;
using Xunit;

namespace TableTool.Tests.Parsing;

public sealed class TableDocumentMergerTests
{
    [Fact]
    public void MergesSameNamedShardsIntoOneLogicalTable()
    {
        var first = Parse("first", "1001");
        var second = Parse("second", "1002");

        var merged = TableDocumentMerger.Merge([first, second]);

        var document = Assert.Single(merged);
        Assert.Equal("Skill", document.Schema.Name);
        Assert.Equal(2, document.Rows.Count);
        Assert.Contains(document.Rows, row => row.RawValues["id"] == "1001");
        Assert.Contains(document.Rows, row => row.RawValues["id"] == "1002");
    }

    [Fact]
    public void RejectsSameTableNameWithDifferentSchema()
    {
        var first = Parse("first", "1001");
        var second = Parse("second", "1001", "name");

        Assert.Throws<FormatException>(() => TableDocumentMerger.Merge([first, second]));
    }

    private static TableTool.Core.Models.TableDocument Parse(string source, string id, string? extra = null)
    {
        var rows = new List<IReadOnlyList<string?>>
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", extra is null ? "" : "名称"],
            (IReadOnlyList<string?>)["字段类型", "int", extra is null ? "" : "string"],
            (IReadOnlyList<string?>)["字段名", "id", extra],
            (IReadOnlyList<string?>)["客户端服务器", "cs", "cs"],
            (IReadOnlyList<string?>)["默认值", "0", ""],
            (IReadOnlyList<string?>)[id, extra is null ? "" : "value"]
        };
        return new TableSchemaParser().Parse(new RawTableGrid(source, rows));
    }
}
