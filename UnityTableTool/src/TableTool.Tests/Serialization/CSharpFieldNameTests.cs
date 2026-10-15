// 用途：验证 C# 导出保留下划线字段名，并拒绝绕过解析的连字符字段。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class CSharpFieldNameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesUnderscoresInMembersAndRuntimeKeys(bool adapter)
    {
        var source = CSharpExporter.Export(LocalizationRuntimeTests.CreateLocalizationDocument(), adapter);

        Assert.StartsWith("// 用途：", source);
        Assert.Contains("public string Zh_CN;", source);
        Assert.Contains("public string En_US;", source);
        Assert.DoesNotContain("Zh-CN", source);
        if (adapter)
        {
            Assert.Contains("Zh_CN = row.Read<string>(\"zh_CN\")", source);
            Assert.Contains("En_US = row.Read<string>(\"en_US\")", source);
        }
    }

    [Theory]
    [InlineData("zh-CN", "string")]
    [InlineData("label", "beans(text-name#string)")]
    [InlineData("labels", "beans(text-name#string)()")]
    [InlineData("label", "map(int)(string)(beans(text-name#string))")]
    public void RejectsInvalidNamesBeforeReturningSource(string name, string type)
    {
        var document = new TableDocument("invalid.xlsx::Data",
            new TableSchema("Data", [new FieldSchema(1, name, "", TypeDescriptor.Parse(type), FieldTarget.Both, null)], name), []);

        foreach (var adapter in new[] { false, true })
        {
            var error = Assert.Throws<InvalidOperationException>(() => CSharpExporter.Export(document, adapter));
            Assert.Contains("FIELD_NAME_INVALID", error.Message);
            Assert.Contains("_", error.Message);
        }
    }
}
