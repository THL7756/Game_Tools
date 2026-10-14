// 用途：验证复杂字段中的逻辑表引用、多语言键校验和依赖闭包展开。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class ComplexReferenceTests
{
    [Fact]
    public void ExpandsBeanAndLocalizationDependencies()
    {
        var source = CreateSourceDocument("source.xlsx::Inventory", "999#2", "greeting");
        var item = CreateItemDocument();
        var localization = CreateLocalizationDocument();

        var expanded = TableSelectionResolver.Expand(
            [source, item, localization],
            [source.SourceName]);

        Assert.Contains(expanded, document => document.Schema.Name == "Inventory");
        Assert.Contains(expanded, document => document.Schema.Name == "item");
        Assert.Contains(expanded, document => document.Schema.Name == "localization");
    }

    [Fact]
    public void ValidatesNestedIdAndLocalizationKeys()
    {
        var source = CreateSourceDocument("source.xlsx::Inventory", "999#2", "missing");
        var item = CreateItemDocument();
        var localization = CreateLocalizationDocument();

        var issues = TableReferenceValidator.Validate(
            [source, item, localization],
            [source]);

        Assert.Contains(issues, issue => issue.Code == ErrorCodes.TableReferenceKeyMissing);
        Assert.Contains(issues, issue => issue.Code == ErrorCodes.LocalizationKeyMissing);
    }

    private static TableDocument CreateSourceDocument(string sourceName, string beanValue, string localizationKey) => new(
        sourceName,
        new TableSchema(
            "Inventory",
            [
                new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
                new FieldSchema(2, "bundle", string.Empty, TypeDescriptor.Parse("beans(item_id#int|count#int)"), FieldTarget.Both, null),
                new FieldSchema(3, "label", string.Empty, TypeDescriptor.Parse("localization"), FieldTarget.Both, null)
            ],
            "id"),
        [new TableRow(7, false, new Dictionary<string, string?>
        {
            ["id"] = "1",
            ["bundle"] = beanValue,
            ["label"] = localizationKey
        })]);

    private static TableDocument CreateItemDocument() => new(
        "item.xlsx::item",
        new TableSchema(
            "item",
            [new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null)],
            "id"),
        [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "1001" })]);

    private static TableDocument CreateLocalizationDocument() => new(
        "localization.xlsx::localization",
        new TableSchema(
            "localization",
            [new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null)],
            "id"),
        [new TableRow(7, false, new Dictionary<string, string?> { ["id"] = "greeting" })]);
}
