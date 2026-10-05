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
        var sampleRoot = Path.Combine(root, "Data");
        var updateSamples = Environment.GetEnvironmentVariable("UPDATE_SAMPLES") == "1";
        var outputRoot = updateSamples
            ? Path.Combine(root, "Data_c")
            : Path.Combine(Path.GetTempPath(), "unity-table-tool-integration", Guid.NewGuid().ToString("N"));
        var dataOutput = updateSamples ? outputRoot : Path.Combine(outputRoot, "Data_c");
        var codeOutput = updateSamples ? Path.Combine(root, "Code") : Path.Combine(outputRoot, "Code");

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
            Assert.NotEmpty(Directory.EnumerateFiles(dataOutput, "*.json"));
            Assert.NotEmpty(Directory.EnumerateFiles(dataOutput, "*.bytes"));
            Assert.Empty(Directory.EnumerateFiles(dataOutput, "*.manifest.json", SearchOption.AllDirectories));
            Assert.NotEmpty(Directory.EnumerateFiles(codeOutput, "*Data.cs"));
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
