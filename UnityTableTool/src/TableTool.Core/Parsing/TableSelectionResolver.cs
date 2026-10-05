using TableTool.Core.Models;

namespace TableTool.Core.Parsing;

public static class TableSelectionResolver
{
    public static IReadOnlyList<TableDocument> Expand(
        IEnumerable<TableDocument> documents,
        IEnumerable<string> selectedSourceNames)
    {
        var all = documents.ToArray();
        var selectedSources = new HashSet<string>(selectedSourceNames, StringComparer.OrdinalIgnoreCase);
        var selectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in all)
        {
            if (selectedSources.Contains(document.SourceName))
                selectedTables.Add(document.Schema.Name);
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var table in all.Where(document => selectedTables.Contains(document.Schema.Name)))
            {
                foreach (var field in table.Schema.Fields)
                {
                    if (!field.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var referencedName = field.Name[..^3];
                    var referenced = all.FirstOrDefault(document =>
                        document.Schema.Name.Equals(referencedName, StringComparison.OrdinalIgnoreCase));
                    if (referenced is not null && selectedTables.Add(referenced.Schema.Name))
                        changed = true;
                }
            }
        }

        return all.Where(document => selectedTables.Contains(document.Schema.Name)).ToArray();
    }

    public static IReadOnlyList<string> FindMissingReferences(
        IEnumerable<TableDocument> allDocuments,
        IEnumerable<TableDocument> selectedDocuments)
    {
        var available = allDocuments
            .Select(document => document.Schema.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return selectedDocuments
            .SelectMany(document => document.Schema.Fields)
            .Where(field => field.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Name[..^3])
            .Where(name => !available.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string GetSourceFileName(string sourceName)
    {
        var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
        var fileName = separator >= 0 ? sourceName[..separator] : sourceName;
        return Path.GetFileName(fileName);
    }
}
