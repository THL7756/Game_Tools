using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using TableTool.Gui.Rules;
using TableTool.Serialization;

namespace TableTool.Gui;

public partial class MainWindow : Window
{
    private readonly TableSchemaParser parser = new();
    private readonly TableValidator validator = new();
    private readonly ObservableCollection<TableFileItem> visibleItems = new();
    private IReadOnlyList<TableDocument> documents = Array.Empty<TableDocument>();
    private IReadOnlyList<ValidationIssue> issues = Array.Empty<ValidationIssue>();
    private string defaultClientDataOutputPath = string.Empty;
    private string defaultServerDataOutputPath = string.Empty;
    private bool hasScanErrors;
    private bool hasLoaded;

    public MainWindow()
    {
        InitializeComponent();
        TableList.ItemsSource = visibleItems;
        SetDefaultPaths();
        ThemeComboBox.SelectedIndex = 0;
        ListFilterComboBox.SelectedIndex = 0;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (hasLoaded)
            return;
        hasLoaded = true;
        Scan_Click(sender, e);
    }

    private void SetDefaultPaths()
    {
        var root = FindProjectRoot();
        TablePathBox.Text = Path.Combine(root, "Data");
        defaultClientDataOutputPath = Path.Combine(root, "Data_c");
        defaultServerDataOutputPath = Path.Combine(root, "Data_s");
        ClientDataOutputPathBox.Text = defaultClientDataOutputPath;
        ServerDataOutputPathBox.Text = defaultServerDataOutputPath;
        CodeOutputPathBox.Text = Path.Combine(root, "Code");
    }

