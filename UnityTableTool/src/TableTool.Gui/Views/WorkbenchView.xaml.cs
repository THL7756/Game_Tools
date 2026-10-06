// 用途：实现配置表搜索、筛选、预览、校验和纯 JSON 打表交互。
// 最近修改日期：2026-10-06

using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TableTool.Core.Models;
using TableTool.Gui.Models;
using TableTool.Gui.Services;

namespace TableTool.Gui.Views;

public partial class WorkbenchView : System.Windows.Controls.UserControl
{
    private readonly AppSettings settings;
    private readonly TableCatalogService catalogService = new();
    private readonly PreviewService previewService = new();
    private readonly BuildService buildService = new();
    private IReadOnlyList<TableModel> tables = Array.Empty<TableModel>();
    private IReadOnlyList<TableModel> visibleTables = Array.Empty<TableModel>();
    private string activeFilter = "all";

    public event EventHandler<string>? StatusChanged;

    public WorkbenchView(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        Loaded += (_, _) => RefreshTables(selectAll: true);
    }

    public void RefreshTables(bool selectAll)
    {
        var selectedNames = tables.Where(table => table.IsSelected).Select(table => table.SchemaName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = catalogService.Load(settings);
        tables = result.Tables;
        foreach (var table in tables)
        {
            table.IsSelected = selectAll || selectedNames.Contains(table.SchemaName);
            table.PropertyChanged += Table_PropertyChanged;
        }

        ApplyFilter();
        SyncText.Text = $"已同步 {DateTime.Now:HH:mm}";
        if (result.Errors.Count > 0)
            StatusChanged?.Invoke(this, string.Join("；", result.Errors));
        else
            StatusChanged?.Invoke(this, settings.ProjectRootDirectory.Replace('\\', '/'));

        if (visibleTables.Count > 0)
        {
            var current = visibleTables.FirstOrDefault(table => table.IsSelected) ?? visibleTables[0];
            TableList.SelectedItem = current;
            UpdatePreview(current);
        }
        else
        {
            ClearPreview();
        }

        UpdateSelectionUi();
    }

    public void SelectAll()
    {
        foreach (var table in visibleTables)
            table.IsSelected = true;
        UpdateSelectionUi();
    }

    public void ClearSelection()
    {
        foreach (var table in tables)
            table.IsSelected = false;
        UpdateSelectionUi();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        visibleTables = tables
            .Where(table => activeFilter switch
            {
                "favorites" => table.IsFavorite,
                "recent" => table.IsRecent,
                _ => true
            })
            .Where(table => query.Length == 0
                || table.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || table.SchemaName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(table.SourcePath).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        TableList.ItemsSource = visibleTables;
        FavoritesFilterText.Text = $"收藏表 {tables.Count(table => table.IsFavorite)}";
        RecentFilterText.Text = $"最近表 {tables.Count(table => table.IsRecent)}";
        AllFilterText.Text = $"所有表 {tables.Count}";
        UpdateFilterButtons();
    }

    private void UpdateFilterButtons()
    {
        foreach (var (button, tag) in new[] { (FavoritesFilterButton, "favorites"), (RecentFilterButton, "recent"), (AllFilterButton, "all") })
        {
            var active = activeFilter == tag;
            button.Background = (Brush)FindResource(active ? "Brush.AccentSoft" : "Brush.Window");
            button.Foreground = (Brush)FindResource(active ? "Brush.Accent" : "Brush.TextSecondary");
        }
    }

    private void UpdateSelectionUi()
    {
        var selectedCount = tables.Count(table => table.IsSelected);
        SelectionCountText.Text = $"已勾选 {selectedCount} / {tables.Count}";
        BuildSelectionText.Text = $"已勾选 {selectedCount} 张表";
        BuildButtonText.Text = $"打表 ({selectedCount})";
        BuildButton.IsEnabled = selectedCount > 0 && (ClientCheckBox.IsChecked == true || ServerCheckBox.IsChecked == true);

        var allVisibleSelected = visibleTables.Count > 0 && visibleTables.All(table => table.IsSelected);
        var anyVisibleSelected = visibleTables.Any(table => table.IsSelected);
        SelectAllCheckBox.IsChecked = allVisibleSelected ? true : anyVisibleSelected ? null : false;

        if (TableList.SelectedItem is TableModel selected)
            UpdatePreview(selected);
    }

    private void UpdatePreview(TableModel table)
    {
        var preview = previewService.Create(table, tables);
        PreviewTitleText.Text = preview.Title;
        ModifiedText.Text = preview.ModifiedText;
        SourceText.Text = preview.SourceText;
        SheetTabs.ItemsSource = preview.SheetNames;
        ReadOnlyText.Text = $"只读预览 · {preview.Fields.Count} 个字段";
        IssueSummaryText.Text = preview.IssueSummary;
        IssueRangeText.Text = $"范围：已勾选的 {tables.Count(item => item.IsSelected)} 张表";
        var warningCount = preview.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        BuildHintText.Text = warningCount > 0 ? $"{warningCount} 条警告不阻断输出" : "校验通过，可直接输出";

        BuildPreviewColumns(preview);
        PreviewGrid.ItemsSource = preview.Data.DefaultView;
        IssuesList.ItemsSource = preview.Issues
            .OrderBy(issue => issue.Severity)
            .Select(issue => new IssueDisplayItem(
                $"{issue.SourceName}:{issue.SourceRow} · {issue.Code}",
                issue.Suggestion is null ? issue.Message : $"{issue.Message} {issue.Suggestion}"))
            .ToArray();
    }

    private void BuildPreviewColumns(PreviewViewModel preview)
    {
        PreviewGrid.Columns.Clear();
        foreach (var field in preview.Fields)
        {
            var header = new StackPanel { Margin = new Thickness(0, 1, 0, 1) };
            header.Children.Add(new TextBlock
            {
                Text = field.Header,
                FontFamily = (FontFamily)FindResource("Font.Mono"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Brush.Text")
            });
            header.Children.Add(new TextBlock
            {
                Text = field.TypeLabel,
                FontSize = 9,
                Foreground = (Brush)FindResource("Brush.TextMuted")
            });

            PreviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"[{field.Name}]"),
                Width = GetColumnWidth(field.Name),
                CanUserSort = false
            });
        }
    }

