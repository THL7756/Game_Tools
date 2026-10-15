// 用途：验证连字符字段被阻断，并准确定位普通表、单例和嵌套类型定义。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class FieldNameValidationTests
{
    [Fact]
    public void ReportsLanguageFieldAtItsNameCellAndPreservesItForPreview()
    {
        var result = Parse("zh-CN", "string");
        var issue = Assert.Single(result.Issues);

        Assert.Equal(ErrorCodes.FieldNameInvalid, issue.Code);
        Assert.Equal(4, issue.SourceRow);
        Assert.Equal(3, issue.SourceColumn);
        Assert.Equal("zh-CN", issue.FieldName);
        Assert.Contains("_", issue.Suggestion);
        Assert.Contains(result.Document!.Schema.Fields, field => field.Name == "zh-CN");

        var batch = TableBatchValidator.Validate([result.Document], [result.Document.SourceName]);
        Assert.Contains(batch.Issues, item => item == issue);
        var highlights = ValidationHighlightResolver.Resolve(result.Document, batch.Issues);
        Assert.Equal(ValidationHighlightLevel.Error, highlights.Columns![2].Level);
    }

    [Theory]
    [InlineData("beans(text-name#string)", "label.text-name")]
    [InlineData("beans(text-name#string)()()", "label.text-name")]
    [InlineData("beans(inner#beans(text-name#string))", "label.inner.text-name")]
    [InlineData("map(int)(string)(beans(text-name#string))", "label.bean.text-name")]
    [InlineData("beans(items#map(int)(string)(beans(text-name#string)))", "label.items.bean.text-name")]
    [InlineData("beans(inner-name#beans(text#string))", "label.inner-name")]
    public void ReportsEachInvalidNestedNameOnceAtItsTypeCell(string type, string path)
    {
        var result = Parse("label", type);
        var issue = Assert.Single(result.Issues);

        Assert.Equal(ErrorCodes.FieldNameInvalid, issue.Code);
        Assert.Equal(3, issue.SourceRow);
        Assert.Equal(3, issue.SourceColumn);
        Assert.Equal("label", issue.FieldName);
        Assert.Contains(path, issue.Message);
    }

    [Theory]
    [InlineData(false, "zh-CN", "string", 2)]
    [InlineData(true, "zh-CN", "string", 3)]
    [InlineData(false, "label", "beans(text-name#string)", 3)]
    [InlineData(true, "label", "beans(text-name#string)", 4)]
    public void ReportsActualSingletonNameOrTypeColumn(bool shifted, string name, string type, int column)
    {
        string?[] definition = shifted
            ? [null, "说明", name, type, "text", "cs"]
            : [null, name, type, "text", "cs"];
        var result = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "single.xlsx::Values",
            [
                ["table: Values|type:single"],
                [null, "id", "type", "data", "cs"],
                ["默认值", null, null, null, "cs"],
                definition,
                [null, "good", "int", "1", "cs"]
            ]));
        var issue = Assert.Single(result.Issues);

        Assert.True(result.Document!.Schema.IsSingleton);
        Assert.Equal(ErrorCodes.FieldNameInvalid, issue.Code);
        Assert.Equal(4, issue.SourceRow);
        Assert.Equal(column, issue.SourceColumn);
        Assert.Equal(name, issue.FieldName);
    }

    [Theory]
    [InlineData("zh_CN", "string")]
    [InlineData("label", "beans(text_name#string)")]
    [InlineData("label", "map(int)(string)(beans(text_name#string))")]
    public void AcceptsUnderscores(string name, string type) => Assert.Empty(Parse(name, type).Issues);

    [Fact]
    public void IgnoresCommentedFieldsAndHyphensInCellValues()
    {
        var result = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "comments.xlsx::Data",
            [
                ["table: Data", null, null, "##ignored"],
                ["desc", "desc", "desc", "desc"],
                ["int", "string", "string", "string"],
                ["id", "text", "##old-name", "old-name"],
                ["cs", "cs", "cs", "cs"],
                [null, null, null, null],
                ["1", "text-with-hyphens", "ignored", "ignored"]
            ]));

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Document!.Schema.Fields.Count);
        Assert.Equal("text-with-hyphens", result.Document.Rows.Single().GetValue(result.Document.Schema.Fields[1]));
    }

    private static TableParseResult Parse(string name, string type) =>
        new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "fields.xlsx::localization",
            [
                ["table: localization"],
                ["字段说明", "key", "text"],
                ["字段类型", "string", type],
                ["字段名", "id", name],
                ["客户端服务器", "cs", "cs"],
                ["默认值", null, null]
            ]));
}
