using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class SampleWorkbookRegressionTests
{
    [Fact]
    public void RealSkillWorkbooksReportTheObservedDuplicateLocations()
    {
        var root = FindRepositoryRoot();
        var paths = new[]
        {
            Path.Combine(root, "Data", "技能表.xlsx"),
            Path.Combine(root, "Data", "究极无敌雷霆爆炸长的表格名称.xlsx")
        };
        var documents = paths
            .SelectMany(TableFileReader.Read)
            .Select(grid => new TableSchemaParser().ParseWithDiagnostics(grid))
            .Select(result => result.Document)
            .Where(document => document is not null)
            .Select(document => document!)
            .Where(document => document.Schema.Name == "Skill")
            .ToArray();

        var duplicates = new TableValidator().Validate(documents)
            .Where(issue => issue.Code == ErrorCodes.PrimaryKeyDuplicate && issue.Key == "1007")
            .ToArray();

        Assert.Equal(2, duplicates.Length);
        Assert.Contains(duplicates, issue =>
            issue.SourceName.EndsWith("技能表.xlsx::Skill_1", StringComparison.Ordinal)
            && issue.SourceRow == 13
            && issue.SourceColumn == 2);
        Assert.Contains(duplicates, issue =>
            issue.SourceName.EndsWith("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1", StringComparison.Ordinal)
            && issue.SourceRow == 7
            && issue.SourceColumn == 2);
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
