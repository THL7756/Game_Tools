// 用途：实现项目路径和视觉风格设置页面交互。
// 最近修改日期：2026-10-06

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using TableTool.Gui.Models;
using TableTool.Gui.Services;

namespace TableTool.Gui.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly AppSettings settings;
    private readonly string requestedPage;
    private string currentPage = "appearance";
    private bool isInitializing = true;

    public event EventHandler? SettingsSaved;

    public SettingsView(AppSettings settings, string initialPage)
    {
        InitializeComponent();
        this.settings = settings;
        requestedPage = initialPage;
        LoadControls();
        ShowPage(initialPage);
        isInitializing = false;
        Loaded += (_, _) => ShowPage(requestedPage);
        Dispatcher.BeginInvoke(() => ShowPage(requestedPage));
    }

    private void LoadControls()
    {
        ProjectRootBox.Text = settings.ProjectRootDirectory;
        TableDirectoryBox.Text = settings.TableDirectory;
        ClientOutputBox.Text = settings.ClientOutputDirectory;
        ServerOutputBox.Text = settings.ServerOutputDirectory;
        BuildScriptBox.Text = settings.BuildScriptPath;
        AccentTextBox.Text = settings.AccentColor.ToUpperInvariant();
        BackgroundTextBox.Text = settings.BackgroundColor.ToUpperInvariant();
        ForegroundTextBox.Text = settings.ForegroundColor.ToUpperInvariant();

        ThemeCombo.SelectedIndex = settings.AppearanceMode switch
        {
            AppearanceMode.Light => 0,
            AppearanceMode.Dark => 1,
            _ => 2
        };
        FontCombo.SelectedIndex = settings.FontFamilyName switch
        {
            "Microsoft YaHei UI" => 1,
            "JetBrains Mono" => 2,
            _ => 0
        };
        FontSizeCombo.SelectedIndex = settings.FontSize switch
        {
            12 => 0,
            14 => 2,
            16 => 3,
            _ => 1
        };
        ZoomCombo.SelectedIndex = settings.Zoom switch
        {
            90 => 0,
            110 => 2,
            125 => 3,
            _ => 1
        };

        SetPreviewSelection(settings.AppearanceMode);
    }

    private void SetPreviewSelection(AppearanceMode mode)
    {
        isInitializing = true;
        switch (mode)
        {
            case AppearanceMode.Light:
                LightPreview.IsChecked = true;
                break;
            case AppearanceMode.System:
                SystemPreview.IsChecked = true;
                break;
            default:
                DarkPreview.IsChecked = true;
                break;
        }
        isInitializing = false;
    }

    private void ShowPage(string page)
    {
        currentPage = page;
        AppearancePanel.Visibility = page == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        PathsPanel.Visibility = page == "paths" ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsPanel.Visibility = page == "shortcuts" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;
        isInitializing = true;
        switch (page)
        {
            case "paths":
                PathsNav.IsChecked = true;
                break;
            case "shortcuts":
                ShortcutsNav.IsChecked = true;
                break;
            case "about":
                AboutNav.IsChecked = true;
                break;
            default:
                AppearanceNav.IsChecked = true;
                break;
        }
        isInitializing = false;
    }

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (isInitializing || sender is not FrameworkElement element)
            return;
        if ((element.Tag?.ToString() ?? "appearance") == currentPage)
            return;
        ShowPage(element.Tag?.ToString() ?? "appearance");
    }

    private void AppearanceMode_Checked(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
            return;
        if (sender is not FrameworkElement element)
            return;
        settings.AppearanceMode = element.Tag?.ToString() switch
        {
            "light" => AppearanceMode.Light,
            "system" => AppearanceMode.System,
            _ => AppearanceMode.Dark
        };
        ThemeCombo.SelectedIndex = settings.AppearanceMode switch
        {
            AppearanceMode.Light => 0,
            AppearanceMode.Dark => 1,
            _ => 2
        };
        ThemeManager.Apply(settings);
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
            return;
        settings.AppearanceMode = ThemeCombo.SelectedIndex switch
        {
            0 => AppearanceMode.Light,
            1 => AppearanceMode.Dark,
            _ => AppearanceMode.System
        };
        SetPreviewSelection(settings.AppearanceMode);
        ThemeManager.Apply(settings);
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
            return;
        settings.FontFamilyName = FontCombo.SelectedIndex switch
        {
            1 => "Microsoft YaHei UI",
            2 => "JetBrains Mono",
            _ => "Noto Sans SC"
        };
        ThemeManager.Apply(settings);
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
            return;
        settings.FontSize = FontSizeCombo.SelectedIndex switch
        {
            0 => 12,
            2 => 14,
            3 => 16,
            _ => 13
        };
        ThemeManager.Apply(settings);
    }

    private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing)
            return;
        settings.Zoom = ZoomCombo.SelectedIndex switch
        {
            0 => 90,
            2 => 110,
            3 => 125,
            _ => 100
        };
        ThemeManager.Apply(settings);
    }

    private void AccentSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string color)
            return;
        settings.AccentColor = color;
        AccentTextBox.Text = color.ToUpperInvariant();
        ThemeManager.Apply(settings);
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        var current = element.Tag?.ToString() switch
        {
            "background" => settings.BackgroundColor,
            "foreground" => settings.ForegroundColor,
            _ => settings.AccentColor
        };

        using var dialog = new System.Windows.Forms.ColorDialog();
        try
        {
            dialog.Color = System.Drawing.ColorTranslator.FromHtml(current);
        }
        catch
        {
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        switch (element.Tag?.ToString())
        {
            case "background":
                settings.BackgroundColor = color;
                BackgroundTextBox.Text = color;
                break;
            case "foreground":
                settings.ForegroundColor = color;
                ForegroundTextBox.Text = color;
                break;
            default:
                settings.AccentColor = color;
                AccentTextBox.Text = color;
                break;
        }

        ThemeManager.Apply(settings);
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        var dialog = new OpenFolderDialog
        {
            Title = "选择目录",
            InitialDirectory = GetInitialDirectory(element.Tag?.ToString())
        };
        if (dialog.ShowDialog() != true)
            return;

        switch (element.Tag?.ToString())
        {
            case "root":
                ProjectRootBox.Text = dialog.FolderName;
                break;
            case "tables":
                TableDirectoryBox.Text = dialog.FolderName;
                break;
            case "client":
                ClientOutputBox.Text = dialog.FolderName;
                break;
            case "server":
                ServerOutputBox.Text = dialog.FolderName;
                break;
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择打表脚本",
            Filter = "脚本文件 (*.py;*.ps1;*.bat;*.cmd)|*.py;*.ps1;*.bat;*.cmd|所有文件 (*.*)|*.*",
            InitialDirectory = settings.ProjectRootDirectory
        };
        if (dialog.ShowDialog() == true)
            BuildScriptBox.Text = dialog.FileName;
    }

    private string GetInitialDirectory(string? tag) => tag switch
    {
        "tables" => TableDirectoryBox.Text,
        "client" => ClientOutputBox.Text,
        "server" => ServerOutputBox.Text,
        _ => ProjectRootBox.Text
    };

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var defaults = SettingsStore.CreateDefault();
        settings.ProjectRootDirectory = defaults.ProjectRootDirectory;
        settings.TableDirectory = defaults.TableDirectory;
        settings.ClientOutputDirectory = defaults.ClientOutputDirectory;
        settings.ServerOutputDirectory = defaults.ServerOutputDirectory;
        settings.BuildScriptPath = defaults.BuildScriptPath;
        settings.AppearanceMode = defaults.AppearanceMode;
        settings.AccentColor = defaults.AccentColor;
        settings.BackgroundColor = defaults.BackgroundColor;
        settings.ForegroundColor = defaults.ForegroundColor;
        settings.FontFamilyName = defaults.FontFamilyName;
        settings.FontSize = defaults.FontSize;
        settings.Zoom = defaults.Zoom;
        LoadControls();
        ThemeManager.Apply(settings);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        settings.ProjectRootDirectory = ProjectRootBox.Text.Trim();
        settings.TableDirectory = TableDirectoryBox.Text.Trim();
        settings.ClientOutputDirectory = ClientOutputBox.Text.Trim();
        settings.ServerOutputDirectory = ServerOutputBox.Text.Trim();
        settings.BuildScriptPath = BuildScriptBox.Text.Trim();
        settings.AccentColor = NormalizeColor(AccentTextBox.Text, settings.AccentColor);
        settings.BackgroundColor = NormalizeColor(BackgroundTextBox.Text, settings.BackgroundColor);
        settings.ForegroundColor = NormalizeColor(ForegroundTextBox.Text, settings.ForegroundColor);

        if (!ValidateSettings())
            return;

        SettingsStore.Save(settings);
        ThemeManager.Apply(settings);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(settings.ProjectRootDirectory)
            || string.IsNullOrWhiteSpace(settings.TableDirectory)
            || string.IsNullOrWhiteSpace(settings.ClientOutputDirectory)
            || string.IsNullOrWhiteSpace(settings.ServerOutputDirectory))
        {
            MessageBox.Show("项目根目录、配置表目录和两个输出目录都必须填写。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var client = Path.GetFullPath(settings.ClientOutputDirectory);
        var server = Path.GetFullPath(settings.ServerOutputDirectory);
        if (string.Equals(client, server, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("客户端和服务器输出目录不能相同。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var tableRoot = Path.GetFullPath(settings.TableDirectory);
        if (IsSameOrAncestor(client, tableRoot) || IsSameOrAncestor(server, tableRoot))
        {
            MessageBox.Show("输出目录不能是配置表目录或其父目录。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private static bool IsSameOrAncestor(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeColor(string value, string fallback)
    {
        try
        {
            var color = System.Drawing.ColorTranslator.FromHtml(value);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return fallback;
        }
    }
}
