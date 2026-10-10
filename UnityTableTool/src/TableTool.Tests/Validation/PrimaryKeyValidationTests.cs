using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class PrimaryKeyValidationTests
{
    [Fact]
    public void ReportsCrossShardDuplicateAtEveryLocation()
    {
        var first = CreateSkill("技能表.xlsx::Skill_1", 13, "1007");
        var second = CreateSkill("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1", 7, "1007");

        var result = TableBatchValidator.Validate(
            [first, second],
            [first.SourceName]);

        var duplicates = result.Issues
            .Where(issue => issue.Code == ErrorCodes.PrimaryKeyDuplicate)
            .ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.Contains(duplicates, issue =>
            issue.SourceName == first.SourceName
            && issue.SourceRow == 13
            && issue.SourceColumn == 2
            && issue.Message.Contains("技能表.xlsx::Skill_1!B13", StringComparison.Ordinal)
            && issue.Message.Contains("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1!B7", StringComparison.Ordinal));
        Assert.Contains(duplicates, issue =>
            issue.SourceName == second.SourceName
            && issue.SourceRow == 7
            && issue.SourceColumn == 2
            && issue.Message.Contains("技能表.xlsx::Skill_1!B13", StringComparison.Ordinal)
            && issue.Message.Contains("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1!B7", StringComparison.Ordinal));
    }

    [Fact]
    public void DoesNotReportUniqueKeysAcrossShards()
    {
        var first = CreateSkill("技能表.xlsx::Skill_1", 13, "1007");
        var second = CreateSkill("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1", 7, "1008");

        var result = TableBatchValidator.Validate(
            [first, second],
            [first.SourceName]);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == ErrorCodes.PrimaryKeyDuplicate);
    }

    [Fact]
    public void StillReportsDuplicateKeysWithinOneSheet()
    {
        var document = new TableDocument(
            "skill.xlsx::Skill_1",
            CreateSkillSchema(),
            [
                new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1007" }),
                new TableRow(8, false, new Dictionary<string, string?> { ["id"] = "1007" })
            ]);

        var issues = new TableValidator().Validate(document)
            .Where(issue => issue.Code == ErrorCodes.PrimaryKeyDuplicate)
            .ToArray();

        Assert.Equal(2, issues.Length);
        Assert.All(issues, issue => Assert.Contains("skill.xlsx::Skill_1!B", issue.Message));
    }

    [Fact]
    public void MappingStillAcceptsAKeyThatIsDuplicatedAcrossShards()
    {
        var first = CreateSkill("技能表.xlsx::Skill_1", 13, "1007");
        var second = CreateSkill("究极无敌雷霆爆炸长的表格名称.xlsx::Skill_1", 7, "1007");
        var item = new TableDocument(
            "item.xlsx::Item_1",
            new TableSchema(
                "Item",
                [new FieldSchema(1, "skill_id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null)],
                "skill_id"),
            [new TableRow(7, false, new Dictionary<string, string?> { ["skill_id"] = "1007" })]);

        var result = TableBatchValidator.Validate(
            [first, second, item],
            [item.SourceName]);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ErrorCodes.PrimaryKeyDuplicate);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == ErrorCodes.TableReferenceKeyMissing);
    }

    private static TableDocument CreateSkill(string sourceName, int sourceRow, string id)
    {
        return new TableDocument(
            sourceName,
            CreateSkillSchema(),
            [new TableRow(sourceRow, false, new Dictionary<string, string?> { ["id"] = id })]);
    }

    private static TableSchema CreateSkillSchema()
    {
        return new TableSchema(
            "Skill",
            [new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null)],
            "id");
    }
}
