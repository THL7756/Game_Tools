// 用途：按客户端、服务器和代码目录输出配置表文件，并以暂存目录安全替换旧输出。
// 编写日期：2026-10-06
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

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
        var wantsData = options.GenerateJson || options.GenerateBytes;
        var dataTargets = wantsData
            ? new[] { ExportTarget.Client, ExportTarget.Server }.Where(target => options.Targets.HasFlag(target)).ToArray()
            : [];
        var codeTargets = options.GenerateCode
            ? new[] { ExportTarget.Client, ExportTarget.Server }.Where(target => options.CodeTargets.HasFlag(target)).ToArray()
            : [];
        if (dataTargets.Length == 0 && codeTargets.Length == 0)
            throw new ArgumentException("At least one export target must be selected.");

        var mergedDocuments = TableDocumentMerger.Merge(documents).ToArray();
        if (mergedDocuments.Length == 0)
            throw new InvalidOperationException("No table documents were found.");

        var dataOutputs = dataTargets.ToDictionary(GetTargetName, target => GetDataOutput(options, target), StringComparer.Ordinal);
        var codeOutputs = codeTargets.ToDictionary(GetTargetName, target => GetCodeOutput(options, target), StringComparer.Ordinal);
        ValidateOutputDirectories(dataOutputs, codeOutputs);

        var dataStaging = dataOutputs.ToDictionary(pair => pair.Key, pair => pair.Value + ".staging", StringComparer.Ordinal);
        var codeStaging = codeOutputs.ToDictionary(pair => pair.Key, pair => pair.Value + ".staging", StringComparer.Ordinal);
        foreach (var path in dataStaging.Values.Concat(codeStaging.Values))
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
        }

        var files = new List<string>();
        var hashes = new List<string>();
        var activeTargets = dataTargets.Concat(codeTargets).Distinct().ToArray();
        foreach (var target in activeTargets)
        {
            var targetDocuments = mergedDocuments
                .Select(document => TableDocumentFilter.ForTarget(document, target))
                .ToArray();
            var dataRoot = dataStaging.TryGetValue(GetTargetName(target), out var dataPath) ? dataPath : null;
            var codeRoot = codeStaging.TryGetValue(GetTargetName(target), out var codePath) ? codePath : null;

            foreach (var document in targetDocuments)
            {
                if (dataRoot is not null && options.GenerateJson)
                    files.Add(Write(dataRoot, document.Schema.Name + ".json", new JsonTableExporter().Export(document, options.ArraySeparators)));
                if (dataRoot is not null && options.GenerateBytes)
                    files.Add(WriteBytes(dataRoot, document.Schema.Name + ".bytes", new BinaryTableExporter().Export(document, options.ArraySeparators)));
                if (codeRoot is not null && options.GenerateCode)
                    files.Add(Write(codeRoot, document.Schema.Name + "Data.cs", CSharpExporter.Export(document, target == ExportTarget.Client)));
                hashes.Add(SchemaHasher.Compute(document.Schema));
            }
        }

        foreach (var pair in dataOutputs)
        {
            ReplaceDirectory(pair.Value, dataStaging[pair.Key]);
            files = files.Select(path => path.Replace(dataStaging[pair.Key], pair.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        foreach (var pair in codeOutputs)
        {
            ReplaceDirectory(pair.Value, codeStaging[pair.Key]);
            files = files.Select(path => path.Replace(codeStaging[pair.Key], pair.Value, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new ExportResult(files, string.Join(";", hashes.Distinct(StringComparer.Ordinal)));
    }

    private static string GetTargetName(ExportTarget target) => target == ExportTarget.Client ? "Client" : "Server";

    private static string GetDataOutput(ExportOptions options, ExportTarget target) =>
        Path.GetFullPath(target == ExportTarget.Client
            ? options.ClientDataOutputDirectory
            : options.ServerDataOutputDirectory);

    private static string GetCodeOutput(ExportOptions options, ExportTarget target) =>
        Path.GetFullPath(target == ExportTarget.Client
            ? options.ClientCodeOutputDirectory
            : options.ServerCodeOutputDirectory);

    private static void ValidateOutputDirectories(
        IReadOnlyDictionary<string, string> dataOutputs,
        IReadOnlyDictionary<string, string> codeOutputs)
    {
        if (dataOutputs.Values.Any(string.IsNullOrWhiteSpace) || codeOutputs.Values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Output directories cannot be empty.");

        if (dataOutputs.Count == 2
            && string.Equals(dataOutputs["Client"], dataOutputs["Server"], StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Client and server data directories must be different.");

        if (codeOutputs.Count == 2
            && string.Equals(codeOutputs["Client"], codeOutputs["Server"], StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Client and server code directories must be different.");

        var all = dataOutputs.Values.Concat(codeOutputs.Values).ToArray();
        for (var left = 0; left < all.Length; left++)
        {
            for (var right = left + 1; right < all.Length; right++)
            {
                if (IsSameOrDescendant(all[left], all[right]) || IsSameOrDescendant(all[right], all[left]))
                    throw new ArgumentException("Output directories must be separate and cannot contain each other.");
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
}
