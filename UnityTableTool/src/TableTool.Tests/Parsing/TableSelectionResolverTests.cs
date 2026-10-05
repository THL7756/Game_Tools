using TableTool.Core.Models;
using TableTool.Core.Parsing;
using Xunit;

namespace TableTool.Tests.Parsing;

public sealed class TableSelectionResolverTests
{
    [Fact]
    public void SelectingOneSplitIncludesAllFilesWithSameLogicalTable()
    {
        var skill = Create("道具表.xlsx", "Item", "id");
        var split = Create("道具表_1.xlsx", "Item", "id");
        var other = Create("技能表.xlsx", "Skill", "id");

        var result = TableSelectionResolver.Expand([skill, split, other], [skill.SourceName]);

        Assert.Equal(new[] { "道具表.xlsx", "道具表_1.xlsx" },
            result.Select(document => TableSelectionResolver.GetSourceFileName(document.SourceName)));
    }

    [Fact]
    public void SelectingTableIncludesReferencedTableByIdField()
    {
        var item = Create("道具表.xlsx", "Item", "id");
        var skill = Create("技能表.xlsx", "Skill", "item_id");

        var result = TableSelectionResolver.Expand([skill, item], [skill.SourceName]);

        Assert.Equal(new[] { "Skill", "Item" }, result.Select(document => document.Schema.Name));
    }

    private static TableDocument Create(string source, string tableName, string fieldName)
    {
        var schema = new TableSchema(
            tableName,
            [new FieldSchema(0, fieldName, string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null)],
            fieldName);
        return new TableDocument(source, schema, [new TableRow(7, false, new Dictionary<string, string?> { [fieldName] = "1" })]);
    }
}
