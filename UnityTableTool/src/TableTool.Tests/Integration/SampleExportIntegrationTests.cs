using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using TableTool.Serialization;
using Xunit;

namespace TableTool.Tests.Integration;

public sealed class SampleExportIntegrationTests
{
    [Fact]
    public void ScansValidatesAndExportsSampleWorkbooks()
    {
        var root = FindRepositoryRoot();
        var sampleRoot = Path.Combine(root, "samples");
        var updateSamples = Environment.GetEnvironmentVariable("UPDATE_SAMPLES") == "1";
        var outputRoot = updateSamples
            ? Path.Combine(root, "samples", "Exported")
            : Path.Combine(Path.GetTempPath(), "unity-table-tool-integration", Guid.NewGuid().ToString("N"));
        var dataOutput = Path.Combine(outputRoot, "Data");
        var codeOutput = Path.Combine(outputRoot, "Code");

        try
        {
            Console.WriteLine("scan:start");
            var grids = TableFileReader.ReadDirectory(sampleRoot);
            Console.WriteLine($"scan:done {grids.Count}");
            var parser = new TableSchemaParser();
            var validator = new TableValidator();
            var documents = TableDocumentMerger.Merge(grids.Select(parser.Parse)).ToArray();
            Console.WriteLine($"merge:done {documents.Length}");
            var issues = documents.SelectMany(validator.Validate).ToArray();
            Console.WriteLine($"validate:done {issues.Length}");

            Assert.NotEmpty(documents);
            Assert.DoesNotContain(issues, issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);

            Console.WriteLine("export:start");
            var result = new ExportService().ExportAll(documents, new ExportOptions(dataOutput, codeOutput));
            Console.WriteLine($"export:done {result.Files.Count}");

            Assert.NotEmpty(result.Files);
            Assert.True(File.Exists(Path.Combine(dataOutput, "Json", "Skill.json")));
            Assert.True(File.Exists(Path.Combine(dataOutput, "Bytes", "Skill.bytes")));
            Assert.True(File.Exists(Path.Combine(dataOutput, "Manifest", "Skill.manifest.json")));
            Assert.True(File.Exists(Path.Combine(codeOutput, "SkillData.cs")));
        }
        finally
        {
            if (!updateSamples && Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UnityTableTool.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate UnityTableTool repository root.");
    }
}