    private static DataGridLength GetColumnWidth(string fieldName) => fieldName switch
    {
        "id" => 72,
        "name" => 115,
        "quality" => 74,
        "hp" => 70,
        "attack" => 72,
        "skill_id" => 86,
        "description" or "desc" => 250,
        _ => Math.Clamp(72 + fieldName.Length * 8, 82, 180)
    };

    private void ClearPreview()
    {
        PreviewTitleText.Text = "配置表预览";
        ModifiedText.Text = string.Empty;
        SourceText.Text = string.Empty;
        SheetTabs.ItemsSource = Array.Empty<string>();
        ReadOnlyText.Text = "只读预览 · 0 个字段";
        PreviewGrid.ItemsSource = null;
        PreviewGrid.Columns.Clear();
        IssuesList.ItemsSource = Array.Empty<IssueDisplayItem>();
        IssueSummaryText.Text = "0 错误 / 0 警告";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!IsLoaded)
            return;
        ApplyFilter();
        UpdateSelectionUi();
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        activeFilter = element.Tag?.ToString() ?? "all";
        ApplyFilter();
        UpdateSelectionUi();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectAllCheckBox.IsChecked == true;
        foreach (var table in visibleTables)
            table.IsSelected = selected;
        UpdateSelectionUi();
    }

    private void TableCheck_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateSelectionUi);
    }

    private void Table_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableModel.IsSelected))
            Dispatcher.BeginInvoke(UpdateSelectionUi);
    }

    private void TableList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TableList.SelectedItem is TableModel table)
            UpdatePreview(table);
    }

    private void OpenTable_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TableModel table)
            OpenPath(table.SourcePath);
    }

    private void OpenInExcel_Click(object sender, RoutedEventArgs e)
    {
        if (TableList.SelectedItem is TableModel table)
            OpenPath(table.SourcePath);
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TableModel table)
            return;
        table.IsFavorite = !table.IsFavorite;
        settings.Favorites = tables.Where(item => item.IsFavorite).Select(item => item.SchemaName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SettingsStore.Save(settings);
        ApplyFilter();
        UpdateSelectionUi();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTables(selectAll: false);

    private void OpenTableDirectory_Click(object sender, RoutedEventArgs e) => OpenPath(settings.TableDirectory);

    private void OutputTarget_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            UpdateSelectionUi();
    }

    private void Build_Click(object sender, RoutedEventArgs e)
    {
        var target = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        var result = buildService.Build(settings, tables, tables.Where(table => table.IsSelected).Select(table => table.Document.SourceName), target);
        LogsList.ItemsSource = result.Logs.Select(log => new LogDisplayItem(log.Time, log.Level, log.Message)).ToArray();
        IssuesList.ItemsSource = result.Issues
            .OrderBy(issue => issue.Severity)
            .Select(issue => new IssueDisplayItem(
                $"{issue.SourceName}:{issue.SourceRow} · {issue.Code}",
                issue.Suggestion is null ? issue.Message : $"{issue.Message} {issue.Suggestion}"))
            .ToArray();

        if (result.Success)
        {
            settings.RecentTables = tables
                .Where(table => table.IsSelected)
                .Select(table => table.SchemaName)
                .Concat(settings.RecentTables)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
            SettingsStore.Save(settings);
            StatusChanged?.Invoke(this, $"打表完成：{result.FileCount} 个 JSON 文件");
        }
        else
        {
            StatusChanged?.Invoke(this, "打表未完成，请检查日志");
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var items = LogsList.ItemsSource as IEnumerable<LogDisplayItem> ?? [];
        var text = string.Join(Environment.NewLine, items.Select(item => $"{item.TimeText} {item.Level} {item.Message}"));
        if (text.Length > 0)
            Clipboard.SetText(text);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => LogsList.ItemsSource = Array.Empty<LogDisplayItem>();

    private static void OpenPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
                Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
