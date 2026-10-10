// 用途：实现配置表搜索、筛选、预览、校验和 JSON/C# 打表交互。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.ComponentModel;
using TableTool.Core.Validation;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private IReadOnlyList<string> catalogInfos = Array.Empty<string>();
    private IReadOnlyList<ValidationIssue> batchIssues = Array.Empty<ValidationIssue>();
    private string lastValidationSignature = string.Empty;
    private string activeFilter = "all";
    private DateTime lastSyncTime = DateTime.Now;
    private bool isInitializing = true;
    private PreviewViewModel? currentPreview;
    private bool isRefreshingPreview;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler? OpenLogsRequested;

    public WorkbenchView(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();
        ClientCheckBox.IsChecked = settings.OutputTargets.HasFlag(ExportTarget.Client);
        ServerCheckBox.IsChecked = settings.OutputTargets.HasFlag(ExportTarget.Server);
        ClientCodeCheckBox.IsChecked = settings.CodeTargets.HasFlag(ExportTarget.Client);
        ServerCodeCheckBox.IsChecked = settings.CodeTargets.HasFlag(ExportTarget.Server);
        Loaded += (_, _) => RefreshTables(selectAll: false);
        ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(RefreshTheme);
        PreviewGrid.SelectedCellsChanged += (_, _) => ApplyPreviewSelectionVisuals();
        ApplyLayoutSizes();
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
        catalogInfos = result.Infos;
        foreach (var info in catalogInfos)
            ToolLogService.Info("配置表", info);
        if (result.Tables.Count == 0 && previousTables.Count > 0)
        {
            StatusChanged?.Invoke(this, result.Errors.Count > 0
                ? string.Join("；", result.Errors)
                : "未找到可读取的配置表，已保留当前列表。");
            if (result.Errors.Count > 0)
                ToolLogService.Warning("配置表", "刷新表列表未找到可读取文件，已保留当前列表。");
            else
                ToolLogService.Info("配置表", "当前目录只有非正式表内容，已保留当前正式表列表。");
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
        SyncText.Text = string.Format("已同步 {0}", lastSyncTime.ToString("MM-dd HH:mm:ss"));
        if (result.Errors.Count == 0)
            ToolLogService.Success("配置表", $"刷新表列表：读取 {result.Tables.Count} 个文件。");
        else
            ToolLogService.Warning("配置表", $"刷新表列表：读取 {result.Tables.Count} 个文件，{result.Errors.Count} 个错误。");
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
        BuildButton.IsEnabled = selectedCount > 0
            && (ClientCheckBox.IsChecked == true || ServerCheckBox.IsChecked == true
                || ClientCodeCheckBox.IsChecked == true || ServerCodeCheckBox.IsChecked == true);

        var allVisibleSelected = visibleTables.Count > 0 && visibleTables.All(table => table.IsSelected);
        var anyVisibleSelected = visibleTables.Any(table => table.IsSelected);
        SelectAllCheckBox.IsChecked = allVisibleSelected ? true : anyVisibleSelected ? null : false;

        if (TableList.SelectedItem is TableModel selected)
            UpdatePreview(selected);
    }

    private void UpdatePreview(TableModel table)
    {
        var allDocuments = tables.SelectMany(item => item.Sheets.Select(sheet => sheet.Document)).ToArray();
        var selectedSourceNames = tables
            .Where(item => item.IsSelected)
            .SelectMany(item => item.Sheets.Select(sheet => sheet.Document.SourceName));
        var validationSources = selectedSourceNames
            .Append(table.CurrentSheet.Document.SourceName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        batchIssues = TableBatchValidator.Validate(allDocuments, validationSources, GetArraySeparators()).Issues;
        var preview = previewService.Create(table, batchIssues, GetArraySeparators());
        currentPreview = preview;
        var displayIssues = batchIssues
            .Concat(preview.Issues)
            .Distinct()
            .ToArray();
        PreviewTitleText.Text = preview.Title;
        ModifiedText.Text = preview.ModifiedText;
        SourceText.Text = preview.SourceText;
        SheetTabs.ItemsSource = table.Sheets;
        ReadOnlyText.Text = string.Format("只读预览 · {0} 行 · {1} 列", preview.Rows.Count, preview.Fields.Count);
        var errorCount = displayIssues.Count(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal)
            + catalogErrors.Count;
        var warningCount = displayIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        ErrorSummaryText.Text = string.Format("{0} 错误", errorCount);
        WarningSummaryText.Text = string.Format("{0} 警告", warningCount);
        var validationSignature = string.Join("|", validationSources.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            + $"::{errorCount}:{warningCount}";
        if (!string.Equals(lastValidationSignature, validationSignature, StringComparison.Ordinal))
        {
            lastValidationSignature = validationSignature;
            ToolLogService.Info("校验", $"检查 {displayIssues.Length} 条问题：{errorCount} 错误，{warningCount} 警告。");
        }
        UpdateSheetValidationStates(table, preview.Issues);

        BuildPreviewColumns(preview);
        PreviewGrid.ItemsSource = preview.Rows;
        IssuesList.ItemsSource = displayIssues
            .Select(IssueDisplayItem.FromIssue)
            .Concat(catalogErrors.Select(error => IssueDisplayItem.CatalogError(error)))
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
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = GetHighlightBrush(field.HighlightLevel),
                ToolTip = string.IsNullOrWhiteSpace(field.HighlightTooltip) ? null : field.HighlightTooltip
            };
            header.Children.Add(new TextBlock
            {
                Text = field.Header,
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

            var cellStyle = new Style(typeof(DataGridCell))
            {
                BasedOn = (Style)FindResource(typeof(DataGridCell))
            };
            cellStyle.Setters.Add(new Setter(ToolTipProperty, new Binding($"CellTooltips[{field.Key}]")));
            var errorTrigger = new DataTrigger
            {
                Binding = new Binding($"CellLevels[{field.Key}]"),
                Value = "Error"
            };
            errorTrigger.Setters.Add(new Setter(BackgroundProperty, FindResource("Brush.ErrorSoft")));
            errorTrigger.Setters.Add(new Setter(ForegroundProperty, FindResource("Brush.Error")));
            cellStyle.Triggers.Add(errorTrigger);
            var warningTrigger = new DataTrigger
            {
                Binding = new Binding($"CellLevels[{field.Key}]"),
                Value = "Warning"
            };
            warningTrigger.Setters.Add(new Setter(BackgroundProperty, FindResource("Brush.WarningSoft")));
            warningTrigger.Setters.Add(new Setter(ForegroundProperty, FindResource("Brush.Warning")));
            cellStyle.Triggers.Add(warningTrigger);

            PreviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"[{field.Key}]"),
                Width = GetColumnWidth(field.Name),
                CanUserSort = false,
                CellStyle = cellStyle,
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

    private void RefreshTheme()
    {
        UpdateFilterButtons();
        if (currentPreview is null)
            return;

        var selection = CapturePreviewSelection();
        isRefreshingPreview = true;
        BuildPreviewColumns(currentPreview);
        PreviewGrid.ItemsSource = currentPreview.Rows;
        isRefreshingPreview = false;
        RestorePreviewSelection(selection);
    }

    private List<(int SourceRow, string Key)> CapturePreviewSelection() =>
        PreviewGrid.SelectedCells
            .Select(cell => (SourceRow: (cell.Item as PreviewRow)?.SourceRow ?? -1, Key: GetColumnKey(cell.Column) ?? string.Empty))
            .Where(item => item.SourceRow >= 0 && item.Key.Length > 0)
            .Distinct()
            .ToList();

    private void RestorePreviewSelection(IReadOnlyList<(int SourceRow, string Key)> selection)
    {
        foreach (var (sourceRow, key) in selection)
        {
            var row = PreviewGrid.Items.OfType<PreviewRow>().FirstOrDefault(item => item.SourceRow == sourceRow);
            var column = PreviewGrid.Columns.FirstOrDefault(item => GetColumnKey(item) == key);
            if (row is not null && column is not null)
                PreviewGrid.SelectedCells.Add(new DataGridCellInfo(row, column));
        }
        ApplyPreviewSelectionVisuals();
    }

    private void ApplyPreviewSelectionVisuals()
    {
        if (isRefreshingPreview)
            return;

        var selectedRows = PreviewGrid.SelectedCells.Select(cell => PreviewGrid.Items.IndexOf(cell.Item)).ToHashSet();
        var selectedColumns = PreviewGrid.SelectedCells.Select(cell => cell.Column.DisplayIndex).ToHashSet();
        foreach (var cell in FindVisualDescendants<DataGridCell>(PreviewGrid))
        {
            var rowIndex = PreviewGrid.Items.IndexOf(cell.DataContext);
            if (cell.IsSelected)
            {
                cell.SetResourceReference(BackgroundProperty, "Brush.SelectionCell");
                cell.SetResourceReference(ForegroundProperty, "Brush.Text");
            }
            else if (selectedRows.Contains(rowIndex) || selectedColumns.Contains(cell.Column.DisplayIndex))
            {
                cell.SetResourceReference(BackgroundProperty, "Brush.SelectionCross");
                cell.SetResourceReference(ForegroundProperty, "Brush.TextSecondary");
            }
            else
            {
                cell.ClearValue(BackgroundProperty);
                cell.ClearValue(ForegroundProperty);
            }
        }
    }

    private void WorkbenchView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && PreviewGrid.IsKeyboardFocusWithin)
        {
            CopyPreviewSelection();
            e.Handled = true;
        }
    }

    private void CopyPreviewSelection()
    {
        if (PreviewGrid.SelectedCells.Count == 0)
            return;

        var cells = PreviewGrid.SelectedCells.Select(cell => new
        {
            Row = PreviewGrid.Items.IndexOf(cell.Item),
            Column = cell.Column.DisplayIndex,
            Key = GetColumnKey(cell.Column) ?? string.Empty,
            RowData = cell.Item as PreviewRow
        }).Where(cell => cell.RowData is not null && cell.Key.Length > 0).ToArray();
        if (cells.Length == 0)
            return;

        var minRow = cells.Min(cell => cell.Row);
        var maxRow = cells.Max(cell => cell.Row);
        var minColumn = cells.Min(cell => cell.Column);
        var maxColumn = cells.Max(cell => cell.Column);
        var keys = PreviewGrid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Select(column => GetColumnKey(column) ?? string.Empty)
            .ToArray();
        var lines = new List<string>();
        for (var rowIndex = minRow; rowIndex <= maxRow; rowIndex++)
        {
            var row = PreviewGrid.Items[rowIndex] as PreviewRow;
            var values = new List<string>();
            for (var columnIndex = minColumn; columnIndex <= maxColumn; columnIndex++)
                values.Add(columnIndex < keys.Length ? row?[keys[columnIndex]] ?? string.Empty : string.Empty);
            lines.Add(string.Join('\t', values));
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
        catch (Exception error)
        {
            ToolLogService.Error("工作台", $"复制表格内容失败：{error.Message}");
        }
    }

    private static string? GetColumnKey(DataGridColumn column)
    {
        if ((column.Header as FrameworkElement)?.Tag is string tag)
            return tag;
        return (column as DataGridBoundColumn)?.Binding is Binding binding ? binding.Path?.Path?.Trim('[', ']') : null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }

    private void ApplyLayoutSizes()
    {
        TableListColumn.Width = new GridLength(Math.Clamp(settings.WorkbenchTableListWidth, 180, 360));
        TableDetailRow.Height = new GridLength(Math.Clamp(settings.WorkbenchDetailHeight, 120, 800), GridUnitType.Star);
        TableIssuesRow.Height = new GridLength(Math.Clamp(settings.WorkbenchIssuesHeight, 120, 800), GridUnitType.Star);
    }

    private void LayoutSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        settings.WorkbenchTableListWidth = TableListColumn.Width.IsAbsolute ? TableListColumn.Width.Value : 215;
        settings.WorkbenchDetailHeight = Math.Max(1, TableDetailRow.Height.Value);
        settings.WorkbenchIssuesHeight = Math.Max(1, TableIssuesRow.Height.Value);
        SettingsStore.Save(settings);
    }

    private void UpdateSheetValidationStates(TableModel table, IReadOnlyList<ValidationIssue> previewIssues)
    {
        foreach (var sheet in table.Sheets)
        {
            var issues = (sheet.Document.ParseIssues ?? [])
                .Concat(new TableValidator(GetArraySeparators()).Validate(sheet.Document))
                .Concat(batchIssues.Where(issue => string.Equals(issue.SourceName, sheet.Document.SourceName, StringComparison.OrdinalIgnoreCase)))
                .Concat(sheet == table.CurrentSheet ? previewIssues : [])
                .Distinct()
                .ToArray();
            var level = ValidationHighlightResolver.GetHighestLevel(issues);
            var (levelName, marker) = level switch
            {
                ValidationHighlightLevel.Error => ("Error", "错"),
                ValidationHighlightLevel.Warning => ("Warning", "警"),
                _ => ("None", string.Empty)
            };
            var highestIssue = issues.FirstOrDefault(issue => level == ValidationHighlightLevel.Error
                ? issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal
                : issue.Severity == ValidationSeverity.Warning);
            var tooltip = highestIssue is null ? string.Empty : ValidationIssueFormatter.Detail(highestIssue);
            sheet.SetValidationState(levelName, marker, tooltip);
        }
    }

    private Brush GetHighlightBrush(string level) => level switch
    {
        "Error" => (Brush)FindResource("Brush.ErrorSoft"),
        "Warning" => (Brush)FindResource("Brush.WarningSoft"),
        _ => Brushes.Transparent
    };

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
        currentPreview = null;
        PreviewTitleText.Text = "配置表预览";
        ModifiedText.Text = string.Empty;
        SourceText.Text = string.Empty;
        SheetTabs.ItemsSource = Array.Empty<TableSheetModel>();
        ReadOnlyText.Text = "只读预览 · 0 个字段";
        PreviewGrid.ItemsSource = null;
        PreviewGrid.Columns.Clear();
        IssuesList.ItemsSource = catalogErrors
            .Select(IssueDisplayItem.CatalogError)
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

    private void WorkbenchView_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollWheelService.Handle(e, e.OriginalSource as DependencyObject, settings, PreviewGrid);

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
        var favoriteAction = table.IsFavorite ? "收藏" : "取消收藏";
        ToolLogService.Info("工作台", $"{favoriteAction}：{table.DisplayName}");
        ApplyFilter();
        UpdateSelectionUi();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTables(selectAll: false);

    private void OpenTableDirectory_Click(object sender, RoutedEventArgs e) => OpenPath(settings.TableDirectory);

    private void OutputTarget_Changed(object sender, RoutedEventArgs e)
    {
        // XAML 加载阶段会先触发复选框事件，此时命名控件可能尚未完成赋值。
        // 等构造完成后再读取并保存输出范围，避免工作台初始化空引用。
        if (isInitializing || !IsInitialized
            || ClientCheckBox is null || ServerCheckBox is null
            || ClientCodeCheckBox is null || ServerCodeCheckBox is null)
            return;

        settings.OutputTargets = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        settings.CodeTargets = (ClientCodeCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCodeCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        SettingsStore.Save(settings);
        ToolLogService.Info("工作台", "输出范围已更新。");
        if (IsLoaded)
            UpdateSelectionUi();
    }

    public void BuildTables()
    {
        if (TableList.SelectedItem is TableModel selected)
            UpdatePreview(selected);
        else
        {
            var allDocuments = tables.SelectMany(item => item.Sheets.Select(sheet => sheet.Document)).ToArray();
            var selectedSourceNames = tables
                .Where(item => item.IsSelected)
                .SelectMany(item => item.Sheets.Select(sheet => sheet.Document.SourceName));
            batchIssues = TableBatchValidator.Validate(allDocuments, selectedSourceNames, GetArraySeparators()).Issues;
        }

        if (catalogErrors.Count > 0 && tables.Count == 0)
        {
            IssuesList.ItemsSource = catalogErrors
                .Select(IssueDisplayItem.CatalogError)
                .ToArray();
            ToolLogService.Error("打表", "存在无法读取的表，已停止打表。");
            OpenLogsRequested?.Invoke(this, EventArgs.Empty);
            ShowBuildResultDialog(success: false);
            StatusChanged?.Invoke(this, "存在无法读取的表，已停止打表");
            return;
        }

        var dataTarget = (ClientCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        var codeTarget = (ClientCodeCheckBox.IsChecked == true ? ExportTarget.Client : ExportTarget.None)
            | (ServerCodeCheckBox.IsChecked == true ? ExportTarget.Server : ExportTarget.None);
        var selectedSources = tables
            .Where(table => table.IsSelected)
            .SelectMany(table => table.Sheets.Select(sheet => sheet.Document.SourceName));
        var result = buildService.Build(settings, tables, selectedSources, dataTarget, codeTarget);
        foreach (var log in result.Logs)
            ToolLogService.Add(NormalizeBuildLevel(log.Level), "打表", log.Message);
        if (catalogErrors.Count > 0)
            ToolLogService.Warning("配置表", "部分文件读取失败，本次打表使用已保留的内存数据。");
        IssuesList.ItemsSource = batchIssues
            .Concat(result.Issues)
            .Distinct()
            .OrderBy(issue => issue.Severity)
            .Select(IssueDisplayItem.FromIssue)
            .Concat(catalogErrors.Select(error => IssueDisplayItem.CatalogError(error)))
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
            OpenLogsRequested?.Invoke(this, EventArgs.Empty);
            ShowBuildResultDialog(success: false);
            StatusChanged?.Invoke(this, "打表未完成，请检查日志");
        }
    }

    private void Build_Click(object sender, RoutedEventArgs e) => BuildTables();

    private ArraySeparatorOptions GetArraySeparators() => new(
        settings.ArrayInnerSeparator,
        settings.ArrayMiddleSeparator,
        settings.ArrayOuterSeparator);

    private void ShowBuildResultDialog(bool success)
    {
        var dialog = new BuildResultDialog(success)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void CopyIssues_Click(object sender, RoutedEventArgs e)
    {
        var items = IssuesList.ItemsSource as IEnumerable<IssueDisplayItem> ?? [];
        var text = string.Join(Environment.NewLine, items.Select(item => item.CopyText));
        if (text.Length > 0)
            Clipboard.SetText(text);
    }

    private static string NormalizeBuildLevel(string level) => level switch
    {
        "PASS" or "DONE" => "SUCCESS",
        "WARN" => "WARNING",
        "FAIL" => "ERROR",
        _ => "INFO"
    };

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
            ToolLogService.Info("系统", $"打开：{path}");
        }
        catch (Exception error)
        {
            ToolLogService.Error("系统", $"打开失败：{path}：{error.Message}");
        }
    }
}