    private void BrowseTablePath_Click(object sender, RoutedEventArgs e) => BrowseFolder(TablePathBox);
    private void BrowseDataOutputPath_Click(object sender, RoutedEventArgs e) => BrowseFolder((sender as Button)?.Tag?.ToString() == "server" ? ServerDataOutputPathBox : ClientDataOutputPathBox);
    private void BrowseCodeOutputPath_Click(object sender, RoutedEventArgs e) => BrowseFolder(CodeOutputPathBox);

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => SettingsTab.IsSelected = true;

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not ComboBoxItem item)
            return;
        var theme = item.Tag?.ToString();
        var dark = theme == "dark" || (theme == "system" && IsSystemDarkTheme());
        var color = dark ? System.Windows.Media.Color.FromRgb(31, 31, 31) : System.Windows.Media.Color.FromRgb(255, 255, 255);
        Resources["WindowBackground"] = new System.Windows.Media.SolidColorBrush(color);
        Background = (System.Windows.Media.Brush)Resources["WindowBackground"];
        Foreground = new System.Windows.Media.SolidColorBrush(dark ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black);
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) == 0;
        }
        catch { return false; }
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => FontSize = e.NewValue;

    private void BrowseFolder(TextBox target)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(target.Text.Trim()))
            dialog.FolderName = target.Text.Trim();
        if (dialog.ShowDialog(this) == true)
            target.Text = dialog.FolderName;
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        documents = Array.Empty<TableDocument>();
        issues = Array.Empty<ValidationIssue>();
        hasScanErrors = false;
        visibleItems.Clear();
        LogList.Items.Clear();

        try
        {
            var root = TablePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("请选择表根目录。");

            var parsed = new List<TableDocument>();
            foreach (var grid in TableFileReader.ReadDirectory(root))
            {
                try
                {
                    parsed.Add(parser.Parse(grid));
                }
                catch (Exception error)
                {
                    hasScanErrors = true;
                    LogList.Items.Add($"错误：{grid.SourceName}: {error.Message}");
                }
            }

            documents = parsed;
            BuildFileItems();
            if (documents.Count == 0 && !hasScanErrors)
                LogList.Items.Add("未发现可解析的表格。");
            foreach (var group in documents.GroupBy(document => document.Schema.Name, StringComparer.OrdinalIgnoreCase))
            {
                var files = group.Select(document => TableSelectionResolver.GetSourceFileName(document.SourceName))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                LogList.Items.Add($"已读取 {group.Key}，分表文件：{string.Join("、", files)}");
            }
        }
        catch (Exception error)
        {
            hasScanErrors = true;
            LogList.Items.Add($"错误：{error.Message}");
        }
    }

    private void BuildFileItems()
    {
        var recent = LoadRecentFiles();
        var favorites = LoadFavorites();
        var items = documents
            .GroupBy(document => TableSelectionResolver.GetSourceFileName(document.SourceName), StringComparer.OrdinalIgnoreCase)
            .Select(group => new TableFileItem(
                group.Key,
                group.Select(document => document.Schema.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                recent.Contains(group.Key, StringComparer.OrdinalIgnoreCase),
                Path.Combine(TablePathBox.Text.Trim(), group.Key),
                favorites.Contains(group.Key, StringComparer.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.IsFavorite)
            .ThenByDescending(item => item.IsRecent)
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var item in items)
            visibleItems.Add(item);
        TableList.ItemsSource = visibleItems;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyListFilter();
    }

    private void ListFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyListFilter();

    private void ApplyListFilter()
    {
        if (SearchBox is null || ListFilterComboBox is null || TableList is null)
            return;
        var query = SearchBox.Text.Trim();
        var filter = (ListFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        var candidates = filter switch
        {
            "favorite" => visibleItems.Where(item => item.IsFavorite),
            "recent" => visibleItems.Where(item => item.IsRecent).Take(10),
            _ => visibleItems
        };
        var items = candidates.Where(item => string.IsNullOrWhiteSpace(query)
            || item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.LogicalTables.Any(table => table.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var item in visibleItems)
            item.IsVisible = items.Contains(item);
        TableList.ItemsSource = items;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (documents.Count == 0)
            Scan_Click(sender, e);
        if (documents.Count == 0 || hasScanErrors)
        {
            if (hasScanErrors)
                LogList.Items.Add("扫描存在错误，修复后重新扫描。");
            return;
        }

        var selected = ResolveSelectedDocuments();
        if (selected.Count == 0)
            return;

        var missingReferences = TableSelectionResolver.FindMissingReferences(documents, selected);
        if (missingReferences.Count > 0)
        {
            issues = missingReferences
                .Select(name => new ValidationIssue(ErrorCodes.TableReferenceMissing, ValidationSeverity.Error,
                    $"关联表 '{name}' 不存在。", "selection"))
                .ToArray();
            LogList.Items.Add($"错误：关联表不存在：{string.Join("、", missingReferences)}");
            return;
        }

        try
        {
            var merged = TableDocumentMerger.Merge(selected);
            issues = merged.SelectMany(validator.Validate).ToArray();
            LogList.Items.Clear();
            foreach (var issue in issues)
                LogList.Items.Add($"{issue.Severity} {issue.Code}: {issue.Message} ({issue.SourceName}:{issue.SourceRow}:{issue.SourceColumn})");
            if (issues.Count == 0)
                LogList.Items.Add($"校验通过，共 {merged.Count} 张逻辑表。");
        }
        catch (Exception error)
        {
            issues = [new ValidationIssue(ErrorCodes.TableMergeInvalid, ValidationSeverity.Error, error.Message, "selection")];
            LogList.Items.Add($"错误：{error.Message}");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOutputPaths())
            return;
        if (JsonRadioButton.IsChecked != true && BytesRadioButton.IsChecked != true && CodeCheckBox.IsChecked != true)
        {
            LogList.Items.Add("请至少选择一种导出格式。");
            return;
        }

        Validate_Click(sender, e);
        if (hasScanErrors || documents.Count == 0 || issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
            return;

        var selected = ResolveSelectedDocuments();
        if (selected.Count == 0)
            return;

        var target = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        if (target == ExportTarget.None)
        {
            LogList.Items.Add("请至少选择客户端或服务器。");
            return;
        }
        var options = new ExportOptions(
            ClientDataOutputPathBox.Text.Trim(),
            ServerDataOutputPathBox.Text.Trim(),
            CodeOutputPathBox.Text.Trim(),
            CodeCheckBox.IsChecked == true,
            JsonRadioButton.IsChecked == true,
            BytesRadioButton.IsChecked == true,
            target);
        try
        {
            var result = new ExportService().ExportAll(selected, options);
            var exportedNames = SelectedFileNames();
            SaveRecentFiles(exportedNames);
            foreach (var item in visibleItems.Where(item => exportedNames.Contains(item.FileName, StringComparer.OrdinalIgnoreCase)))
                item.MarkRecent();
            foreach (var item in visibleItems)
                item.IsSelected = false;
            ApplyListFilter();
            LogList.Items.Clear();
            LogList.Items.Add($"导出完成，schema hash: {result.SchemaHash}");
            foreach (var file in result.Files)
                LogList.Items.Add(file);
        }
        catch (Exception error)
        {
            LogList.Items.Add($"导出错误：{error.Message}");
        }
    }

    private IReadOnlyList<TableDocument> ResolveSelectedDocuments()
    {
        var selectedFiles = SelectedFileNames();
        if (selectedFiles.Count == 0)
        {
            LogList.Items.Add("请至少勾选一个表文件。");
            return Array.Empty<TableDocument>();
        }

        var selectedSources = documents
            .Where(document => selectedFiles.Contains(TableSelectionResolver.GetSourceFileName(document.SourceName), StringComparer.OrdinalIgnoreCase))
            .Select(document => document.SourceName)
            .ToArray();
        var expanded = TableSelectionResolver.Expand(documents, selectedSources);
        var automaticFiles = expanded
            .Select(document => TableSelectionResolver.GetSourceFileName(document.SourceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Except(selectedFiles, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (automaticFiles.Length > 0)
            LogList.Items.Add($"自动加入分表/关联表：{string.Join("、", automaticFiles)}");
        return expanded;
    }

    private IReadOnlyList<string> SelectedFileNames() =>
        visibleItems.Where(item => item.IsSelected).Select(item => item.FileName).ToArray();

    private bool EnsureOutputPaths()
    {
        var source = TablePathBox.Text.Trim();
        var clientData = ClientDataOutputPathBox.Text.Trim();
        var serverData = ServerDataOutputPathBox.Text.Trim();
        var code = CodeOutputPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(source) || (CodeCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(code))
            || (ClientCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(clientData))
            || (ServerCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(serverData)))
        {
            LogList.Items.Add("表根目录、已选端的数据目录和代码目录都必须填写。");
            return false;
        }

        try
        {
            var sourcePath = Path.GetFullPath(source);
            var codePath = CodeCheckBox.IsChecked == true ? Path.GetFullPath(code) : null;
            var dataPaths = new[] { (ClientCheckBox.IsChecked == true ? clientData : null), (ServerCheckBox.IsChecked == true ? serverData : null) }
                .Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Path.GetFullPath(path!)).ToArray();
            if (codePath is not null && dataPaths.Any(path => string.Equals(path, codePath, StringComparison.OrdinalIgnoreCase)))
            {
                LogList.Items.Add("数据目录和代码目录不能相同。");
                return false;
            }
            if (dataPaths.Any(path => IsSameOrAncestor(path, sourcePath)) || (codePath is not null && IsSameOrAncestor(codePath, sourcePath)))
            {
                LogList.Items.Add("输出目录不能等于表根目录或其父目录，以免删除源表。");
                return false;
            }
            if (codePath is not null && dataPaths.Any(path => IsSameOrAncestor(path, codePath) || IsSameOrAncestor(codePath, path)))
            {
                LogList.Items.Add("输出数据目录和输出代码目录不能互相包含。");
                return false;
            }
            return true;
        }
        catch (Exception error)
        {
            LogList.Items.Add($"路径错误：{error.Message}");
            return false;
        }
    }

    private static bool IsSameOrAncestor(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string FindProjectRoot()
    {
        var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "Data")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        return Directory.GetCurrentDirectory();
    }

    private static string RecentFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnityTableTool",
        "recent.txt");

    private static string FavoritesFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnityTableTool",
        "favorites.txt");

    private static string[] LoadRecentFiles()
    {
        try
        {
            return File.Exists(RecentFilePath)
                ? File.ReadAllLines(RecentFilePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void SaveRecentFiles(IEnumerable<string> names)
    {
        try
        {
            var merged = names.Concat(LoadRecentFiles())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(RecentFilePath)!);
            File.WriteAllLines(RecentFilePath, merged);
        }
        catch
        {
        }
    }

    private static string[] LoadFavorites()
    {
        try { return File.Exists(FavoritesFilePath) ? File.ReadAllLines(FavoritesFilePath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray() : Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static void SaveFavorites(IEnumerable<string> names)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(FavoritesFilePath)!); File.WriteAllLines(FavoritesFilePath, names.Distinct(StringComparer.OrdinalIgnoreCase)); }
        catch { }
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TableFileItem item) return;
        item.IsFavorite = !item.IsFavorite;
        SaveFavorites(visibleItems.Where(entry => entry.IsFavorite).Select(entry => entry.FileName));
        ApplyListFilter();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TableFileItem item || !File.Exists(item.SourcePath)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.SourcePath) { UseShellExecute = true }); }
        catch (Exception error) { LogList.Items.Add($"打开文件失败：{error.Message}"); }
    }
}

public sealed class TableFileItem : INotifyPropertyChanged
{
    private bool isSelected;
    private bool isVisible = true;
    private bool isFavorite;

    public TableFileItem(string fileName, IReadOnlyList<string> logicalTables, bool isRecent, string sourcePath, bool isFavorite)
    {
        FileName = fileName;
        LogicalTables = logicalTables;
        IsRecent = isRecent;
        SourcePath = sourcePath;
        IsFavorite = isFavorite;
    }

    public string FileName { get; }
    public IReadOnlyList<string> LogicalTables { get; }
    public bool IsRecent { get; private set; }
    public string SourcePath { get; }
    public bool IsFavorite { get => isFavorite; set { if (isFavorite == value) return; isFavorite = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText))); } }
    public void MarkRecent() { IsRecent = true; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRecent))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText))); }
    public string DisplayText => (IsFavorite ? "★ " : "") + (IsRecent ? "最近 · " : string.Empty) + FileName + "  [" + string.Join(", ", LogicalTables) + "]";
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

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
                return;
            isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
