// 用途：验证未勾选、未激活 Sheet 的诊断，以及标签、预览高亮的结果一致性。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using TableTool.Gui.Services;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class PreviewValidationTests
{
    private readonly PreviewService service = new();

    [Fact]
    public void UncheckedFileReportsNestedReferenceErrorsBeforeOpeningTheSheet()
    {
        var basic = Document("tables.xlsx::Basic", "Basic", "1");
        var complex = ComplexDocument("999#2");
        var file = FileModel("tables.xlsx", basic, complex);
        var item = FileModel("item.xlsx", Document("item.xlsx::Item", "item", "1001"));

        var result = service.Validate([file, item], file);

        Assert.False(file.IsSelected);
        Assert.False(item.IsSelected);
        Assert.Same(file.Sheets[0], file.CurrentSheet);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ErrorCodes.TableReferenceKeyMissing, issue.Code);
        Assert.Equal(complex.SourceName, issue.SourceName);
        Assert.Equal("None", file.Sheets[0].ValidationLevel);
        Assert.Equal("错", file.Sheets[1].ValidationMarker);
        Assert.Contains("999", file.Sheets[1].ValidationTooltip);
        Assert.Contains("C7", file.Sheets[1].ValidationTooltip);

        file.SelectSheet(file.Sheets[1]);
        var preview = service.Create(file, result.Issues);
        var bundle = preview.Fields.Single(field => field.Name == "bundle");
        Assert.Equal(result.Issues, preview.Issues);
        Assert.Equal("Error", preview.Rows.Single().CellLevels[bundle.Key]);
        Assert.Equal("1 错误 / 0 警告", preview.IssueSummary);
    }

    [Fact]
    public void SelectionDoesNotDuplicateIssuesAndUnrelatedUncheckedFilesStayOutOfScope()
    {
        var file = FileModel("tables.xlsx", ComplexDocument("999#2"));
        file.IsSelected = true;
        var item = FileModel("item.xlsx", Document("item.xlsx::Item", "item", "1001"));
        var unrelated = FileModel("other.xlsx", Document("other.xlsx::Other", "Other", "invalid"));

        var result = service.Validate([file, item, unrelated], file);
        Assert.Single(result.Issues);
        Assert.DoesNotContain(result.Documents, document => document.Schema.Name == "Other");

        unrelated.IsSelected = true;
        result = service.Validate([file, item, unrelated], file);
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, issue => issue.SourceName == unrelated.Document.SourceName);
    }

    [Theory]
    [InlineData("beans(item_id#int|count#int)", "1001#invalid")]
    [InlineData("map(int)(string)(beans(item_id#int|count#int))", "1|text#1001#2;1|duplicate#1001#3")]
    [InlineData("map(int)(string)(beans(item_id#int|count#int))", "1|text#999#2")]
    [InlineData("map(int)(string)(beans(item_id#int|count#int))", "1|bad\\q#1001#2")]
    public void UnopenedSheetsMarkInvalidComplexValues(string type, string value)
    {
        var file = FileModel("tables.xlsx", Document("tables.xlsx::Basic", "Basic", "1"),
            ComplexDocument(value, type));
        var item = FileModel("item.xlsx", Document("item.xlsx::Item", "item", "1001"));

        var result = service.Validate([file, item], file);

        Assert.Contains(result.Issues, issue => issue.SourceName == file.Sheets[1].Document.SourceName
            && issue.FieldName == "bundle" && issue.Severity == ValidationSeverity.Error);
        Assert.Equal("Error", file.Sheets[1].ValidationLevel);
        Assert.False(file.IsSelected);
    }

    [Fact]
    public void SheetMarkersPrioritizeErrorsAndClearWhenIssuesAreRemoved()
    {
        var document = Document("tables.xlsx::Basic", "Basic", "1");
        var file = FileModel("tables.xlsx", document);
        var warning = new ValidationIssue("WARNING", ValidationSeverity.Warning, "warning", document.SourceName);
        var error = new ValidationIssue("ERROR", ValidationSeverity.Fatal, "error", document.SourceName);

        service.UpdateSheetStates(file.Sheets, [warning]);
        Assert.Equal("警", file.CurrentSheet.ValidationMarker);
        service.UpdateSheetStates(file.Sheets, [warning, error, error]);
        Assert.Equal("错", file.CurrentSheet.ValidationMarker);
        Assert.Equal(2, file.CurrentSheet.ValidationTooltip.Split(Environment.NewLine).Length);
        service.UpdateSheetStates(file.Sheets, []);
        Assert.Equal("None", file.CurrentSheet.ValidationLevel);
        Assert.Empty(file.CurrentSheet.ValidationMarker);
        Assert.Empty(file.CurrentSheet.ValidationTooltip);
    }

    [Fact]
    public void DuplicateFieldNamesDoNotHighlightTheWrongDataCell()
    {
        var parsed = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "duplicate.xlsx::Data",
            [
                ["table: Duplicate"],
                ["desc", "desc", "desc"],
                ["int", "int", "int"],
                ["id", "count", "count"],
                ["cs", "cs", "cs"],
                [null, null, null],
                ["1", "2", "invalid"]
            ]));
        var file = FileModel("duplicate.xlsx", parsed.Document!);

        var result = service.Validate([file], file);
        var preview = service.Create(file, result.Issues);
        var fields = preview.Fields.Where(field => field.Name == "count").ToArray();

        Assert.False(preview.Rows.Single().CellLevels.ContainsKey(fields[0].Key));
        Assert.Equal("Error", preview.Rows.Single().CellLevels[fields[1].Key]);
        Assert.All(fields, field => Assert.Equal("Error", field.HighlightLevel));
    }

    [Fact]
    public void SingletonErrorOnlyHighlightsTheAffectedDefinition()
    {
        var first = Field(2, "bad", "int") with { NameRow = 7 };
        var second = Field(2, "good", "int") with { NameRow = 8 };
        var document = new TableDocument("single.xlsx::Values",
            new TableSchema("Values", [first, second], "bad", true),
            [new TableRow(7, false, new Dictionary<string, string?> { ["bad"] = "invalid", ["good"] = "1" })]);
        var file = FileModel("single.xlsx", document);

        var result = service.Validate([file], file);
        var preview = service.Create(file, result.Issues);

        Assert.Equal("Error", preview.Rows[0].CellLevels["data"]);
        Assert.Empty(preview.Rows[1].CellLevels);
    }

    [Theory]
    [InlineData("MissingType", "cs")]
    [InlineData("int", "invalid")]
    public void SingletonDefinitionErrorsRemainVisible(string type, string target)
    {
        var parsed = new TableSchemaParser().ParseWithDiagnostics(new RawTableGrid(
            "single.xlsx::Values",
            [
                ["table: Values|type:single"],
                [null, "id", "type", "data", "cs"],
                ["默认值", null, null, null, "cs"],
                [null, "bad", type, "1", target],
                [null, "good", "int", "1", "cs"]
            ]));
        Assert.NotNull(parsed.Document);
        var file = FileModel("single.xlsx", parsed.Document!);

        var result = service.Validate([file], file);
        var preview = service.Create(file, result.Issues);

        Assert.Equal("错", file.CurrentSheet.ValidationMarker);
        Assert.Equal("Error", preview.Rows[0].CellLevels["data"]);
        Assert.Empty(preview.Rows[1].CellLevels);
    }

    private static TableDocument ComplexDocument(string value, string type = "beans(item_id#int|count#int)") => new(
        "tables.xlsx::Complex", new TableSchema("Complex",
            [Field(1, "id", "int"), Field(2, "bundle", type)], "id"),
        [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1", ["bundle"] = value })]);

    private static TableDocument Document(string source, string name, string id) => new(
        source, new TableSchema(name, [Field(1, "id", "int")], "id"),
        [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = id })]);

    private static FieldSchema Field(int column, string name, string type) =>
        new(column, name, string.Empty, TypeDescriptor.Parse(type), FieldTarget.Both, null);

    private static TableModel FileModel(string path, params TableDocument[] documents) => new(
        path, path, path, documents.Select(document => new TableSheetModel(
            document, document.SourceName.Split("::")[1])).ToArray(), false, false);
}
