// 用途：扫描配置表文件，按文件聚合真实 Sheet，并构建工作台列表模型。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.ComponentModel;
using System.Globalization;
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
            var grids = TableFileReader.ReadDirectory(
                settings.TableDirectory,
                (path, error) => errors.Add($"{Path.GetRelativePath(settings.TableDirectory, path)}：{error.Message}"));
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
        }
        catch (Exception error)
        {
            errors.Add(error.Message);
        }

        var favorites = settings.Favorites.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recent = settings.RecentTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = documents
            .GroupBy(document => GetFileKey(document.SourceName), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateTableModel(settings.TableDirectory, group, favorites, recent))
            .ToArray();

        return new CatalogResult(tables, errors);
    }

    private static TableModel CreateTableModel(
        string tableDirectory,
        IGrouping<string, TableDocument> group,
        IReadOnlySet<string> favorites,
        IReadOnlySet<string> recent)
    {
        var fileKey = group.Key.Replace('\\', '/');
        var sourcePath = Path.GetFullPath(Path.Combine(tableDirectory, fileKey.Replace('/', Path.DirectorySeparatorChar)));
        var sheets = group
            .OrderBy(document => GetSheetName(document.SourceName), StringComparer.OrdinalIgnoreCase)
            .Select(document => new TableSheetModel(document, GetSheetName(document.SourceName)))
            .ToArray();
        var isFavorite = ContainsFileKey(favorites, fileKey, sourcePath, group);
        var isRecent = ContainsFileKey(recent, fileKey, sourcePath, group);
        return new TableModel(
            fileKey,
            Path.GetFileNameWithoutExtension(fileKey),
            sourcePath,
            sheets,
            isFavorite,
            isRecent);
    }

    private static bool ContainsFileKey(
        IReadOnlySet<string> keys,
        string fileKey,
        string sourcePath,
        IEnumerable<TableDocument> documents)
    {
        if (keys.Contains(fileKey) || keys.Contains(sourcePath) || keys.Contains(Path.GetFileName(fileKey)))
            return true;

        return documents.Any(document =>
            keys.Contains(document.SourceName)
            || keys.Contains(document.Schema.Name));
    }

    private static string GetFileKey(string sourceName)
    {
        var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0 ? sourceName[..separator] : sourceName;
    }

    private static string GetSheetName(string sourceName)
    {
        var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0
            ? sourceName[(separator + 2)..]
            : Path.GetFileNameWithoutExtension(sourceName);
    }
}

public sealed class TableSheetModel : INotifyPropertyChanged
{
    private bool isCurrent;
    private string validationLevel = "None";
    private string validationMarker = string.Empty;
    private string validationTooltip = string.Empty;

    public TableSheetModel(TableDocument document, string sheetName)
    {
        Document = document;
        SheetName = sheetName;
    }

    public TableDocument Document { get; }
    public string SheetName { get; }
    public string LogicalTableName => Document.Schema.Name;
    public string ValidationLevel => validationLevel;
    public string ValidationMarker => validationMarker;
    public string ValidationTooltip => validationTooltip;

    public bool IsCurrent
    {
        get => isCurrent;
        set
        {
            if (isCurrent == value)
                return;
            isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public void SetValidationState(string level, string marker, string tooltip)
    {
        validationLevel = level;
        validationMarker = marker;
        validationTooltip = tooltip;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationLevel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMarker)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationTooltip)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TableModel : INotifyPropertyChanged
{
    private bool isSelected;
    private bool isFavorite;
    private TableSheetModel currentSheet;

    public TableModel(
        string fileKey,
        string displayName,
        string sourcePath,
        IReadOnlyList<TableSheetModel> sheets,
        bool isFavorite,
        bool isRecent)
    {
        if (sheets.Count == 0)
            throw new ArgumentException("A table file must contain at least one sheet.", nameof(sheets));

        FileKey = fileKey;
        DisplayName = displayName;
        SourcePath = sourcePath;
        Sheets = sheets;
        currentSheet = sheets[0];
        currentSheet.IsCurrent = true;
        this.isFavorite = isFavorite;
        IsRecent = isRecent;
    }

    public string FileKey { get; }
    public string FavoriteKey => FileKey;
    public string DisplayName { get; }
    public string DisplayLabel => TruncateDisplayName(DisplayName, 7);
    public string SourcePath { get; }
    public bool IsRecent { get; }
    public IReadOnlyList<TableSheetModel> Sheets { get; }
    public TableSheetModel CurrentSheet => currentSheet;
    public TableDocument Document => currentSheet.Document;
    public string SchemaName => currentSheet.LogicalTableName;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
                return;
            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
        }
    }

    public void SelectSheet(TableSheetModel sheet)
    {
        if (!Sheets.Contains(sheet) || ReferenceEquals(currentSheet, sheet))
            return;

        currentSheet.IsCurrent = false;
        currentSheet = sheet;
        currentSheet.IsCurrent = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSheet)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SchemaName)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string TruncateDisplayName(string value, int maxTextElements)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var elements = StringInfo.GetTextElementEnumerator(value);
        var result = new List<string>(maxTextElements + 1);
        while (elements.MoveNext() && result.Count < maxTextElements)
            result.Add(elements.GetTextElement());

        var hasMore = elements.MoveNext();
        return hasMore
            ? string.Concat(result) + "…"
            : value;
    }
}
