// 用途：验证更新后的表格规范工作簿与解析规则保持一致。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class SampleWorkbookRegressionTests
{
    [Fact]
    public void UpdatedWorkbookKeepsCommentColumnsAndRowsOutOfFormalData()
    {
        var root = FindRepositoryRoot();
        var grid = TableFileReader.Read(Path.Combine(root, "Data", "表格规范.xlsx"))
            .Single(item => item.SourceName.EndsWith("基础表 所有字段类型", StringComparison.Ordinal));
        var result = new TableSchemaParser().ParseWithDiagnostics(grid);

        Assert.NotNull(result.Document);
        Assert.Empty(result.Issues);
        Assert.DoesNotContain(result.Document!.Schema.Fields, field => field.Name is "note" or "cube_special");
        Assert.DoesNotContain(result.Document.Schema.Fields, field => field.Name is "localization" or "item_num");
        Assert.Contains(result.Document.Schema.ValidationFields!, field => field.Name == "#test_note");
        Assert.Single(result.Document.Rows);
    }

    [Fact]
    public void UpdatedWorkbookParsesSingletonValuesFromShiftedRows()
    {
        var root = FindRepositoryRoot();
        var grid = TableFileReader.Read(Path.Combine(root, "Data", "表格规范.xlsx"))
            .Single(item => item.SourceName.EndsWith("单例表", StringComparison.Ordinal));
        var result = new TableSchemaParser().ParseWithDiagnostics(grid);

        Assert.NotNull(result.Document);
        Assert.Empty(result.Issues);
        var document = result.Document!;
        Assert.True(document.Schema.IsSingleton);
        Assert.Contains(document.Schema.Fields, field => field.Name == "values");
        Assert.Equal("1#2#3", document.Rows.Single().GetValue(document.Schema.Fields.Single(field => field.Name == "values")));
    }

    [Fact]
    public void UpdatedWorkbookContainsLocalizationBeansAndMapExamples()
    {
        var root = FindRepositoryRoot();
        var grids = TableFileReader.Read(Path.Combine(root, "Data", "表格规范.xlsx"));
        var localization = new TableSchemaParser().ParseWithDiagnostics(
            grids.Single(item => item.SourceName.EndsWith("localization", StringComparison.Ordinal))).Document;
        var complex = new TableSchemaParser().ParseWithDiagnostics(
            grids.Single(item => item.SourceName.EndsWith("复杂类型示例", StringComparison.Ordinal))).Document;

        Assert.NotNull(localization);
        Assert.Equal("id", localization!.Schema.PrimaryKey);
        Assert.NotNull(complex);
        Assert.Equal("localization", complex!.Schema.Fields.Single(field => field.Name == "label").Type.BaseType);
        Assert.Equal("map", complex.Schema.Fields.Single(field => field.Name == "item_map").Type.BaseType);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UnityTableTool.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate UnityTableTool repository root.");
    }
}
