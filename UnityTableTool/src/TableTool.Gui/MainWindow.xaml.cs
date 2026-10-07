// 用途：管理主窗口导航、窗口控制和顶层主题操作。
// 最近修改日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

    public MainWindow()
    {
        InitializeComponent();
        settings = SettingsStore.Load();
        ThemeManager.Apply(settings);
LoadLanguages();
SearchMenuItem.InputGestureText = settings.SearchShortcut;
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
ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(UpdateThemeButtons);
ApplyLanguageSafely();

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
                SearchMenuItem.InputGestureText = settings.SearchShortcut;
                workbenchView?.RefreshTables(selectAll: false);
                ShowWorkbench();
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

        if (MatchesShortcut(settings.SearchShortcut, e))
        {
            FocusSearchBox();
            e.Handled = true;
            return;
        }

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
        }
        catch
        {
        }
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
