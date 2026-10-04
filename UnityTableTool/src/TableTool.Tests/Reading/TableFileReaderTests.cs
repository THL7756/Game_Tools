using TableTool.Core.Reading;
using Xunit;

namespace TableTool.Tests.Reading;

public sealed class TableFileReaderTests
{
    [Fact]
    public void RecursivelyReadsSupportedTableFilesFromOneRootDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "unity-table-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested", "deep"));
        File.WriteAllText(Path.Combine(root, "Skill.csv"), "table: Skill\n字段说明,ID\n字段类型,int\n字段名,id\n客户端服务器,cs\n默认值,0\n,1");
        File.WriteAllText(Path.Combine(root, "nested", "deep", "Item.tsv"), "table: Item\t\n字段说明\tID\n字段类型\tint\n字段名\tid\n客户端服务器\tcs\n默认值\t0\n\t2");
        File.WriteAllText(Path.Combine(root, "ignore.txt"), "not a table");

        try
        {
            var grids = TableFileReader.ReadDirectory(root);

            Assert.Equal(2, grids.Count);
            Assert.Contains(grids, grid => grid.SourceName.Contains("Skill.csv", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(grids, grid => grid.SourceName.Contains("Item.tsv", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
