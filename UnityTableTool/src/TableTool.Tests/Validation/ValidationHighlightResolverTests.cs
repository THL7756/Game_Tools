using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class ValidationHighlightResolverTests
{
    [Fact]
    public void MapsRowIssuesToCellsAndHeaderIssuesToFields()
    {
        var field = new FieldSchema(
            1,
            "value",
            string.Empty,
            TypeDescriptor.Parse("int"),
            FieldTarget.Both,
            "bad-default");
        var document = new TableDocument(
            "table.xlsx::Sheet",
            new TableSchema("Config", [field], field.Name),
            [new TableRow(9, false, new Dictionary<string, string?> { ["value"] = "1" })]);
        var issues = new[]
        {
            new ValidationIssue(
                "FIELD_TYPE_UNKNOWN",
                ValidationSeverity.Error,
                "Cell is invalid.",
                document.SourceName,
                9,
                2,
                "value"),
            new ValidationIssue(
                "FIELD_DEFAULT_INVALID",
                ValidationSeverity.Warning,
                "Default is invalid.",
                document.SourceName,
                FieldName: "value")
        };

        var result = ValidationHighlightResolver.Resolve(document, issues);

        Assert.Equal(ValidationHighlightLevel.Error, result.HighestLevel);
        Assert.Equal(ValidationHighlightLevel.Error, result.Cells["9:value"].Level);
        Assert.Equal("FIELD_TYPE_UNKNOWN", result.Cells["9:value"].Code);
        Assert.Equal(ValidationHighlightLevel.Warning, result.Fields["value"].Level);
    }

    [Fact]
    public void KeepsTheHighestSeverityForTheSameCell()
    {
        var field = new FieldSchema(0, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null);
        var document = new TableDocument(
            "table.xlsx::Sheet",
            new TableSchema("Config", [field], field.Name),
            [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1" })]);
        var issues = new[]
        {
            new ValidationIssue(
                "WARNING",
                ValidationSeverity.Warning,
                "Warning.",
                document.SourceName,
                7,
                1,
                "id"),
            new ValidationIssue(
                "FATAL",
                ValidationSeverity.Fatal,
                "Fatal.",
                document.SourceName,
                7,
                1,
                "id")
        };

        var result = ValidationHighlightResolver.Resolve(document, issues);

        Assert.Equal("FATAL", result.Cells["7:id"].Code);
        Assert.Equal(ValidationHighlightLevel.Error, result.Cells["7:id"].Level);
    }
}
