using System.Data;
using System.Text;
using ExcelDataReader;

namespace TableTool.Core.Reading;

public static class TableFileReader
{
    public static IReadOnlyList<Parsing.RawTableGrid> Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Table file was not found.", path);

        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase))
            return [ReadDelimited(path)];

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = File.OpenRead(path);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        return dataSet.Tables.Cast<DataTable>().Select(table => new Parsing.RawTableGrid(
            $"{Path.GetFileName(path)}::{table.TableName}",
            table.Rows.Cast<DataRow>().Select(row => (IReadOnlyList<string?>)row.ItemArray.Select(cell => cell == DBNull.Value ? null : Convert.ToString(cell)).ToArray()).ToArray())).ToArray();
    }

    private static Parsing.RawTableGrid ReadDelimited(string path)
    {
        var separator = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        var rows = File.ReadAllLines(path).Select(line => (IReadOnlyList<string?>)line.Split(separator).Cast<string?>().ToArray()).ToArray();
        return new Parsing.RawTableGrid(path, rows);
    }
}
