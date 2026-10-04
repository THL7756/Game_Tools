using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Serialization;

public sealed class ExportService
{
    public string ExportJson(TableDocument document) => new JsonTableExporter().Export(document);

    public byte[] ExportBytes(TableDocument document) => new BinaryTableExporter().Export(document);

    public ExportResult Export(TableDocument document, ExportOptions options)
    {
        return ExportAll([document], options);
    }

    public ExportResult ExportAll(IEnumerable<TableDocument> documents, ExportOptions options)
    {
        var tableDocuments = TableDocumentMerger.Merge(documents).ToArray();
        if (tableDocuments.Length == 0)
            throw new InvalidOperationException("No table documents were found.");
        if (string.IsNullOrWhiteSpace(options.DataOutputDirectory))
            throw new ArgumentException("Data output directory cannot be empty.");
        if (string.IsNullOrWhiteSpace(options.CodeOutputDirectory))
            throw new ArgumentException("Code output directory cannot be empty.");

        var dataOutput = Path.GetFullPath(options.DataOutputDirectory);
        var codeOutput = Path.GetFullPath(options.CodeOutputDirectory);
        if (IsSameOrDescendant(dataOutput, codeOutput) || IsSameOrDescendant(codeOutput, dataOutput))
            throw new ArgumentException("Data and code output directories must be separate and cannot contain each other.");

        var dataStaging = dataOutput + ".staging";
        var codeStaging = codeOutput + ".staging";
        if (Directory.Exists(dataStaging))
            Directory.Delete(dataStaging, recursive: true);
        if (Directory.Exists(codeStaging))
            Directory.Delete(codeStaging, recursive: true);
        Directory.CreateDirectory(dataStaging);
        Directory.CreateDirectory(codeStaging);

        var files = new List<string>();
        foreach (var document in tableDocuments)
        {
            if (options.GenerateJson)
                files.Add(Write(dataStaging, "Json", document.Schema.Name + ".json", new JsonTableExporter().Export(document)));
            if (options.GenerateBytes)
                files.Add(WriteBytes(dataStaging, "Bytes", document.Schema.Name + ".bytes", new BinaryTableExporter().Export(document)));
            if (options.GenerateCode)
                files.Add(WriteRoot(codeStaging, document.Schema.Name + "Data.cs", CSharpExporter.Export(document)));
            if (options.GenerateManifest)
            {
                var formats = new List<string>();
                if (options.GenerateJson) formats.Add("json");
                if (options.GenerateBytes) formats.Add("bytes");
                files.Add(Write(dataStaging, "Manifest", document.Schema.Name + ".manifest.json", new ManifestExporter().Export(document, formats)));
            }
        }

        if (Directory.Exists(dataOutput))
            Directory.Delete(dataOutput, recursive: true);
        if (Directory.Exists(codeOutput))
            Directory.Delete(codeOutput, recursive: true);
        var dataParent = Path.GetDirectoryName(dataOutput);
        if (!string.IsNullOrWhiteSpace(dataParent))
            Directory.CreateDirectory(dataParent);
        var codeParent = Path.GetDirectoryName(codeOutput);
        if (!string.IsNullOrWhiteSpace(codeParent))
            Directory.CreateDirectory(codeParent);
        Directory.Move(dataStaging, dataOutput);
        Directory.Move(codeStaging, codeOutput);
        return new ExportResult(
            files.Select(path => path.Replace(dataStaging, dataOutput, StringComparison.OrdinalIgnoreCase)
                .Replace(codeStaging, codeOutput, StringComparison.OrdinalIgnoreCase)).ToArray(),
            string.Join(";", tableDocuments.Select(document => SchemaHasher.Compute(document.Schema))));
    }

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Write(string root, string folder, string name, string content)
    {
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteBytes(string root, string folder, string name, byte[] content)
    {
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string WriteRoot(string root, string name, string content)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
