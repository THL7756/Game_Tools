using TableTool.Core.Parsing;
using TableTool.Core.Models;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Serialization;

public sealed class ExportTests
{
    [Fact]
    public void JsonExportIsDeterministic()
    {
        var document = CreateDocument();
        var exporter = new JsonTableExporter();

        var first = exporter.Export(document);
        var second = exporter.Export(document);

        Assert.Equal(first, second);
        Assert.Contains("Skill", first);
        Assert.Contains("effects", first);
        Assert.Contains("\"type\": \"int()\"", first);
        Assert.DoesNotContain("int(())", first);
    }

    [Fact]
    public void BinaryExportContainsUtb1MagicAndStableCrc()
    {
        var document = CreateDocument();
        var exporter = new BinaryTableExporter();

        var first = exporter.Export(document);
        var second = exporter.Export(document);

        Assert.Equal("UTB1", System.Text.Encoding.ASCII.GetString(first, 0, 4));
        Assert.Equal(first, second);
        Assert.True(first.Length > 32);
    }

    [Fact]
    public void ExportAllSeparatesDataAndCodeDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-tests", Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(root, "data");
        var codeDirectory = Path.Combine(root, "code");
        try
        {
            var result = new ExportService().ExportAll([CreateDocument()], new ExportOptions(dataDirectory, codeDirectory));

            Assert.True(File.Exists(Path.Combine(dataDirectory, "Skill.json")));
            Assert.True(File.Exists(Path.Combine(dataDirectory, "Skill.bytes")));
            Assert.False(File.Exists(Path.Combine(dataDirectory, "Skill.manifest.json")));
            Assert.True(File.Exists(Path.Combine(codeDirectory, "SkillData.cs")));
            Assert.Equal(3, result.Files.Count);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportFiltersFieldsForSelectedTarget()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", "客户端", "服务器"],
            (IReadOnlyList<string?>)["字段类型", "int", "string", "string"],
            (IReadOnlyList<string?>)["字段名", "id", "client_value", "server_value"],
            (IReadOnlyList<string?>)["客户端服务器", "cs", "c", "s"],
            (IReadOnlyList<string?>)["默认值", "0", "", ""],
            (IReadOnlyList<string?>)["1001", "客户端值", "服务器值"]
        });
        var document = new TableSchemaParser().Parse(grid);
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var data = Path.Combine(root, "Data_c");
            var code = Path.Combine(root, "Code");
            new ExportService().ExportAll([document], new ExportOptions(data, code, false, true, false, ExportTarget.Client));
            var json = File.ReadAllText(Path.Combine(data, "Skill.json"));
            Assert.Contains("client_value", json);
            Assert.DoesNotContain("server_value", json);
            Assert.DoesNotContain("target", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportWritesCodeToSeparateTargetDirectories()
    {
        var grid = CreateTargetSplitGrid();
        var document = new TableSchemaParser().Parse(grid);
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var clientData = Path.Combine(root, "Data_c");
            var serverData = Path.Combine(root, "Data_s");
            var clientCode = Path.Combine(root, "Code_c");
            var serverCode = Path.Combine(root, "Code_s");
            new ExportService().ExportAll([document], new ExportOptions(
                clientData,
                serverData,
                clientCode,
                serverCode,
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: ExportTarget.Both,
                codeTargets: ExportTarget.Both));

            Assert.True(File.Exists(Path.Combine(clientCode, "SkillData.cs")));
            Assert.True(File.Exists(Path.Combine(serverCode, "SkillData.cs")));
            Assert.False(Directory.Exists(Path.Combine(clientCode, "Client")));
            Assert.False(Directory.Exists(Path.Combine(clientCode, "Server")));
            Assert.True(File.Exists(Path.Combine(clientData, "Skill.json")));
            Assert.True(File.Exists(Path.Combine(serverData, "Skill.json")));

            var clientJson = File.ReadAllText(Path.Combine(clientData, "Skill.json"));
            var serverJson = File.ReadAllText(Path.Combine(serverData, "Skill.json"));
            Assert.Contains("client_value", clientJson);
            Assert.DoesNotContain("server_value", clientJson);
            Assert.Contains("server_value", serverJson);
            Assert.DoesNotContain("client_value", serverJson);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportAllowsCodeOnlyAndDataOnlyCombinations()
    {
        var document = CreateDocument();
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var dataOnlyRoot = Path.Combine(root, "data-only");
            new ExportService().ExportAll([document], new ExportOptions(
                Path.Combine(dataOnlyRoot, "Data_c"),
                Path.Combine(dataOnlyRoot, "Data_s"),
                Path.Combine(dataOnlyRoot, "Code_c"),
                Path.Combine(dataOnlyRoot, "Code_s"),
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: ExportTarget.Client,
                codeTargets: ExportTarget.None));
            Assert.True(File.Exists(Path.Combine(dataOnlyRoot, "Data_c", "Skill.json")));
            Assert.False(Directory.Exists(Path.Combine(dataOnlyRoot, "Code_c")));
            Assert.False(Directory.Exists(Path.Combine(dataOnlyRoot, "Code_s")));

            var codeOnlyRoot = Path.Combine(root, "code-only");
            new ExportService().ExportAll([document], new ExportOptions(
                Path.Combine(codeOnlyRoot, "Data_c"),
                Path.Combine(codeOnlyRoot, "Data_s"),
                Path.Combine(codeOnlyRoot, "Code_c"),
                Path.Combine(codeOnlyRoot, "Code_s"),
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: ExportTarget.None,
                codeTargets: ExportTarget.Server));
            Assert.True(File.Exists(Path.Combine(codeOnlyRoot, "Code_s", "SkillData.cs")));
            Assert.False(Directory.Exists(Path.Combine(codeOnlyRoot, "Data_c")));
            Assert.False(Directory.Exists(Path.Combine(codeOnlyRoot, "Data_s")));
            Assert.False(Directory.Exists(Path.Combine(codeOnlyRoot, "Code_c")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportRejectsOverlappingOrSharedCodeDirectories()
    {
        var document = CreateDocument();
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var sharedCode = Path.Combine(root, "Code");
            var clientData = Path.Combine(root, "Data_c");
            var serverData = Path.Combine(root, "Data_s");
            Assert.Throws<ArgumentException>(() => new ExportService().ExportAll([document], new ExportOptions(
                clientData,
                serverData,
                sharedCode,
                sharedCode,
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: ExportTarget.Both,
                codeTargets: ExportTarget.Both)));

            Assert.Throws<ArgumentException>(() => new ExportService().ExportAll([document], new ExportOptions(
                clientData,
                serverData,
                Path.Combine(clientData, "Code_c"),
                Path.Combine(root, "Code_s"),
                generateCode: true,
                generateJson: true,
                generateBytes: false,
                targets: ExportTarget.Both,
                codeTargets: ExportTarget.Both)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static RawTableGrid CreateTargetSplitGrid() => new("memory", new[]
    {
        (IReadOnlyList<string?>)["table: Skill"],
        (IReadOnlyList<string?>)["字段说明", "ID", "客户端", "服务器"],
        (IReadOnlyList<string?>)["字段类型", "int", "string", "string"],
        (IReadOnlyList<string?>)["字段名", "id", "client_value", "server_value"],
        (IReadOnlyList<string?>)["客户端服务器", "cs", "c", "s"],
        (IReadOnlyList<string?>)["默认值", "0", "", ""],
        (IReadOnlyList<string?>)["1001", "客户端值", "服务器值"]
    });

    private static TableTool.Core.Models.TableDocument CreateDocument()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", "效果"],
            (IReadOnlyList<string?>)["字段类型", "int", "int()"],
            (IReadOnlyList<string?>)["字段名", "id", "effects"],
            (IReadOnlyList<string?>)["客户端服务器", "", ""],
            (IReadOnlyList<string?>)["默认值", "0", ""],
            (IReadOnlyList<string?>)["1001", "1#2"]
        });
        return new TableSchemaParser().Parse(grid);
    }
}
