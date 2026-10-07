using TableTool.Core.Models;
using TableTool.Serialization;

namespace TableTool.Gui.Services;

public sealed class GuiExportService
{
    private readonly ExportService exportService = new();

    public ExportResult Export(
        IEnumerable<TableDocument> documents,
        string clientDataOutputDirectory,
        string serverDataOutputDirectory,
        string clientCodeOutputDirectory,
        string serverCodeOutputDirectory,
        ExportTarget targets,
        ExportTarget codeTargets,
        bool code,
        bool json,
        bool bytes)
    {
        return exportService.ExportAll(
            documents,
            new ExportOptions(
                clientDataOutputDirectory,
                serverDataOutputDirectory,
                clientCodeOutputDirectory,
                serverCodeOutputDirectory,
                code,
                json,
                bytes,
                targets,
                codeTargets));
    }
}
