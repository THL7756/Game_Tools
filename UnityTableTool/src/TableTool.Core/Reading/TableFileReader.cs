// 用途：读取 Excel、CSV 和 TSV 配置表，并保留文件相对路径及真实 Sheet 名。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

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
        // Excel 会以共享模式打开文件；读取时允许共享读写和删除，避免打开中的工作簿无法识别。
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        return dataSet.Tables.Cast<DataTable>().Select(table => new Parsing.RawTableGrid(
            $"{Path.GetFileName(path)}::{table.TableName}",
            table.Rows.Cast<DataRow>().Select(row => (IReadOnlyList<string?>)row.ItemArray.Select(cell => cell == DBNull.Value ? null : Convert.ToString(cell)).ToArray()).ToArray())).ToArray();
    }

    public static IReadOnlyList<Parsing.RawTableGrid> ReadDirectory(
        string rootDirectory,
        Action<string, Exception>? onFileError = null)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException($"Table root directory was not found: {rootDirectory}");

        return Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(path => ReadFileSafely(path, rootDirectory, onFileError))
            .ToArray();
    }

    private static IReadOnlyList<Parsing.RawTableGrid> ReadFileSafely(
        string path,
        string rootDirectory,
        Action<string, Exception>? onFileError)
    {
        try
        {
            return Read(path).Select(grid => WithRelativeSourceName(grid, path, rootDirectory)).ToArray();
        }
        catch (Exception error)
        {
            onFileError?.Invoke(path, error);
            return [];
        }
    }

    private static Parsing.RawTableGrid WithRelativeSourceName(Parsing.RawTableGrid grid, string path, string rootDirectory)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');
        var separator = grid.SourceName.IndexOf("::", StringComparison.Ordinal);
        var sheetName = separator >= 0 ? grid.SourceName[(separator + 2)..] : string.Empty;
        var sourceName = sheetName.Length == 0 ? relativePath : $"{relativePath}::{sheetName}";
        return grid with { SourceName = sourceName };
    }

    private static Parsing.RawTableGrid ReadDelimited(string path)
    {
        var separator = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = ReadLines(reader)
            .Select(line => (IReadOnlyList<string?>)ParseLine(line, separator).Cast<string?>().ToArray())
            .ToArray();
        return new Parsing.RawTableGrid(path, rows);
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
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
