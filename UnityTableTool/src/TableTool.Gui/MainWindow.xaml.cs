// 用途：承载工具主窗口、模块切换、设置恢复和关闭保存逻辑。
// 编写日期：2026-10-08
// 最近修改日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TableTool.Gui.Models;
using TableTool.Gui.Services;
using TableTool.Gui.Views;

namespace TableTool.Gui;

public partial class MainWindow : Window
{
    private readonly AppSettings settings;
    private WorkbenchView? workbenchView;
private bool isInitializing = true;
private bool isApplyingLanguage;
    private bool isSwitchingContent;
    private readonly HashSet<string> activeLogLevels = new(ToolLogCategories.Levels, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> activeLogCategories = new(ToolLogCategories.All, StringComparer.Ordinal);
    private readonly HashSet<string> collapsedLogCategories = new(StringComparer.Ordinal);
    private bool isLogAtBottom = true;
    private string logSearchText = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        settings = SettingsStore.Load();
        ApplyLayoutSizes();
        activeLogLevels.Clear();
        activeLogLevels.UnionWith(settings.LogFilterLevels);
        activeLogCategories.Clear();
        activeLogCategories.UnionWith(settings.LogFilterCategories);
        collapsedLogCategories.UnionWith(settings.LogCollapsedCategories);
        LogCategoryFilters.ItemsSource = ToolLogCategories.All
            .Select(category => new LogCategoryFilterItem(category, 0, activeLogCategories.Contains(category)))
            .ToArray();
        UpdateLogCollapseDuplicatesButton();
        UpdateLogCategoryButtonState();
        LogOverlayPanel.Height = settings.LogPanelHeight;
        ToolLogService.EntryAdded += ToolLogService_EntryAdded;
        ToolLogService.Cleared += ToolLogService_Cleared;
        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        Closing += MainWindow_Closing;
        ThemeManager.Apply(settings);
LoadLanguages();
UpdateThemeButtons();
        ApplyZoom();
        isInitializing = false;

        var screen = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(argument => argument.StartsWith("--screen=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
        if (string.Equals(screen, "settings-paths", StringComparison.OrdinalIgnoreCase))
            ShowSettings("paths");
        else if (string.Equals(screen, "settings-appearance", StringComparison.OrdinalIgnoreCase))
            ShowSettings("appearance");
        else
ShowWorkbench();
ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(() =>
{
    UpdateThemeButtons();
    ApplyZoom();
});
        ApplyLanguageSafely();
        ReloadLogs();
        UpdateLogFilterButtons();

    }

    private void ShowWorkbench()
    {
        if (isSwitchingContent)
            return;

        try
        {
            isSwitchingContent = true;
            if (workbenchView is null)
            {
                workbenchView = new WorkbenchView(settings);
                workbenchView.OpenLogsRequested += (_, _) => Dispatcher.BeginInvoke(() => SetLogsExpanded(true));
                workbenchView.StatusChanged += (_, text) => StatusPathText.Text = text;
            }

            MainContent.Content = workbenchView;
            ConfigNav.IsChecked = true;
            workbenchView.RefreshTables(selectAll: false);
            StatusPathText.Text = settings.ProjectRootDirectory.Replace('\\', '/');
        }
        catch (Exception error)
        {
            App.LogUiError("切换到配置工具失败", error);
        }
        finally
        {
            isSwitchingContent = false;
        }
    }

    private void ShowSettings(string page)
    {
        if (isSwitchingContent)
            return;

        try
        {
            isSwitchingContent = true;
            var view = new SettingsView(settings, page);
            view.SettingsSaved += (_, _) =>
            {
                SettingsStore.Save(settings);
                ThemeManager.Apply(settings);
                ApplyZoom();
                workbenchView?.RefreshTables(selectAll: false);
            };
            MainContent.Content = view;
            ConfigNav.IsChecked = false;
            StatusPathText.Text = settings.ProjectRootDirectory.Replace('\\', '/');
        }
        catch (Exception error)
        {
            App.LogUiError("打开设置失败", error);
        }
        finally
        {
            isSwitchingContent = false;
        }
    }

private void ApplyLanguageSafely()
{
if (isApplyingLanguage)
return;

try
{
isApplyingLanguage = true;
LanguageManager.Apply(this, settings.Language);
if (MainContent.Content is DependencyObject content)
LanguageManager.Apply(content, settings.Language);
}
catch (Exception error)
{
App.LogUiError("应用界面语言失败", error);
}
finally
{
isApplyingLanguage = false;
}
}

private void ApplyZoom()
    {
        if (RootLayout is null)
            return;
        var zoom = settings.Zoom / 100d;
        RootLayout.LayoutTransform = Math.Abs(zoom - 1) < 0.001
            ? null
            : new ScaleTransform(zoom, zoom);
    }



private void UpdateThemeButtons()
{
switch (settings.AppearanceMode)
{
case AppearanceMode.Light:
LightThemeButton.IsChecked = true;
break;
case AppearanceMode.System:
SystemThemeButton.IsChecked = true;
break;
default:
DarkThemeButton.IsChecked = true;
break;
}
}

private void ThemeMode_Checked(object sender, RoutedEventArgs e)
{
if (isInitializing || settings is null || sender is not FrameworkElement element)
return;
ApplyThemeTag(element.Tag?.ToString());
}

private void ThemeMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
ApplyThemeTag(element.Tag?.ToString());
UpdateThemeButtons();
    }

    private void ApplyThemeTag(string? tag)
    {
        settings.AppearanceMode = tag switch
        {
            "light" => AppearanceMode.Light,
            "system" => AppearanceMode.System,
            _ => AppearanceMode.Dark
        };
        ThemeManager.Apply(settings);
        SettingsStore.Save(settings);
        ToolLogService.Info("界面", $"页面模式已切换：{settings.AppearanceMode}");
    }


private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings("appearance");

private void MenuAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        switch (element.Tag?.ToString())
        {
            case "open-tables":
                OpenDirectory(settings.TableDirectory);
                break;
            case "refresh":
                workbenchView?.RefreshTables(selectAll: false);
                break;
            case "select-all":
                workbenchView?.SelectAll();
                break;
            case "clear-selection":
                workbenchView?.ClearSelection();
                break;
            case "undo":
                ExecuteFocusedCommand(ApplicationCommands.Undo);
                break;
            case "redo":
                ExecuteFocusedCommand(ApplicationCommands.Redo);
                break;
            case "copy":
                ExecuteFocusedCommand(ApplicationCommands.Copy);
                break;
            case "cut":
                ExecuteFocusedCommand(ApplicationCommands.Cut);
                break;
            case "paste":
                ExecuteFocusedCommand(ApplicationCommands.Paste);
                break;
            case "search":
                FocusSearchBox();
                break;
            case "settings":
                ShowSettings("appearance");
                break;
            case "docs":
                OpenDirectory(Path.Combine(settings.ProjectRootDirectory, "docs"));
                break;
            case "exit":
                Close();
                break;
        }
    }

