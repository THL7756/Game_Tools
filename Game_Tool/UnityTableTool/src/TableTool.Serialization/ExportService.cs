using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed class ExportService
{
    public string ExportJson(TableDocument document) => new JsonTableExporter().Export(document);

    public byte[] ExportBytes(TableDocument document) => new BinaryTableExporter().Export(document);

    public ExportResult Export(TableDocument document, ExportOptions options)
    {
        var staging = options.OutputDirectory + ".staging";
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);

        var files = new List<string>();
        if (options.GenerateJson)
            files.Add(Write(staging, "Json", document.Schema.Name + ".json", new JsonTableExporter().Export(document)));
        if (options.GenerateBytes)
            files.Add(WriteBytes(staging, "Bytes", document.Schema.Name + ".bytes", new BinaryTableExporter().Export(document)));
        if (options.GenerateCode)
            files.Add(Write(staging, "Code", document.Schema.Name + "Data.cs", CSharpExporter.Export(document)));
        if (options.GenerateManifest)
        {
            var formats = new List<string>();
            if (options.GenerateJson) formats.Add("json");
            if (options.GenerateBytes) formats.Add("bytes");
            var manifest = new ManifestExporter().Export(document, formats);
            files.Add(Write(staging, "Manifest", document.Schema.Name + ".manifest.json", manifest));
        }

        if (Directory.Exists(options.OutputDirectory))
            Directory.Delete(options.OutputDirectory, recursive: true);
        Directory.Move(staging, options.OutputDirectory);
        return new ExportResult(files.Select(path => path.Replace(staging, options.OutputDirectory, StringComparison.OrdinalIgnoreCase)).ToArray(), SchemaHasher.Compute(document.Schema));
    }

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
}
