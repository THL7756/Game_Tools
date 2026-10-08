using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class TableBatchValidatorTests
{
    [Fact]
    public void ValidatesAllSelectedSheetsAndReportsMissingReferenceSource()
    {
        var item = CreateDocument(
            "items.xlsx::Item",
            "Item",
            new FieldSchema(1, "pet_id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
            "not-an-int");
        var skill = CreateDocument(
            "skills.xlsx::Skill",
            "Skill",
            new FieldSchema(0, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
            "also-invalid");

        var result = TableBatchValidator.Validate(
            [item, skill],
            [item.SourceName, skill.SourceName]);

        Assert.Contains(result.Issues, issue =>
            issue.SourceName == item.SourceName
            && issue.FieldName == "pet_id"
            && issue.Code == ErrorCodes.TableReferenceMissing);
        Assert.Contains(result.Issues, issue =>
            issue.SourceName == skill.SourceName
            && issue.SourceRow == 7
            && issue.FieldName == "id");
    }

    [Fact]
    public void ExcludesIssuesFromUncheckedDocuments()
    {
        var selected = CreateDocument(
            "selected.xlsx::Selected",
            "Selected",
            new FieldSchema(0, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
            "10");
        var uncheckedDocument = CreateDocument(
            "unchecked.xlsx::Unchecked",
            "Unchecked",
            new FieldSchema(0, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
            "not-an-int");

        var result = TableBatchValidator.Validate(
            [selected, uncheckedDocument],
            [selected.SourceName]);

        Assert.DoesNotContain(result.Issues, issue => issue.SourceName == uncheckedDocument.SourceName);
    }

    private static TableDocument CreateDocument(
        string sourceName,
        string tableName,
        FieldSchema field,
        string value) =>
        new(
            sourceName,
            new TableSchema(tableName, [field], field.Name),
            [new TableRow(7, false, new Dictionary<string, string?> { [field.Name] = value })]);
}
