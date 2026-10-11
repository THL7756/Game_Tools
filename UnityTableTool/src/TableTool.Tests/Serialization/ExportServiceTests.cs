// 用途：验证导出服务只覆盖本次生成文件，不清空输出目录中的其他内容。
// 编写日期：2026-10-11
// 作者：Codex（按用户需求修改）

using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class ExportServiceTests
{
    [Fact]
    public void PreservesExistingFilesAndOverwritesOnlyGeneratedFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var clientData = Path.Combine(root, "Data_c");
            var serverData = Path.Combine(root, "Data_s");
            var clientCode = Path.Combine(root, "Code_c");
            var serverCode = Path.Combine(root, "Code_s");
            Directory.CreateDirectory(clientData);
            File.WriteAllText(Path.Combine(clientData, "manual.txt"), "keep");
            File.WriteAllText(Path.Combine(clientData, "OtherTable.json"), "old table");
            File.WriteAllText(Path.Combine(clientData, "Skill.json"), "old generated content");

            var result = new ExportService().ExportAll(
                [CreateSkillDocument()],
                new ExportOptions(
                    clientData,
                    serverData,
                    clientCode,
                    serverCode,
                    generateCode: false,
                    generateJson: true,
                    generateBytes: false,
                    targets: ExportTarget.Client,
                    codeTargets: ExportTarget.None));

            Assert.Equal("keep", File.ReadAllText(Path.Combine(clientData, "manual.txt")));
            Assert.Equal("old table", File.ReadAllText(Path.Combine(clientData, "OtherTable.json")));
            Assert.Contains("\"tableName\": \"Skill\"", File.ReadAllText(Path.Combine(clientData, "Skill.json")));
            Assert.Contains(Path.Combine(clientData, "Skill.json"), result.Files, StringComparer.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root, "*.staging-*"));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public void PreservesOutputWhenExportFailsBeforeCommit()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var clientData = Path.Combine(root, "Data_c");
            Directory.CreateDirectory(clientData);
            File.WriteAllText(Path.Combine(clientData, "manual.txt"), "keep");

            var invalidCodeDirectory = Path.Combine(root, "Code_c");
            File.WriteAllText(invalidCodeDirectory, "not a directory");

            Assert.ThrowsAny<Exception>(() => new ExportService().ExportAll(
                [CreateSkillDocument()],
                new ExportOptions(
                    clientData,
                    Path.Combine(root, "Data_s"),
                    invalidCodeDirectory,
                    Path.Combine(root, "Code_s"),
                    generateCode: true,
                    generateJson: true,
                    generateBytes: false,
                    targets: ExportTarget.Client,
                    codeTargets: ExportTarget.Client)));

            Assert.Equal("keep", File.ReadAllText(Path.Combine(clientData, "manual.txt")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root, "*.staging-*"));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static TableDocument CreateSkillDocument() => new(
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
                ["values"] = "1#2"
            })]);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "UnityTableToolTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
