// 用途：实现配置表搜索、筛选、预览、校验和 JSON/C# 打表交互。
// 最近修改日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
    private IReadOnlyList<string> catalogErrors = Array.Empty<string>();
    private string activeFilter = "all";
    private DateTime lastSyncTime = DateTime.Now;
    private bool isInitializing = true;

    public event EventHandler<string>? StatusChanged;

    public WorkbenchView(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();
        ClientCheckBox.IsChecked = settings.OutputTargets.HasFlag(ExportTarget.Client);
        ServerCheckBox.IsChecked = settings.OutputTargets.HasFlag(ExportTarget.Server);
        Loaded += (_, _) => RefreshTables(selectAll: false);
        ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(UpdateFilterButtons);
        isInitializing = false;
    }

    public void RefreshTables(bool selectAll)
    {
        var selectedKeys = tables.Where(table => table.IsSelected)
            .Select(table => table.FavoriteKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousTables = tables;
        var previousSheets = previousTables.ToDictionary(
            table => table.FileKey,
            table => table.CurrentSheet.SheetName,
            StringComparer.OrdinalIgnoreCase);
        var result = catalogService.Load(settings);
        catalogErrors = result.Errors;
        if (result.Tables.Count == 0 && previousTables.Count > 0)
        {
            StatusChanged?.Invoke(this, result.Errors.Count > 0
                ? string.Join("；", result.Errors)
                : "未找到可读取的配置表，已保留当前列表。");
            catalogErrors = result.Errors;
            UpdateSelectionUi();
            return;
        }
        var loadedKeys = result.Tables
            .Select(table => table.FileKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retainedTables = previousTables
            .Where(table => !loadedKeys.Contains(table.FileKey) && File.Exists(table.SourcePath))
            .ToArray();
        tables = result.Tables.Concat(retainedTables)
            .OrderBy(table => table.FileKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var table in tables)
        {
            table.IsSelected = selectAll || selectedKeys.Contains(table.FavoriteKey);
            if (previousSheets.TryGetValue(table.FileKey, out var sheetName)
                && table.Sheets.FirstOrDefault(sheet => string.Equals(sheet.SheetName, sheetName, StringComparison.OrdinalIgnoreCase)) is { } sheet)
                table.SelectSheet(sheet);
            table.PropertyChanged -= Table_PropertyChanged;
            table.PropertyChanged += Table_PropertyChanged;
        }

        ApplyFilter();
        lastSyncTime = DateTime.Now;
        SyncText.Text = string.Format("已同步 {0}", lastSyncTime.ToString("HH:mm"));
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
        AllFilterText.Text = string.Format("所有 {0}", tables.Count);
        RecentFilterText.Text = string.Format("最近 {0}", tables.Count(table => table.IsRecent));
        FavoritesFilterText.Text = string.Format("收藏 {0}", tables.Count(table => table.IsFavorite));
        UpdateFilterButtons();
    }

    private void UpdateFilterButtons()
    {
        foreach (var (button, tag) in new[] { (FavoritesFilterButton, "favorites"), (RecentFilterButton, "recent"), (AllFilterButton, "all") })
        {
            var active = activeFilter == tag;
            button.SetResourceReference(Control.BackgroundProperty, active ? "Brush.AccentSoft" : "Brush.Window");
            button.SetResourceReference(Control.ForegroundProperty, active ? "Brush.Accent" : "Brush.TextSecondary");
        }
    }

    private void UpdateSelectionUi()
    {
        var selectedCount = tables.Count(table => table.IsSelected);
        SelectionCountText.Text = string.Format("已勾选 {0} / {1}", selectedCount, tables.Count);
        BuildButtonText.Text = "开始打表";
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
        SheetTabs.ItemsSource = table.Sheets;
        ReadOnlyText.Text = string.Format("只读预览 · {0} 个字段", preview.Fields.Count);
        var errorCount = preview.Issues.Count(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal);
        var warningCount = preview.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        ErrorSummaryText.Text = string.Format("{0} 错误", errorCount);
        WarningSummaryText.Text = string.Format("{0} 警告", warningCount);
        IssueRangeText.Text = string.Empty;
        BuildHintText.Text = warningCount > 0
            ? string.Format("{0} 条警告不阻断输出", warningCount)
            : "校验通过，可直接输出";

        BuildPreviewColumns(preview);
        PreviewGrid.ItemsSource = preview.Data.DefaultView;
        IssuesList.ItemsSource = preview.Issues
            .OrderBy(issue => issue.Severity)
            .Select(issue => new IssueDisplayItem(
                ValidationIssueFormatter.Headline(issue),
                ValidationIssueFormatter.Detail(issue),
                issue.Severity))
            .Concat(catalogErrors.Select(error => new IssueDisplayItem(
                "表读取失败",
                error,
                ValidationSeverity.Error)))
            .ToArray();
    }

    private void BuildPreviewColumns(PreviewViewModel preview)
    {
        PreviewGrid.Columns.Clear();
        foreach (var field in preview.Fields)
        {
            var header = new StackPanel
            {
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            header.Children.Add(new TextBlock
            {
                Text = field.Header,
                FontFamily = (FontFamily)FindResource("Font.Mono"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Brush.Text"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });
            header.Children.Add(new TextBlock
            {
                Text = field.TypeLabel,
                FontSize = 9,
                Foreground = (Brush)FindResource("Brush.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

            PreviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"[{field.Name}]"),
                Width = GetColumnWidth(field.Name),
                CanUserSort = false,
                ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters =
                    {
                        new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                        new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center),
                        new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                        new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap)
                    }
                }
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
        SheetTabs.ItemsSource = Array.Empty<TableSheetModel>();
        ReadOnlyText.Text = "只读预览 · 0 个字段";
        PreviewGrid.ItemsSource = null;
        PreviewGrid.Columns.Clear();
        IssuesList.ItemsSource = catalogErrors
            .Select(error => new IssueDisplayItem("表读取失败", error, ValidationSeverity.Error))
            .ToArray();
        ErrorSummaryText.Text = "0 错误";
        WarningSummaryText.Text = "0 警告";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!IsLoaded)
            return;
        ApplyFilter();
        UpdateSelectionUi();
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void WorkbenchView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var scrollViewer = source is null ? null : FindVisualParent<ScrollViewer>(source);
        scrollViewer ??= FindVisualChild<ScrollViewer>(PreviewGrid);
        if (scrollViewer is null)
            return;

        var step = Math.Clamp((double)settings.VerticalWheelScrollStep, 4d, 60d);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - Math.Sign(e.Delta) * Math.Clamp((double)settings.HorizontalWheelScrollStep, 4d, 60d));
        else
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - Math.Sign(e.Delta) * step);

        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T result)
                return result;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
                return result;
            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
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

    private void SheetTab_Click(object sender, RoutedEventArgs e)
    {
        if (TableList.SelectedItem is not TableModel table
            || (sender as FrameworkElement)?.Tag is not TableSheetModel sheet)
            return;

        table.SelectSheet(sheet);
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

    private void RefreshCurrentTable_Click(object sender, RoutedEventArgs e)
    {
        if (TableList.SelectedItem is not TableModel current)
        {
            StatusChanged?.Invoke(this, "请先选择需要刷新的表文件。");
            return;
        }

        var fileKey = current.FileKey;
        var sheetName = current.CurrentSheet.SheetName;
        RefreshTables(selectAll: false);
        var refreshed = tables.FirstOrDefault(table =>
            string.Equals(table.FileKey, fileKey, StringComparison.OrdinalIgnoreCase));
        if (refreshed is null)
            return;

        TableList.SelectedItem = refreshed;
        if (refreshed.Sheets.FirstOrDefault(sheet =>
                string.Equals(sheet.SheetName, sheetName, StringComparison.OrdinalIgnoreCase)) is { } sheet)
            refreshed.SelectSheet(sheet);
        UpdatePreview(refreshed);
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TableModel table)
            return;
        table.IsFavorite = !table.IsFavorite;
        settings.Favorites = tables.Where(item => item.IsFavorite).Select(item => item.FavoriteKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SettingsStore.Save(settings);
        ApplyFilter();
        UpdateSelectionUi();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTables(selectAll: false);

    private void OpenTableDirectory_Click(object sender, RoutedEventArgs e) => OpenPath(settings.TableDirectory);

    private void OutputTarget_Changed(object sender, RoutedEventArgs e)
    {
        // XAML 加载阶段会先触发复选框事件，此时两个命名控件可能尚未完成赋值。
        // 等构造完成后再读取并保存输出范围，避免工作台初始化空引用。
        if (isInitializing || !IsInitialized || ClientCheckBox is null || ServerCheckBox is null)
            return;

        settings.OutputTargets = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        SettingsStore.Save(settings);
        if (IsLoaded)
            UpdateSelectionUi();
    }

    public void BuildTables()
    {
        if (catalogErrors.Count > 0 && tables.Count == 0)
        {
            IssuesList.ItemsSource = catalogErrors
                .Select(error => new IssueDisplayItem("表读取失败", error, ValidationSeverity.Error))
                .ToArray();
            LogsList.ItemsSource = new[]
            {
                new LogDisplayItem(DateTime.Now, "FAIL", "打表失败，请展开日志查看详细信息。")
            };
            SetLogsExpanded(true);
            ShowBuildResultDialog(success: false);
            StatusChanged?.Invoke(this, "存在无法读取的表，已停止打表");
            return;
        }

        var target = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        var selectedSources = tables
            .Where(table => table.IsSelected)
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document.SourceName));
        var result = buildService.Build(settings, tables, selectedSources, target);
        var buildLogs = result.Logs.Select(log => new LogDisplayItem(log.Time, log.Level, log.Message));
        if (catalogErrors.Count > 0)
            buildLogs = new[]
            {
                new LogDisplayItem(DateTime.Now, "WARN", "部分文件读取失败，本次打表使用已保留的内存数据。")
            }.Concat(buildLogs);
        LogsList.ItemsSource = buildLogs.ToArray();
        IssuesList.ItemsSource = result.Issues
            .OrderBy(issue => issue.Severity)
            .Select(issue => new IssueDisplayItem(
                ValidationIssueFormatter.Headline(issue),
                ValidationIssueFormatter.Detail(issue),
                issue.Severity))
            .Concat(catalogErrors.Select(error => new IssueDisplayItem(
                "表读取失败",
                error,
                ValidationSeverity.Warning)))
            .ToArray();

        if (result.Success)
        {
            settings.RecentTables = tables
                .Where(table => table.IsSelected)
                .Select(table => table.FavoriteKey)
                .Concat(settings.RecentTables)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
            SettingsStore.Save(settings);
            StatusChanged?.Invoke(this, string.Format("打表完成：{0} 个输出文件", result.FileCount));
            foreach (var table in tables)
                table.IsSelected = false;
            UpdateSelectionUi();
            ShowBuildResultDialog(success: true);
        }
        else
        {
            SetLogsExpanded(true);
            ShowBuildResultDialog(success: false);
            StatusChanged?.Invoke(this, "打表未完成，请检查日志");
        }
    }

    private void Build_Click(object sender, RoutedEventArgs e) => BuildTables();

    private void ShowBuildResultDialog(bool success)
    {
        var dialog = new BuildResultDialog(success)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var items = LogsList.ItemsSource as IEnumerable<LogDisplayItem> ?? [];
        var text = string.Join(Environment.NewLine, items.Select(item => $"{item.TimeText} {item.Level} {item.Message}"));
        if (text.Length > 0)
            Clipboard.SetText(text);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => LogsList.ItemsSource = Array.Empty<LogDisplayItem>();

    private void ToggleLogs_Click(object sender, RoutedEventArgs e)
    {
        var collapsed = LogsScrollViewer.Visibility == Visibility.Collapsed;
        SetLogsExpanded(collapsed);
    }

    private void SetLogsExpanded(bool expanded)
    {
        LogsScrollViewer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        LogsRow.Height = expanded ? new GridLength(170) : new GridLength(38);
        ToggleLogsButton.Content = expanded ? "收起" : "展开";
    }

    private static void OpenPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            path = Path.GetFullPath(path);
            if (!File.Exists(path) && !Directory.Exists(path))
                return;
            if (string.IsNullOrWhiteSpace(Path.GetExtension(path)))
                Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
