// 用途：管理主窗口导航、窗口控制和顶层主题操作。
// 最近修改日期：2026-10-06

using System.Diagnostics;
using System.IO;
using System.Windows;
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

    public MainWindow()
    {
        InitializeComponent();
        settings = SettingsStore.Load();
        ThemeManager.Apply(settings);
        ApplyZoom();
        UpdateThemeButtons();
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
    }

    private void ShowWorkbench()
    {
        if (workbenchView is null)
        {
            workbenchView = new WorkbenchView(settings);
            workbenchView.StatusChanged += (_, text) => StatusPathText.Text = text;
        }

        MainContent.Content = workbenchView;
        ConfigNav.IsChecked = true;
        workbenchView.RefreshTables(selectAll: true);
        StatusPathText.Text = settings.ProjectRootDirectory.Replace('\\', '/');
    }

    private void ShowSettings(string page)
    {
        var view = new SettingsView(settings, page);
        view.SettingsSaved += (_, _) =>
        {
            SettingsStore.Save(settings);
            ThemeManager.Apply(settings);
            ApplyZoom();
            workbenchView?.RefreshTables(selectAll: false);
            ShowWorkbench();
        };
        MainContent.Content = view;
        StatusPathText.Text = settings.ProjectRootDirectory.Replace('\\', '/');
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
        if (isInitializing)
            return;
        if (sender == ConfigNav)
        {
            if (MainContent.Content is Views.SettingsView)
                ShowWorkbench();
            return;
        }
        ConfigNav.IsChecked = true;
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
