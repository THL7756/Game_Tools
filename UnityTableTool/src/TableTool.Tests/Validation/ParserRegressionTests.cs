// 用途：验证重复字段、整列注释和同名表 schema 冲突的诊断行为。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class ParserRegressionTests
{
    [Fact]
    public void DuplicateFieldsRemainVisibleWithIndependentSourceValues()
    {
        var result = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "duplicate.xlsx::Sheet1",
            [
                ["table: Duplicate"],
                ["desc", "desc"],
                ["string", "string"],
                ["name", "name"],
                ["cs", "cs"],
                [null, null],
                ["first", "second"]
            ]));

        Assert.NotNull(result.Document);
        Assert.Equal(2, result.Document!.Schema.Fields.Count);
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == ErrorCodes.FieldNameDuplicate));
        var row = result.Document.Rows.Single();
        Assert.Equal("first", row.GetValue(result.Document.Schema.Fields[0]));
        Assert.Equal("second", row.GetValue(result.Document.Schema.Fields[1]));
    }

    [Fact]
    public void FirstRowCommentMarkerSkipsTheWholeColumn()
    {
        var result = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "comment.xlsx::Sheet1",
            [
                ["table: Comment", "##ignored"],
                ["desc", "desc"],
                ["int", "string"],
                ["id", "ignored"],
                ["cs", "cs"],
                ["0", null],
                ["1", "value"]
            ]));

        Assert.NotNull(result.Document);
        Assert.Single(result.Document!.Schema.Fields);
        Assert.Equal("id", result.Document.Schema.Fields[0].Name);
    }

    [Fact]
    public void SameTableRequiresFieldOrderAndDefaultsToMatch()
    {
        var first = CreateShard("first.xlsx::Sheet1", [
            new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, "0"),
            new FieldSchema(2, "name", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null)
        ]);
        var second = CreateShard("second.xlsx::Sheet1", [
            new FieldSchema(1, "name", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null),
            new FieldSchema(2, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, "1")
        ]);

        var issues = TableDocumentMerger.FindSchemaConflicts([first, second]);

        Assert.Equal(4, issues.Count(issue => issue.Code == ErrorCodes.TableMergeInvalid));
        Assert.Contains(issues, issue => issue.SourceName == first.SourceName && issue.SourceColumn == 2);
        Assert.Contains(issues, issue => issue.SourceName == second.SourceName && issue.SourceColumn == 2);
    }

    private static TableDocument CreateShard(string sourceName, IReadOnlyList<FieldSchema> fields) =>
        new(
            sourceName,
            new TableSchema("Item", fields, "id"),
            [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1", ["name"] = "item" })]);
}
