using TableTool.Core.Parsing;
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

            Assert.True(File.Exists(Path.Combine(dataDirectory, "Json", "Skill.json")));
            Assert.True(File.Exists(Path.Combine(dataDirectory, "Bytes", "Skill.bytes")));
            Assert.True(File.Exists(Path.Combine(dataDirectory, "Manifest", "Skill.manifest.json")));
            Assert.True(File.Exists(Path.Combine(codeDirectory, "SkillData.cs")));
            Assert.Equal(4, result.Files.Count);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

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
