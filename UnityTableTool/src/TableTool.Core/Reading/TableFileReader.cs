using System.Data;
using System.Text;
using ExcelDataReader;

namespace TableTool.Core.Reading;

public static class TableFileReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".csv", ".tsv"
    };

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

    public static IReadOnlyList<Parsing.RawTableGrid> ReadDirectory(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException($"Table root directory was not found: {rootDirectory}");

        return Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(Read)
            .ToArray();
    }

    private static Parsing.RawTableGrid ReadDelimited(string path)
    {
        var separator = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        var rows = File.ReadAllLines(path)
            .Select(line => (IReadOnlyList<string?>)ParseLine(line, separator).Cast<string?>().ToArray())
            .ToArray();
        return new Parsing.RawTableGrid(path, rows);
    }

    private static IReadOnlyList<string> ParseLine(string line, char separator)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
                continue;
            }

            if (character == separator && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
                continue;
            }

            value.Append(character);
        }

        if (quoted)
            throw new FormatException("Quoted CSV field is not closed.");

        fields.Add(value.ToString());
        return fields;
    }
}
