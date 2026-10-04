using TableTool.Core.Models;
using TableTool.Serialization;

namespace TableTool.Gui.Services;

public sealed class GuiExportService
{
    private readonly ExportService exportService = new();

    public ExportResult Export(TableDocument document, string outputDirectory, bool code, bool json, bool bytes)
    {
        return exportService.Export(document, new ExportOptions(outputDirectory, code, json, bytes));
    }
}
