// 用途：按客户端、服务器和代码目录输出配置表文件，只覆盖本次生成的文件，并传递复杂类型上下文。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

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

        var stagingRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        var hashes = new List<string>();
        var localizationCatalog = ComplexValueSerializer.BuildLocalizationCatalog(mergedDocuments);
        try
        {
            foreach (var output in dataOutputs.Values.Concat(codeOutputs.Values).Distinct(StringComparer.OrdinalIgnoreCase))
                stagingRoots[output] = CreateStagingDirectory(output);

            var dataStaging = dataOutputs.ToDictionary(pair => pair.Key, pair => stagingRoots[pair.Value], StringComparer.Ordinal);
            var codeStaging = codeOutputs.ToDictionary(pair => pair.Key, pair => stagingRoots[pair.Value], StringComparer.Ordinal);
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
                        files.Add(Write(dataRoot, document.Schema.Name + ".json", new JsonTableExporter().Export(document, options.ArraySeparators, localizationCatalog)));
                    if (dataRoot is not null && options.GenerateBytes)
                        files.Add(WriteBytes(dataRoot, document.Schema.Name + ".bytes", new BinaryTableExporter().Export(document, options.ArraySeparators, localizationCatalog)));
                    if (codeRoot is not null && options.GenerateCode)
                        files.Add(Write(codeRoot, document.Schema.Name + "Data.cs", CSharpExporter.Export(document, target == ExportTarget.Client)));
                    hashes.Add(SchemaHasher.Compute(document.Schema));
                }
            }

            foreach (var pair in dataOutputs)
                files = CommitStagedFiles(pair.Value, dataStaging[pair.Key], files);
            foreach (var pair in codeOutputs)
                files = CommitStagedFiles(pair.Value, codeStaging[pair.Key], files);

            return new ExportResult(files, string.Join(";", hashes.Distinct(StringComparer.Ordinal)));
        }
        finally
        {
            foreach (var path in stagingRoots.Values)
                DeleteDirectoryIfExists(path);
        }
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

    private static string CreateStagingDirectory(string output)
    {
        var parent = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
        var staging = output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + ".staging-"
            + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        return staging;
    }

    private static List<string> CommitStagedFiles(string output, string staging, IEnumerable<string> files)
    {
        var committed = files
            .Where(path => IsPathUnder(path, staging))
            .Select(path => (Staged: path, Relative: Path.GetRelativePath(staging, path)))
            .ToArray();
        if (committed.Length == 0)
            return files.ToList();

        Directory.CreateDirectory(output);
        var result = files.ToList();
        foreach (var item in committed)
        {
            var destination = Path.Combine(output, item.Relative);
            ReplaceFileAtomically(item.Staged, destination);
            var index = result.FindIndex(path => string.Equals(path, item.Staged, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                result[index] = destination;
        }
        return result;
    }

    private static void ReplaceFileAtomically(string staged, string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);

        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        IOException? lastError = null;
        try
        {
            File.Copy(staged, temporary, overwrite: true);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (File.Exists(destination))
                        File.Delete(destination);
                    File.Move(temporary, destination);
                    return;
                }
                catch (IOException error)
                {
                    lastError = error;
                    Thread.Sleep(100 * (attempt + 1));
                }
            }
            throw lastError ?? new IOException($"Unable to replace output file '{destination}'.");
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static bool IsPathUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录清理失败不应覆盖导出结果或改变用户目录内容。
        }
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
