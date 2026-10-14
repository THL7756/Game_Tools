// 用途：验证精简后的 JSON 协议以及 Unity Runtime 的字段和数组解析行为。
// 编写日期：2026-10-11
// 最近修改日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System.Text;
using System.Text.Json;
using Company.UnityTableRuntime;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class JsonAndRuntimeTests
{
    [Fact]
    public void NormalTableContainsOnlyTheActiveRowsPayload()
    {
        var json = new JsonTableExporter().Export(CreateSkillDocument());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("formatVersion", out _));
        Assert.False(root.TryGetProperty("schemaHash", out _));
        Assert.True(root.TryGetProperty("rows", out var rows));
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
        Assert.False(root.TryGetProperty("data", out _));
        Assert.Equal("Skill", root.GetProperty("tableName").GetString());
        Assert.Equal("int()", root.GetProperty("fields")[1].GetProperty("type").GetString());
    }

    [Fact]
    public void SingletonTableContainsOnlyTheActiveDataPayload()
    {
        var json = new JsonTableExporter().Export(CreateSingletonDocument());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("isSingleton").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
        Assert.False(root.TryGetProperty("rows", out _));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("primaryKey").ValueKind);
    }

    [Fact]
    public void RuntimeReadsCustomArraySeparatorsFromExportedJson()
    {
        var separators = new ArraySeparatorOptions("@", "|", ";");
        var json = new JsonTableExporter().Export(CreateSkillDocument("1@2"), separators);
        var table = JsonTableDataReader.Read(Encoding.UTF8.GetBytes(json));

        Assert.Equal("Skill", table.TableName);
        Assert.Equal("id", table.PrimaryKey);
        Assert.True(table.TryGet("1007", out var row));
        var values = table.View(row).Read<int[]>("values");

        Assert.Equal([1, 2], values);
        Assert.Equal("@", table.Separators.Inner);
    }

    [Fact]
    public void TableManagerSkipsAnIdenticalPayloadAndKeepsLoadedTableNames()
    {
        var data = Encoding.UTF8.GetBytes(new JsonTableExporter().Export(CreateSkillDocument()));
        var manager = new TableManager();

        Assert.True(manager.LoadIfChanged("Skill", data));
        Assert.False(manager.LoadIfChanged("Skill", data.ToArray()));
        Assert.True(manager.IsLoaded("Skill"));
        Assert.Contains("Skill", manager.LoadedTableNames);
        Assert.True(manager.Has("Skill", "1007"));

        manager.Unload("Skill");
        Assert.False(manager.IsLoaded("Skill"));
    }

    [Fact]
    public void TableManagerReloadsWhenPayloadChanges()
    {
        var first = Encoding.UTF8.GetBytes(new JsonTableExporter().Export(CreateSkillDocument()));
        var second = Encoding.UTF8.GetBytes(new JsonTableExporter().Export(CreateSkillDocument("3#4")));
        var manager = new TableManager();

        Assert.True(manager.LoadIfChanged("Skill", first));
        Assert.True(manager.LoadIfChanged("Skill", second));
    }

    private static TableDocument CreateSkillDocument(string values = "1#2") => new(
        "Skill.xlsx::Skill",
        new TableSchema(
            "Skill",
            [
                new FieldSchema(1, "id", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null),
                new FieldSchema(2, "values", string.Empty, TypeDescriptor.Parse("int()"), FieldTarget.Both, null)
            ],
            "id"),
        [new TableRow(
            7,
            false,
            new Dictionary<string, string?>
            {
                ["id"] = "1007",
                ["values"] = values
            })]);

    private static TableDocument CreateSingletonDocument() => new(
        "Global.xlsx::Global",
        new TableSchema(
            "Global",
            [new FieldSchema(1, "slot_num", string.Empty, TypeDescriptor.Parse("int"), FieldTarget.Both, null)],
            "slot_num",
            IsSingleton: true),
        [new TableRow(
            7,
            false,
            new Dictionary<string, string?>
            {
                ["slot_num"] = "1"
            })]);
}
