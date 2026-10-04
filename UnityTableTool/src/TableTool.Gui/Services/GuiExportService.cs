using TableTool.Core.Models;
using TableTool.Serialization;

namespace TableTool.Gui.Services;

public sealed class GuiExportService
{
    private readonly ExportService exportService = new();

    public ExportResult Export(TableDocument document, string dataOutputDirectory, string codeOutputDirectory, bool code, bool json, bool bytes)
    {
        return exportService.Export(document, new ExportOptions(dataOutputDirectory, codeOutputDirectory, code, json, bytes));
    }
}
