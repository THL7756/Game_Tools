// 用途：将统一工作簿模型写成可打开的最小 XLSX 文件。
// 编写时间：2026-10-05。
// 作者：Codex。
using System.IO.Compression;
using System.Xml.Linq;
using TableDiff.Core.Models;

namespace TableDiff.Core.IO;

public static class XlsxPackageWriter
{
    private static readonly XNamespace MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static void Write(string path, ExcelWorkbookData workbook)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteXml(archive, "[Content_Types].xml", BuildContentTypes(workbook));
        WriteXml(archive, "_rels/.rels", BuildRootRelationships());
        WriteXml(archive, "xl/workbook.xml", BuildWorkbook(workbook));
        WriteXml(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(workbook));
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            WriteXml(archive, $"xl/worksheets/sheet{index + 1}.xml", BuildSheet(workbook.Sheets[index]));
        }
    }

    private static XDocument BuildContentTypes(ExcelWorkbookData workbook)
    {
        var root = new XElement(ContentTypesNamespace + "Types",
            new XElement(ContentTypesNamespace + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ContentTypesNamespace + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypesNamespace + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            root.Add(new XElement(ContentTypesNamespace + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{index + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static XDocument BuildRootRelationships()
    {
        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(PackageRelationshipNamespace + "Relationships",
            new XElement(PackageRelationshipNamespace + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
    }

    private static XDocument BuildWorkbook(ExcelWorkbookData workbook)
    {
        var sheets = new XElement(MainNamespace + "sheets");
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            sheets.Add(new XElement(MainNamespace + "sheet", new XAttribute("name", workbook.Sheets[index].Name), new XAttribute("sheetId", index + 1), new XAttribute(RelationshipNamespace + "id", $"rId{index + 1}")));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(MainNamespace + "workbook", sheets));
    }

    private static XDocument BuildWorkbookRelationships(ExcelWorkbookData workbook)
    {
        var root = new XElement(PackageRelationshipNamespace + "Relationships");
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            root.Add(new XElement(PackageRelationshipNamespace + "Relationship", new XAttribute("Id", $"rId{index + 1}"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", $"worksheets/sheet{index + 1}.xml")));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static XDocument BuildSheet(ExcelSheetData sheet)
    {
        var sheetData = new XElement(MainNamespace + "sheetData");
        var headerRow = new XElement(MainNamespace + "row", new XAttribute("r", 1));
        for (var index = 0; index < sheet.Headers.Count; index++)
        {
            headerRow.Add(BuildCell(index + 1, 1, sheet.Headers[index]));
        }

        sheetData.Add(headerRow);
        var rowNumber = 2;
        foreach (var row in sheet.Rows)
        {
            var rowElement = new XElement(MainNamespace + "row", new XAttribute("r", rowNumber++));
            for (var index = 0; index < sheet.Headers.Count; index++)
            {
                rowElement.Add(BuildCell(index + 1, rowNumber - 1, row.Values.GetValueOrDefault(sheet.Headers[index])));
            }

            sheetData.Add(rowElement);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(MainNamespace + "worksheet", sheetData));
    }

    private static XElement BuildCell(int columnNumber, int rowNumber, string? value)
    {
        return new XElement(MainNamespace + "c", new XAttribute("r", ColumnName(columnNumber) + rowNumber), new XAttribute("t", "inlineStr"), new XElement(MainNamespace + "is", new XElement(MainNamespace + "t", value ?? string.Empty)));
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

    private static void WriteXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}
