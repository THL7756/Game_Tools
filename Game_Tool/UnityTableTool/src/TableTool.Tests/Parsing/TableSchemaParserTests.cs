using TableTool.Core.Parsing;
using Xunit;

namespace TableTool.Tests.Parsing;

public sealed class TableSchemaParserTests
{
    [Fact]
    public void ParsesSixHeaderRowsAndSkipsBlankFieldNames()
    {
        var grid = Grid(
            ["table: Skill", "", "", "", ""],
            ["字段说明", "技能 ID", "技能名称", "忽略"],
            ["字段类型", "int", "string", "string"],
            ["字段名", "id", "name", ""],
            ["客户端服务器", "", "c", ""],
            ["默认值", "0", "", ""],
            ["1001", "火球", "备注"]);

        var document = new TableSchemaParser().Parse(grid);

        Assert.Equal("Skill", document.Schema.Name);
        Assert.Equal(new[] { "id", "name" }, document.Schema.Fields.Select(field => field.Name));
        Assert.Single(document.Rows);
        Assert.Equal("1001", document.Rows[0].RawValues["id"]);
        Assert.Equal("火球", document.Rows[0].RawValues["name"]);
    }

    [Fact]
    public void ClassifiesCommentAndTestColumns()
    {
        var grid = Grid(
            ["table: Skill", "", "", "", ""],
            ["字段说明", "ID", "测试", "注释"],
            ["字段类型", "int", "int", "string"],
            ["字段名", "id", "#test", "##备注"],
            ["客户端服务器", "cs", "", ""],
            ["默认值", "0", "0", ""],
            ["1001", "2", "ignored"]);

        var document = new TableSchemaParser().Parse(grid);

        Assert.Single(document.Schema.Fields);
        Assert.Equal("id", document.Schema.Fields[0].Name);
        Assert.Single(document.Rows);
        Assert.Equal("1001", document.Rows[0].RawValues["id"]);
    }

    private static RawTableGrid Grid(params string[][] rows) =>
        new("memory", rows.Select(row => (IReadOnlyList<string?>)row).ToArray());
}
