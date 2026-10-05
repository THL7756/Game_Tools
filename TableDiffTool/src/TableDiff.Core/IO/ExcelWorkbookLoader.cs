// 用途：使用 .NET 内置 ZIP/XML API 读取 xlsx 工作簿并转换为统一模型。
// 编写时间：2026-10-05。
// 作者：Codex。
using System.IO.Compression;
using System.Xml.Linq;
using TableDiff.Core.Models;

namespace TableDiff.Core.IO;

public static class ExcelWorkbookLoader
{
    private static readonly XNamespace MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static ExcelWorkbookData Load(string path, ExcelDiffOptions? options = null)
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("TableDiff 当前首批支持 .xlsx 文件。");
        }

        options ??= new ExcelDiffOptions();
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadSharedStrings(archive);
        var workbookDocument = ReadXml(archive, "xl/workbook.xml");
        var relationshipDocument = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var relationships = relationshipDocument.Root?.Elements(PackageRelationshipsNamespace + "Relationship")
            .ToDictionary(element => (string?)element.Attribute("Id") ?? string.Empty, element => (string?)element.Attribute("Target") ?? string.Empty, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new ExcelWorkbookData();

        foreach (var sheetElement in workbookDocument.Root?.Element(MainNamespace + "sheets")?.Elements(MainNamespace + "sheet") ?? [])
        {
            var name = (string?)sheetElement.Attribute("name") ?? "Sheet";
            var relationshipId = (string?)sheetElement.Attribute(DocumentRelationshipsNamespace + "id") ?? string.Empty;
            if (!relationships.TryGetValue(relationshipId, out var target))
            {
                continue;
            }

            var sheetDocument = ReadXml(archive, NormalizeSheetPath(target));
            var rawRows = ReadRows(sheetDocument, sharedStrings);
            var header = rawRows.FirstOrDefault(row => row.Values.Values.Any(value => !string.IsNullOrWhiteSpace(value)));
            if (header is null)
            {
                continue;
            }

            var headers = BuildHeaders(header.Values);
            var keyColumn = ResolveKeyColumn(headers, options.KeyColumn);
            var sheetData = new ExcelSheetData(name, header.RowIndex, headers, keyColumn);
            var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var rawRow in rawRows.Where(row => row.RowIndex > header.RowIndex))
            {
                if (rawRow.Values.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                var values = headers.Select((headerName, index) => new
                    {
                        Header = headerName,
                        Value = rawRow.Values.GetValueOrDefault(ColumnName(index + 1))
                    })
                    .ToDictionary(item => item.Header, item => item.Value, StringComparer.Ordinal);
                var key = values.GetValueOrDefault(keyColumn);
                key = string.IsNullOrWhiteSpace(key) ? $"__row_{rawRow.RowIndex}" : key;
                if (duplicateCounts.TryGetValue(key, out var count))
                {
                    count++;
                    duplicateCounts[key] = count;
                    key = $"{key}#duplicate{count}";
                }
                else
                {
                    duplicateCounts[key] = 1;
                }

                sheetData.Rows.Add(new ExcelRowData(key, rawRow.RowIndex, values));
            }

            result.Sheets.Add(sheetData);
        }

        return result;
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"XLSX entry not found: {path}");
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string NormalizeSheetPath(string target)
    {
        target = target.Replace('\\', '/').TrimStart('/');
        return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root?.Elements(MainNamespace + "si")
            .Select(item => string.Concat(item.Descendants(MainNamespace + "t").Select(text => text.Value)))
            .ToList() ?? [];
    }

    private static List<RawRow> ReadRows(XDocument document, IReadOnlyList<string> sharedStrings)
    {
        var rows = new List<RawRow>();
        foreach (var rowElement in document.Root?.Element(MainNamespace + "sheetData")?.Elements(MainNamespace + "row") ?? [])
        {
            var rowIndex = (int?)rowElement.Attribute("r") ?? rows.Count + 1;
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var cell in rowElement.Elements(MainNamespace + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var column = ColumnName(reference);
                var type = (string?)cell.Attribute("t");
                var value = type == "inlineStr"
                    ? string.Concat(cell.Descendants(MainNamespace + "t").Select(text => text.Value))
                    : cell.Element(MainNamespace + "v")?.Value;
                if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                {
                    value = sharedStrings[sharedIndex];
                }

                var formula = cell.Element(MainNamespace + "f")?.Value;
                values[column] = formula is null ? value : "=" + formula;
            }

            rows.Add(new RawRow(rowIndex, values));
        }

        return rows;
    }

    private static List<string> BuildHeaders(IReadOnlyDictionary<string, string?> values)
    {
        var maxColumn = values.Keys.Select(ColumnNumber).DefaultIfEmpty(0).Max();
        var headers = new List<string>();
        for (var column = 1; column <= maxColumn; column++)
        {
            var name = values.GetValueOrDefault(ColumnName(column));
            headers.Add(string.IsNullOrWhiteSpace(name) ? $"Column{column}" : name.Trim());
        }

        return headers;
    }

    private static string ResolveKeyColumn(IReadOnlyList<string> headers, string? requested)
    {
        return !string.IsNullOrWhiteSpace(requested) && headers.Contains(requested, StringComparer.Ordinal)
            ? requested
            : headers[0];
    }

    private static string ColumnName(string reference)
    {
        return new string(reference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
    }

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0)
        {
            number--;
            result = (char)('A' + number % 26) + result;
            number /= 26;
        }

        return result;
    }

    private static int ColumnNumber(string name)
    {
        var number = 0;
        foreach (var character in name)
        {
            number = number * 26 + character - 'A' + 1;
        }

        return number;
    }

    private sealed record RawRow(int RowIndex, Dictionary<string, string?> Values);
}
