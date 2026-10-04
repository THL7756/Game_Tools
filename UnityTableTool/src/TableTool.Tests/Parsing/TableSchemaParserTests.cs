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

    [Fact]
    public void ParsesSingletonMetadataAndRequiresOneDataRow()
    {
        var grid = Grid(
            ["table: GlobalConfig", "type:single"],
            ["常量ID", "数值类型", "具体数值"],
            ["str", "str", "str"],
            ["id", "type", "data"],
            ["name", "string", "正式配置"],
            ["", "", ""]);

        var document = new TableSchemaParser().Parse(grid);

        Assert.True(document.Schema.IsSingleton);
        Assert.Single(document.Rows);
        Assert.Equal("正式配置", document.Rows[0].RawValues["name"]);
    }

    [Fact]
    public void ParsesSingletonRowsWithOptionalDescriptionAndFlexibleColumns()
    {
        var grid = Grid(
            ["table: GlobalConfig\ntype:single"],
            ["常量ID", "常量描述（不导表）", "数值类型", "具体数值", "前后端"],
            ["str", "", "str", "str", "cs"],
            ["id", "desc", "type", "data", ""],
            ["", "", "", "", "cs"],
            ["team_num", "预设阵容数量", "int", "5", "c"],
            ["slot_num", "化形槽位数", "int", "1", "s"]);

        var document = new TableSchemaParser().Parse(grid);

        Assert.True(document.Schema.IsSingleton);
        Assert.Equal(new[] { "team_num", "slot_num" }, document.Schema.Fields.Select(field => field.Name));
        Assert.Equal(new[] { "c", "s" }, document.Schema.Fields.Select(field => field.Target.ToString()[0].ToString().ToLowerInvariant()));
        Assert.Single(document.Rows);
        Assert.Equal("5", document.Rows[0].RawValues["team_num"]);
        Assert.Equal("1", document.Rows[0].RawValues["slot_num"]);
    }

    [Fact]
    public void ParsesSingletonWithoutDescriptionColumn()
    {
        var grid = Grid(
            ["table: GlobalConfig", "type:single"],
            ["前后端", "具体数值", "常量ID", "数值类型"],
            ["cs", "str", "str", "str"],
            ["", "data", "id", "type"],
            ["cs", "5", "team_num", "int"]);

        var document = new TableSchemaParser().Parse(grid);

        Assert.Single(document.Schema.Fields);
        Assert.Equal("team_num", document.Schema.Fields[0].Name);
        Assert.Equal("5", document.Rows[0].RawValues["team_num"]);
    }

    private static RawTableGrid Grid(params string[][] rows) =>
        new("memory", rows.Select(row => (IReadOnlyList<string?>)row).ToArray());
}
