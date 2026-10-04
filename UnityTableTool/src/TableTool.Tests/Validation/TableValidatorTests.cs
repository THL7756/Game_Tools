using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class TableValidatorTests
{
    [Fact]
    public void SingletonDoesNotRequireAPrimaryKey()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: GlobalConfig\ntype:single"],
            (IReadOnlyList<string?>)["常量ID", "数值类型", "具体数值"],
            (IReadOnlyList<string?>)["str", "str", "str"],
            (IReadOnlyList<string?>)["id", "type", "data"],
            (IReadOnlyList<string?>)["team_num", "int", "5"],
            (IReadOnlyList<string?>)["slot_num", "int", "1"]
        });
        var document = new TableSchemaParser().Parse(grid);

        var issues = new TableValidator().Validate(document);

        Assert.DoesNotContain(issues, issue => issue.Code == ErrorCodes.PrimaryKeyMissing);
    }

    [Fact]
    public void ReportsDuplicatePrimaryKeysAndInvalidArrayValues()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", "效果"],
            (IReadOnlyList<string?>)["字段类型", "int", "int()"],
            (IReadOnlyList<string?>)["字段名", "id", "effects"],
            (IReadOnlyList<string?>)["客户端服务器", "", ""],
            (IReadOnlyList<string?>)["默认值", "0", ""],
            (IReadOnlyList<string?>)["1", "1|2"],
            (IReadOnlyList<string?>)["1", "3#4"]
        });

        var document = new TableSchemaParser().Parse(grid);
        var issues = new TableValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ErrorCodes.PrimaryKeyDuplicate);
        Assert.Contains(issues, issue => issue.Code == ErrorCodes.FieldArraySeparatorInvalid);
    }

    [Fact]
    public void ValidatesTestColumnsWithoutExportingThem()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", "调试值"],
            (IReadOnlyList<string?>)["字段类型", "int", "int"],
            (IReadOnlyList<string?>)["字段名", "id", "#test"],
            (IReadOnlyList<string?>)["客户端服务器", "cs", "cs"],
            (IReadOnlyList<string?>)["默认值", "0", ""],
            (IReadOnlyList<string?>)["1", "not-an-int"]
        });

        var document = new TableSchemaParser().Parse(grid);
        var issues = new TableValidator().Validate(document);

        Assert.Single(document.Schema.Fields);
        Assert.Contains(issues, issue => issue.FieldName == "#test");
        Assert.Contains(issues, issue => issue.Code == ErrorCodes.FieldTypeUnknown);
    }

    [Fact]
    public void ReportsInvalidSingletonRowCount()
    {
        var document = new TableDocument(
            "memory",
            new TableSchema("GlobalConfig", [new FieldSchema(2, "value", "", TypeDescriptor.Parse("string"), FieldTarget.Both, null)], "value", true),
            [
                new TableRow(7, false, new Dictionary<string, string?> { ["value"] = "A" }),
                new TableRow(8, false, new Dictionary<string, string?> { ["value"] = "B" })
            ]);
        var issues = new TableValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ErrorCodes.SingletonRowCountInvalid);
    }
}
