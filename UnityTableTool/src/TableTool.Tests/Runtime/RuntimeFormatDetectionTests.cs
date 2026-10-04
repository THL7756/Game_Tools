using System.Text;
using Company.UnityTableRuntime;
using TableTool.Core.Parsing;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Runtime;

public sealed class RuntimeFormatDetectionTests
{
    [Fact]
    public void DetectsJsonAndLooksUpPrimaryKey()
    {
        var document = CreateDocument();
        var json = Encoding.UTF8.GetBytes(new JsonTableExporter().Export(document));

        var runtime = RuntimeFormat.ReadAuto(json);

        Assert.Equal("Skill", runtime.TableName);
        Assert.True(runtime.TryGet("1001", out var row));
        Assert.Equal("1#2", row["effects"]);
    }

    [Fact]
    public void DetectsUtb1AndLooksUpPrimaryKey()
    {
        var document = CreateDocument();
        var bytes = new BinaryTableExporter().Export(document);

        var runtime = RuntimeFormat.ReadAuto(bytes);

        Assert.Equal("Skill", runtime.TableName);
        Assert.True(runtime.TryGet("1001", out _));
    }

    [Fact]
    public void RejectsUnknownFormat()
    {
        Assert.Throws<FormatException>(() => RuntimeFormat.ReadAuto(Encoding.UTF8.GetBytes("not a table")));
    }

    [Fact]
    public void RejectsCorruptedBinaryPayload()
    {
        var bytes = new BinaryTableDataReaderTestFactory().Create(CreateDocument());
        bytes[^5] ^= 0x01;

        Assert.Throws<FormatException>(() => RuntimeFormat.ReadAuto(bytes));
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

    private sealed class BinaryTableDataReaderTestFactory
    {
        public byte[] Create(TableTool.Core.Models.TableDocument document) => new BinaryTableExporter().Export(document);
    }
}