    private void Module_Checked(object sender, RoutedEventArgs e)
    {
        if (isInitializing || isSwitchingContent)
            return;
        try
        {
            isSwitchingContent = true;
            if (sender == ConfigNav)
            {
                isSwitchingContent = false;
                ShowWorkbench();
                return;
            }

            MainContent.Content = sender switch
            {
                _ when sender == ProjectNav => new PlaceholderView("项目助手", "项目管理、路径检查和常用操作将在这里提供。", "folder-kanban.png"),
                _ when sender == AssetsNav => new PlaceholderView("素材工具", "素材浏览、整理和批处理功能待补充。", "images.png"),
                _ when sender == AudioNav => new PlaceholderView("音频工具", "音频检查、转换和预览功能待补充。", "audio-lines.png"),
                _ => new PlaceholderView("提示词", "提示词分类、搜索和复用功能待补充。", "notebook-text.png")
            };
            StatusPathText.Text = "模块待补充";
        }
        catch (Exception error)
        {
            App.LogUiError("切换工作台模块失败", error);
        }
        finally
        {
            isSwitchingContent = false;
        }
    }



private void LoadLanguages()
{
LanguageCombo.ItemsSource = LanguageManager.LoadOptions();
LanguageCombo.SelectedValuePath = nameof(LanguageOption.Code);
LanguageCombo.SelectedValue = settings.Language;
if (LanguageCombo.SelectedIndex < 0)
{
LanguageCombo.SelectedIndex = 0;
if (LanguageCombo.SelectedItem is LanguageOption option)
{
settings.Language = option.Code;
SettingsStore.Save(settings);
}
}
}

private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
if (isInitializing || isApplyingLanguage || LanguageCombo.SelectedValue is not string code)
return;
settings.Language = code;
try
{
SettingsStore.Save(settings);
ApplyLanguageSafely();
}
catch (Exception error)
{
App.LogUiError("切换界面语言失败", error);
}
}

    private void ApplyLayoutSizes()
    {
        MainSidebarColumn.Width = new GridLength(Math.Clamp(settings.MainSidebarWidth, 104, 240));
    }

    private void LayoutSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        settings.MainSidebarWidth = MainSidebarColumn.Width.IsAbsolute ? MainSidebarColumn.Width.Value : 120;
        SettingsStore.Save(settings);
    }

    private void LogToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetLogsExpanded(LogOverlayHost.Visibility != Visibility.Visible);

    private void CloseLogs_Click(object sender, RoutedEventArgs e) => SetLogsExpanded(false);

    private void LogOverlayHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!LogOverlayPanel.IsMouseOver)
            SetLogsExpanded(false);
    }

    private void SetLogsExpanded(bool expanded)
    {
        LogOverlayHost.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        if (!expanded)
            return;

        isLogAtBottom = true;
        Dispatcher.BeginInvoke(() =>
        {
            LogOverlayHost.UpdateLayout();
            LogsScrollViewer.ScrollToEnd();
        });
    }

    private void LogFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        var filter = element.Tag?.ToString() ?? "all";
        if (filter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var selectAll = activeLogLevels.Count != ToolLogCategories.Levels.Count;
            activeLogLevels.Clear();
            if (selectAll)
                activeLogLevels.UnionWith(ToolLogCategories.Levels);
        }
        else if (ToolLogCategories.Levels.Contains(filter, StringComparer.OrdinalIgnoreCase))
        {
            if (!activeLogLevels.Add(filter))
                activeLogLevels.Remove(filter);
        }
        SaveLogState();
        UpdateLogFilterButtons();
        ReloadLogs();
    }

    private void UpdateLogFilterButtons()
    {
        SetLogFilterButtonState(LogFilterAllButton, activeLogLevels.Count == ToolLogCategories.Levels.Count);
        SetLogFilterButtonState(LogFilterInfoButton, activeLogLevels.Contains("INFO"));
        SetLogFilterButtonState(LogFilterSuccessButton, activeLogLevels.Contains("SUCCESS"));
        SetLogFilterButtonState(LogFilterWarningButton, activeLogLevels.Contains("WARNING"));
        SetLogFilterButtonState(LogFilterErrorButton, activeLogLevels.Contains("ERROR"));
    }

    private void UpdateLogCategoryFilterStates()
    {
        for (var index = 0; index < LogCategoryFilters.Items.Count; index++)
        {
            if (LogCategoryFilters.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container)
                continue;
            var category = (LogCategoryFilters.Items[index] as LogCategoryFilterItem)?.Category;
            var checkBox = FindVisualChild<CheckBox>(container);
            if (checkBox is not null && category is not null)
                checkBox.IsChecked = activeLogCategories.Contains(category);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }

    private void LogCategoryFilters_Loaded(object sender, RoutedEventArgs e) => UpdateLogCategoryFilterStates();

    private static void SetLogFilterButtonState(ToggleButton button, bool active)
    {
        button.IsChecked = active;
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var items = (LogsList.ItemsSource as IEnumerable<LogGroupDisplayItem> ?? [])
            .SelectMany(group => group.Entries);
        var text = string.Join(Environment.NewLine, items.Select(item => item.CopyText));
        if (text.Length > 0)
            Clipboard.SetText(text);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => ToolLogService.Clear();

    private void LogCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        LogCategoryPopup.IsOpen = !LogCategoryPopup.IsOpen;
        UpdateLogCategoryButtonState();
    }

    private void LogCategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not string category)
            return;
        if (checkBox.IsChecked == true)
            activeLogCategories.Add(category);
        else
            activeLogCategories.Remove(category);
        SyncLogCategoryItems();
        UpdateLogCategoryButtonState();
        SaveLogState();
        ReloadLogs();
    }

    private void SelectAllLogCategories_Click(object sender, RoutedEventArgs e) =>
        SetAllLogCategories(true);

    private void ClearAllLogCategories_Click(object sender, RoutedEventArgs e) =>
        SetAllLogCategories(false);

    private void SetAllLogCategories(bool selected)
    {
        activeLogCategories.Clear();
        if (selected)
            activeLogCategories.UnionWith(ToolLogCategories.All);
        SyncLogCategoryItems();
        UpdateLogCategoryButtonState();
        SaveLogState();
        ReloadLogs();
    }

    private void SyncLogCategoryItems()
    {
        if (LogCategoryFilters.ItemsSource is not IEnumerable<LogCategoryFilterItem> items)
            return;
        foreach (var item in items)
            item.IsSelected = activeLogCategories.Contains(item.Category);
    }

    private void UpdateLogCategoryButtonState() =>
        SetLogFilterButtonState(LogCategoryButton, activeLogCategories.Count != ToolLogCategories.All.Count);

    private void LogCollapseDuplicates_Click(object sender, RoutedEventArgs e)
    {
        settings.LogCollapseDuplicates = !settings.LogCollapseDuplicates;
        UpdateLogCollapseDuplicatesButton();
        SaveLogState();
        ReloadLogs();
    }

    private void UpdateLogCollapseDuplicatesButton()
    {
        LogCollapseDuplicatesButton.Content = settings.LogCollapseDuplicates ? "展开重复" : "重复折叠";
        SetLogFilterButtonState(LogCollapseDuplicatesButton, settings.LogCollapseDuplicates);
    }

    private void LogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        logSearchText = LogSearchBox.Text.Trim();
        ReloadLogs();
    }

    private void LogGroupToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LogGroupDisplayItem group)
            return;
        if (!collapsedLogCategories.Add(group.Category))
            collapsedLogCategories.Remove(group.Category);
        SaveLogState();
        ReloadLogs();
    }

    private void ExpandAllLogGroups_Click(object sender, RoutedEventArgs e)
    {
        collapsedLogCategories.Clear();
        SaveLogState();
        ReloadLogs();
    }

    private void CollapseAllLogGroups_Click(object sender, RoutedEventArgs e)
    {
        collapsedLogCategories.Clear();
        collapsedLogCategories.UnionWith(ToolLogCategories.All);
        SaveLogState();
        ReloadLogs();
    }

    private void LogPanelResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var height = Math.Clamp(LogOverlayPanel.Height - e.VerticalChange, 220, 560);
        LogOverlayPanel.Height = height;
        settings.LogPanelHeight = height;
        SettingsStore.Save(settings);
    }

    private void SaveLogState()
    {
        settings.LogFilterLevels = activeLogLevels
            .OrderBy(level => Array.IndexOf(ToolLogCategories.Levels.ToArray(), level))
            .ToList();
        settings.LogFilterCategories = activeLogCategories
            .Where(ToolLogCategories.All.Contains)
            .ToList();
        settings.LogCollapsedCategories = collapsedLogCategories
            .Where(ToolLogCategories.All.Contains)
            .ToList();
        SettingsStore.Save(settings);
    }

    private void ToolLogService_EntryAdded(object? sender, ToolLogEntry entry) =>
        Dispatcher.BeginInvoke(ReloadLogs);

    private void ToolLogService_Cleared(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(ReloadLogs);

    private void LogsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
        isLogAtBottom = LogsScrollViewer.ScrollableHeight <= 0 || LogsScrollViewer.VerticalOffset >= LogsScrollViewer.ScrollableHeight - 2;

    private void ReloadLogs()
    {
        var followBottom = isLogAtBottom || LogOverlayHost.Visibility != Visibility.Visible;
        var allEntries = ToolLogService.Snapshot().Select(LogDisplayItem.FromEntry).ToArray();
        var categoryCounts = allEntries
            .GroupBy(item => item.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var item in LogCategoryFilters.ItemsSource.OfType<LogCategoryFilterItem>())
        {
            item.Count = categoryCounts.GetValueOrDefault(item.Category);
            item.IsSelected = activeLogCategories.Contains(item.Category);
        }
        UpdateLogCategoryButtonState();
        LogFilterAllButton.Content = $"全部 ({allEntries.Length})";
        LogFilterInfoButton.Content = $"信息 ({allEntries.Count(item => item.Level == "INFO")})";
        LogFilterSuccessButton.Content = $"成功 ({allEntries.Count(item => item.Level == "SUCCESS")})";
        LogFilterWarningButton.Content = $"警告 ({allEntries.Count(item => item.Level == "WARNING")})";
        LogFilterErrorButton.Content = $"错误 ({allEntries.Count(item => item.Level == "ERROR")})";
        var entries = allEntries
            .Where(item => activeLogLevels.Contains(item.Level)
                && activeLogCategories.Contains(item.Category)
                && (logSearchText.Length == 0
                    || item.Category.Contains(logSearchText, StringComparison.OrdinalIgnoreCase)
                    || item.Source.Contains(logSearchText, StringComparison.OrdinalIgnoreCase)
                    || item.Message.Contains(logSearchText, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var visibleEntries = settings.LogCollapseDuplicates
            ? LogDisplayItems.Collapse(entries)
            : entries;
        var groups = visibleEntries
            .GroupBy(item => item.Category, StringComparer.Ordinal)
            .OrderBy(group => Array.IndexOf(ToolLogCategories.All.ToArray(), group.Key))
            .Select(group => new LogGroupDisplayItem(
                group.Key,
                group.ToArray()))
            .ToArray();
        LogsList.ItemsSource = groups;
        var totalErrors = allEntries.Count(item => item.Level == "ERROR");
        var totalWarnings = allEntries.Count(item => item.Level == "WARNING");
        LogSummaryText.Text = $"{entries.Length}/{allEntries.Length} 条 · 错误 {totalErrors} · 警告 {totalWarnings}";
        LogToggleButton.ToolTip = $"打开日志（错误 {totalErrors} · 警告 {totalWarnings}）";
        if (followBottom)
            Dispatcher.BeginInvoke(() => LogsScrollViewer.ScrollToEnd());
    }

    private void ExecuteFocusedCommand(RoutedCommand command)
    {
        if (command.CanExecute(null, Keyboard.FocusedElement))
            command.Execute(null, Keyboard.FocusedElement);
    }

    private void FocusSearchBox()
    {
        if (workbenchView is null)
            return;
        workbenchView.FocusSearch();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (MainContent.Content is SettingsView)
            return;


        if (MatchesShortcut(settings.BuildShortcut, e))
        {
            workbenchView?.BuildTables();
            e.Handled = true;
            return;
        }

        if (MatchesShortcut(settings.RefreshShortcut, e))
        {
            workbenchView?.RefreshTables(selectAll: false);
            e.Handled = true;
        }
    }

    private static bool MatchesShortcut(string shortcut, KeyEventArgs e)
    {
        var normalized = shortcut.Replace(" ", string.Empty);
        var parts = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1 || !Enum.TryParse(parts[^1], true, out Key key))
            return false;

        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "shift" => ModifierKeys.Shift,
                "alt" => ModifierKeys.Alt,
                "win" or "windows" => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
        }

        var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        return actualKey == key && Keyboard.Modifiers == modifiers;
    }

    private static void OpenDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            ToolLogService.Info("系统", $"打开目录：{path}");
        }
        catch (Exception error)
        {
            ToolLogService.Error("系统", $"打开目录失败：{path}：{error.Message}");
        }
    }

protected override void OnClosed(EventArgs e)
{
base.OnClosed(e);
Application.Current.Shutdown();
Environment.Exit(0);
}

private void MainWindow_Closing(object? sender, CancelEventArgs e)
{
        if (LogOverlayPanel is not null && LogOverlayPanel.Height > 0)
            settings.LogPanelHeight = Math.Clamp(LogOverlayPanel.Height, 220, 560);
        SaveLogState();
    if (WindowState == WindowState.Normal)
    {
        settings.MainSidebarWidth = MainSidebarColumn.Width.IsAbsolute ? MainSidebarColumn.Width.Value : settings.MainSidebarWidth;
        settings.WindowWidth = Math.Max(MinWidth, Width);
        settings.WindowHeight = Math.Max(MinHeight, Height);
    }

    SettingsStore.Save(settings);
}

private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
