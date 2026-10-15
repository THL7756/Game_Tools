// 用途：验证多语言索引查询、缺失语言、加载和卸载及语言表更新行为。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System.Text;
using Company.UnityTableRuntime;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class LocalizationRuntimeTests
{
    [Fact]
    public void QueriesLanguagesFromTheLoadedTableAndKeepsKeyCaseSensitivity()
    {
        var manager = new TableManager();
        manager.Load("localization", ExportLocalization());

        Assert.Equal("你好", manager.GetLocalizationText("greeting", "zh-CN"));
        Assert.Equal("Hello", manager.GetLocalizationText("greeting", "EN-us"));
        Assert.Null(manager.GetLocalizationText("greeting", "fr-FR"));
        Assert.Null(manager.GetLocalizationText("greeting", "id"));
        Assert.Throws<KeyNotFoundException>(() => manager.GetLocalizationText("Greeting", "en-US"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ReturnsNullForMissingTranslations(string? text)
    {
        var manager = new TableManager();
        manager.Load("localization", ExportLocalization(text));
        Assert.Null(manager.GetLocalizationText("greeting", "en-US"));
    }

    [Fact]
    public void UsesLanguageColumnDefaults()
    {
        var document = CreateLocalizationDocument(null);
        document = document with
        {
            Schema = document.Schema with
            {
                Fields = document.Schema.Fields.Select(field => field.Name == "en-US"
                    ? field with { DefaultValue = "Default text" } : field).ToArray()
            }
        };
        var manager = new TableManager();
        manager.Load("localization", Encoding.UTF8.GetBytes(new JsonTableExporter().Export(document)));
        Assert.Equal("Default text", manager.GetLocalizationText("greeting", "en-US"));
    }

    [Fact]
    public void ReloadAndUnloadImmediatelyAffectTextQueries()
    {
        var manager = new TableManager();
        Assert.Throws<KeyNotFoundException>(() => manager.GetLocalizationText("greeting", "en-US"));
        var initial = ExportLocalization();
        Assert.True(manager.LoadIfChanged("localization", initial));
        Assert.False(manager.LoadIfChanged("localization", initial));
        Assert.True(manager.LoadIfChanged("localization", ExportLocalization("Hi")));
        Assert.Equal("Hi", manager.GetLocalizationText("greeting", "en-US"));

        manager.Unload("localization");
        Assert.Throws<KeyNotFoundException>(() => manager.GetLocalizationText("greeting", "en-US"));
        manager.Load("localization", initial);
        manager.Clear();
        Assert.Throws<KeyNotFoundException>(() => manager.GetLocalizationText("greeting", "en-US"));
    }

    private static byte[] ExportLocalization(string? english = "Hello") =>
        Encoding.UTF8.GetBytes(new JsonTableExporter().Export(CreateLocalizationDocument(english)));

    internal static TableDocument CreateLocalizationDocument(string? english = "Hello") => new(
        "localization.xlsx::localization",
        new TableSchema("localization",
        [
            new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null),
            new FieldSchema(2, "zh-CN", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Client, null),
            new FieldSchema(3, "en-US", string.Empty, TypeDescriptor.Parse("string"), FieldTarget.Both, null)
        ], "id"),
        [new TableRow(7, false, new Dictionary<string, string?>
        {
            ["id"] = "greeting", ["zh-CN"] = "你好", ["en-US"] = english
        })]);
}
