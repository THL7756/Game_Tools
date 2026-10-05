using TableTool.Core.Models;
using TableTool.Serialization;

namespace TableTool.Gui.Services;

public sealed class GuiExportService
{
    private readonly ExportService exportService = new();

    public ExportResult Export(
        IEnumerable<TableDocument> documents,
        string dataOutputDirectory,
        string codeOutputDirectory,
        ExportTarget target,
        bool code,
        bool json,
        bool bytes)
    {
        return exportService.ExportAll(
            documents,
            new ExportOptions(dataOutputDirectory, codeOutputDirectory, code, json, bytes, target));
    }
}
