// 用途：扫描配置表并构建工作台表列表。
// 最近修改日期：2026-10-06

using System.IO;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Gui.Models;

namespace TableTool.Gui.Services;

public sealed record CatalogResult(
    IReadOnlyList<TableModel> Tables,
    IReadOnlyList<string> Errors);

public sealed class TableCatalogService
{
    public CatalogResult Load(AppSettings settings)
    {
        var errors = new List<string>();
        var documents = new List<TableDocument>();
        try
        {
            var grids = TableFileReader.ReadDirectory(settings.TableDirectory);
            var parser = new TableSchemaParser();
            foreach (var grid in grids)
            {
                try
                {
                    documents.Add(parser.Parse(grid));
                }
                catch (FormatException error)
                {
                    errors.Add($"{grid.SourceName}：{error.Message}");
                }
            }

            documents = [.. TableDocumentMerger.Merge(documents)];
        }
        catch (Exception error)
        {
            errors.Add(error.Message);
        }

        var favorites = settings.Favorites.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recent = settings.RecentTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = documents
            .OrderBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase)
            .Select(document => new TableModel(
                document,
                GetDisplayName(document.Schema.Name),
                GetSourcePath(settings.TableDirectory, document.SourceName),
                favorites.Contains(document.Schema.Name),
                recent.Contains(document.Schema.Name)))
            .ToArray();

        return new CatalogResult(tables, errors);
    }

    private static string GetDisplayName(string schemaName) => schemaName switch
    {
        "Skill" => "技能配置",
        "Item" => "道具配置",
        "GlobalConfig" => "全局配置",
        _ => schemaName
    };

    private static string GetSourcePath(string tableDirectory, string sourceName)
    {
        var fileName = sourceName.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split("::", 2)[0])
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(fileName)
            ? tableDirectory
            : Path.GetFullPath(Path.Combine(tableDirectory, fileName));
    }
}

public sealed class TableModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool isSelected;
    private bool isFavorite;

    public TableModel(TableDocument document, string displayName, string sourcePath, bool isFavorite, bool isRecent)
    {
        Document = document;
        DisplayName = displayName;
        SourcePath = sourcePath;
        this.isFavorite = isFavorite;
        IsRecent = isRecent;
    }

    public TableDocument Document { get; }
    public string SchemaName => Document.Schema.Name;
    public string DisplayName { get; }
    public string SourcePath { get; }
    public bool IsRecent { get; }
    public string DisplayLabel => IsRecent ? $"最近 · {DisplayName}" : DisplayName;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsFavorite
    {
        get => isFavorite;
        set
        {
            if (isFavorite == value)
                return;
            isFavorite = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsFavorite)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
