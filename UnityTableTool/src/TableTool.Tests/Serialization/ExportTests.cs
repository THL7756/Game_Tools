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
