using TableTool.Core.Models;
using TableTool.Core.Parsing;

namespace TableTool.Serialization;

public sealed class ExportService
{
    public string ExportJson(TableDocument document) => new JsonTableExporter().Export(document);

    public byte[] ExportBytes(TableDocument document) => new BinaryTableExporter().Export(document);

    public ExportResult Export(TableDocument document, ExportOptions options) => ExportAll([document], options);

    public ExportResult ExportAll(IEnumerable<TableDocument> documents, ExportOptions options)
    {
        var targets = new[] { ExportTarget.Client, ExportTarget.Server }
            .Where(target => options.Targets.HasFlag(target))
            .ToArray();
        if (targets.Length == 0)
            throw new ArgumentException("At least one export target must be selected.");
        if (!options.GenerateCode && !options.GenerateJson && !options.GenerateBytes)
            throw new ArgumentException("At least one output format must be selected.");

        var mergedDocuments = TableDocumentMerger.Merge(documents).ToArray();
        if (mergedDocuments.Length == 0)
            throw new InvalidOperationException("No table documents were found.");

        var dataOutputs = targets.ToDictionary(GetTargetName, target => GetDataOutput(options, target), StringComparer.Ordinal);
        var codeOutput = options.GenerateCode ? Path.GetFullPath(options.CodeOutputDirectory) : null;
        ValidateOutputDirectories(dataOutputs.Values, codeOutput);

        var dataStaging = dataOutputs.ToDictionary(pair => pair.Key, pair => pair.Value + ".staging", StringComparer.Ordinal);
        var codeStaging = codeOutput is null ? null : codeOutput + ".staging";
        foreach (var path in dataStaging.Values.Append(codeStaging).Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (Directory.Exists(path!))
                Directory.Delete(path!, recursive: true);
            Directory.CreateDirectory(path!);
        }

        var files = new List<string>();
        var hashes = new List<string>();
        foreach (var target in targets)
        {
            var targetDocuments = mergedDocuments
                .Select(document => TableDocumentFilter.ForTarget(document, target))
                .ToArray();
            var dataRoot = dataStaging[GetTargetName(target)];
            var codeRoot = codeStaging is null
                ? null
                : targets.Length == 1
                    ? codeStaging
                    : Path.Combine(codeStaging, GetTargetName(target));

            foreach (var document in targetDocuments)
            {
                if (options.GenerateJson)
                    files.Add(Write(dataRoot, document.Schema.Name + ".json", new JsonTableExporter().Export(document)));
                if (options.GenerateBytes)
                    files.Add(WriteBytes(dataRoot, document.Schema.Name + ".bytes", new BinaryTableExporter().Export(document)));
                if (options.GenerateCode)
                    files.Add(WriteRoot(codeRoot!, document.Schema.Name + "Data.cs", CSharpExporter.Export(document)));
                hashes.Add(SchemaHasher.Compute(document.Schema));
            }
        }

        foreach (var pair in dataOutputs)
        {
            ReplaceDirectory(pair.Value, dataStaging[pair.Key]);
            files = files.Select(path => path.Replace(dataStaging[pair.Key], pair.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (codeStaging is not null)
        {
            ReplaceDirectory(codeOutput!, codeStaging);
            files = files.Select(path => path.Replace(codeStaging, codeOutput, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new ExportResult(files, string.Join(";", hashes.Distinct(StringComparer.Ordinal)));
    }

    private static string GetTargetName(ExportTarget target) => target == ExportTarget.Client ? "Client" : "Server";

    private static string GetDataOutput(ExportOptions options, ExportTarget target) =>
        Path.GetFullPath(target == ExportTarget.Client
            ? options.ClientDataOutputDirectory
            : options.ServerDataOutputDirectory);

    private static void ValidateOutputDirectories(IEnumerable<string> dataDirectories, string? codeDirectory)
    {
        var dataPaths = dataDirectories.ToArray();
        if (dataPaths.Any(string.IsNullOrWhiteSpace) || (codeDirectory is not null && string.IsNullOrWhiteSpace(codeDirectory)))
            throw new ArgumentException("Output directories cannot be empty.");

        if (dataPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dataPaths.Length)
            throw new ArgumentException("Client and server data directories must be different.");

        var all = codeDirectory is null ? dataPaths : dataPaths.Append(codeDirectory).ToArray();
        for (var left = 0; left < all.Length; left++)
        {
            for (var right = left + 1; right < all.Length; right++)
            {
                if (IsSameOrDescendant(all[left], all[right]) || IsSameOrDescendant(all[right], all[left]))
                    throw new ArgumentException("Data and code output directories must be separate and cannot contain each other.");
            }
        }
    }

    private static void ReplaceDirectory(string output, string staging)
    {
        var parent = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
        IOException? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(output))
                    Directory.Delete(output, recursive: true);
                Directory.Move(staging, output);
                return;
            }
            catch (IOException error)
            {
                lastError = error;
                Thread.Sleep(100 * (attempt + 1));
            }
        }
        throw lastError ?? new IOException($"Unable to replace output directory '{output}'.");
    }

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Write(string root, string name, string content)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteBytes(string root, string name, byte[] content)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string WriteRoot(string root, string name, string content) => Write(root, name, content);
}
