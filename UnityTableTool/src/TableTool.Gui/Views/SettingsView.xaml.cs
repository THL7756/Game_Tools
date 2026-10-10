// 用途：实现项目路径和视觉风格设置页面交互。
// 用途：实现设置页的路径、外观、快捷键、滚轮和数组分隔符配置。
// 最近修改日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TableTool.Gui.Models;
using TableTool.Gui.Services;

namespace TableTool.Gui.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly AppSettings settings;
    private string currentPage = "appearance";
    private bool isInitializing = true;
    private bool isChangingPage;
    private readonly Dictionary<TextBox, string> shortcutValuesBeforeCapture = [];

    public event EventHandler? SettingsSaved;

    public SettingsView(AppSettings settings, string initialPage)
    {
        this.settings = settings;
        InitializeComponent();
        ApplyLayoutSizes();
        LoadControls();
        ShowPage(initialPage);
        isInitializing = false;
    }

    private void LoadControls()
    {
        ProjectRootBox.Text = settings.ProjectRootDirectory;
        TableDirectoryBox.Text = settings.TableDirectory;
        ClientOutputBox.Text = settings.ClientOutputDirectory;
        ServerOutputBox.Text = settings.ServerOutputDirectory;
        ClientCodeOutputBox.Text = settings.ClientCodeOutputDirectory;
        ServerCodeOutputBox.Text = settings.ServerCodeOutputDirectory;
        UnityRuntimePackageBox.Text = settings.UnityRuntimePackageDirectory;
        BuildShortcutBox.Text = settings.BuildShortcut;
        RefreshShortcutBox.Text = settings.RefreshShortcut;
        ArrayInnerSeparatorBox.Text = settings.ArrayInnerSeparator;
        ArrayMiddleSeparatorBox.Text = settings.ArrayMiddleSeparator;
        ArrayOuterSeparatorBox.Text = settings.ArrayOuterSeparator;
        LoadWheelStepControls();
        AccentColorPicker.SetHex(settings.AccentColor);
        BackgroundColorPicker.SetHex(settings.BackgroundColor);
        ForegroundColorPicker.SetHex(settings.ForegroundColor);

        if (ThemeCombo.Parent is Grid themeRow)
            themeRow.Visibility = Visibility.Collapsed;
        var installedFonts = Fonts.SystemFontFamilies
            .OrderBy(font => font.Source, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        FontCombo.ItemsSource = installedFonts;
        var selectedFont = installedFonts.FirstOrDefault(font =>
            string.Equals(font.Source, settings.FontFamilyName, StringComparison.OrdinalIgnoreCase))
            ?? installedFonts.FirstOrDefault(font => string.Equals(font.Source, "Segoe UI", StringComparison.OrdinalIgnoreCase))
            ?? installedFonts.FirstOrDefault();
        FontCombo.SelectedItem = selectedFont;
        if (selectedFont is not null && !string.Equals(selectedFont.Source, settings.FontFamilyName, StringComparison.OrdinalIgnoreCase))
        {
            settings.FontFamilyName = selectedFont.Source;
            ThemeManager.Apply(settings);
        }
        FontSizeCombo.ItemsSource = new double[] { 8, 10, 12, 13, 14, 16, 18, 20, 24, 28, 32 };
        FontSizeCombo.Text = settings.FontSize.ToString("0.#");
        ZoomCombo.SelectedIndex = settings.Zoom switch
        {
            90 => 0,
            110 => 2,
            125 => 3,
            _ => 1
        };

        SetPreviewSelection(settings.AppearanceMode);
    }

    private void ShortcutBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || shortcutValuesBeforeCapture.ContainsKey(box))
            return;
        shortcutValuesBeforeCapture[box] = GetShortcutValue(box);
        box.Text = "请按下快捷键…";
    }

    private void ShortcutBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || !shortcutValuesBeforeCapture.Remove(box, out _))
            return;
        box.Text = FormatShortcutDisplay(GetShortcutValue(box));
    }

    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            box.Text = $"{FormatModifiers(Keyboard.Modifiers)}+…";
            e.Handled = true;
            return;
        }

        if (key == Key.Escape)
        {
            var previous = shortcutValuesBeforeCapture.GetValueOrDefault(box, GetShortcutValue(box));
            shortcutValuesBeforeCapture.Remove(box);
            box.Text = FormatShortcutDisplay(previous);
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            SetShortcutValue(box, string.Empty);
            shortcutValuesBeforeCapture.Remove(box);
            box.Text = "未设置";
            PersistSettings();
            ToolLogService.Info("设置", "快捷键已清除。");
            e.Handled = true;
            return;
        }

        if (key == Key.None)
        {
            e.Handled = true;
            return;
        }

        var shortcut = $"{FormatModifiers(Keyboard.Modifiers)}{key}";
        var otherBox = ReferenceEquals(box, BuildShortcutBox) ? RefreshShortcutBox : BuildShortcutBox;
        if (string.Equals(shortcut, GetShortcutValue(otherBox), StringComparison.OrdinalIgnoreCase))
        {
            box.Text = "快捷键冲突，请重新输入";
            e.Handled = true;
            return;
        }

        SetShortcutValue(box, shortcut);
        shortcutValuesBeforeCapture.Remove(box);
        box.Text = shortcut;
        PersistSettings();
        ToolLogService.Info("设置", $"快捷键已更新：{shortcut}");
        e.Handled = true;
    }

    private static string FormatModifiers(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return parts.Count == 0 ? string.Empty : string.Join("+", parts) + "+";
    }

    private static string FormatShortcutDisplay(string value) =>
        string.IsNullOrWhiteSpace(value) ? "未设置" : value;

    private string GetShortcutValue(TextBox box) =>
        string.Equals(box.Tag?.ToString(), "build", StringComparison.OrdinalIgnoreCase)
            ? settings.BuildShortcut
            : settings.RefreshShortcut;

    private void SetShortcutValue(TextBox box, string value)
    {
        if (string.Equals(box.Tag?.ToString(), "build", StringComparison.OrdinalIgnoreCase))
            settings.BuildShortcut = value;
        else
            settings.RefreshShortcut = value;
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
        if (isChangingPage)
            return;

        isChangingPage = true;
        try
        {
            page = page is "paths" or "shortcuts" or "about" ? page : "appearance";
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
        }
        finally
        {
            isInitializing = false;
            isChangingPage = false;
        }
    }

    private void SettingsNav_Checked(object sender, RoutedEventArgs e)
    {
        if (isInitializing || isChangingPage || sender is not FrameworkElement element)
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
        ThemeManager.Apply(settings);
        PersistSettings();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 主题已由页面模式单选按钮控制，保留事件入口以兼容旧配置文件与旧 XAML。
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isInitializing || FontCombo.SelectedItem is not FontFamily font)
            return;
        settings.FontFamilyName = font.Source;
        ThemeManager.Apply(settings);
        PersistSettings();
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isInitializing && FontSizeCombo.SelectedItem is double size)
            CommitFontSize(size.ToString("0.#"), showError: false);
    }

    private void FontSizeCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        CommitFontSize(FontSizeCombo.Text, showError: true);
        e.Handled = true;
    }

    private void FontSizeCombo_LostFocus(object sender, RoutedEventArgs e) =>
        CommitFontSize(FontSizeCombo.Text, showError: true);

    private void CommitFontSize(string text, bool showError)
    {
        if (!double.TryParse(text, out var size))
            size = double.NaN;
        if (double.IsNaN(size) || size < 6 || size > 72)
        {
            if (showError)
                MessageBox.Show("字体大小必须是 6–72 之间的数值。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            FontSizeCombo.Text = settings.FontSize.ToString("0.#");
            return;
        }

        settings.FontSize = Math.Round(size, 1);
        FontSizeCombo.Text = settings.FontSize.ToString("0.#");
        ThemeManager.Apply(settings);
        PersistSettings();
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
        PersistSettings();
    }

    private void LoadWheelStepControls()
    {
        SetWheelStepControls(VerticalWheelStepSlider, VerticalWheelStepBox, settings.VerticalWheelScrollStep);
        SetWheelStepControls(HorizontalWheelStepSlider, HorizontalWheelStepBox, settings.HorizontalWheelScrollStep);
    }

    private static void SetWheelStepControls(Slider slider, TextBox box, int value)
    {
        var normalized = Math.Clamp(value, 1, 10);
        slider.Value = normalized;
        box.Text = normalized.ToString();
    }

    private void VerticalWheelStepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!isInitializing && VerticalWheelStepBox is not null)
            SetWheelStep(true, (int)e.NewValue);
    }

    private void HorizontalWheelStepSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!isInitializing && HorizontalWheelStepBox is not null)
            SetWheelStep(false, (int)e.NewValue);
    }

    private void WheelStepBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        CommitWheelStep(sender as TextBox, showError: true);
        e.Handled = true;
    }

    private void WheelStepBox_LostFocus(object sender, RoutedEventArgs e) =>
        CommitWheelStep(sender as TextBox, showError: true);

    private void CommitWheelStep(TextBox? box, bool showError)
    {
        if (box is null || !int.TryParse(box.Text, out var value) || value < 1 || value > 10)
        {
            if (showError)
                MessageBox.Show("滚轮行/列数必须是 1–10 之间的整数。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoadWheelStepControls();
            return;
        }
        SetWheelStep(ReferenceEquals(box, VerticalWheelStepBox), value);
    }

    private void SetWheelStep(bool vertical, int value)
    {
        value = Math.Clamp(value, 1, 10);
        if (vertical)
        {
            settings.VerticalWheelScrollStep = value;
            VerticalWheelStepBox.Text = value.ToString();
            VerticalWheelStepSlider.Value = value;
        }
        else
        {
            settings.HorizontalWheelScrollStep = value;
            HorizontalWheelStepBox.Text = value.ToString();
            HorizontalWheelStepSlider.Value = value;
        }
        PersistSettings();
    }

    private void ColorPicker_PreviewChanged(object? sender, EventArgs e)
    {
        if (isInitializing || sender is not ColorPickerView picker)
            return;
        ApplyPickerColor(picker);
    }

    private void ColorPicker_Committed(object? sender, EventArgs e)
    {
        if (sender is not ColorPickerView picker)
            return;
        ApplyPickerColor(picker);
        PersistSettings();
    }

    private void ColorPicker_Canceled(object? sender, EventArgs e)
    {
        if (sender is ColorPickerView picker)
            ApplyPickerColor(picker);
    }

    private void ApplyPickerColor(ColorPickerView picker)
    {
        var color = picker.GetHex();
        switch (picker.ColorKey)
        {
            case "background":
                settings.BackgroundColor = color;
                break;
            case "foreground":
                settings.ForegroundColor = color;
                break;
            default:
                settings.AccentColor = color;
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
            case "clientCode":
                ClientCodeOutputBox.Text = dialog.FolderName;
                break;
            case "serverCode":
                ServerCodeOutputBox.Text = dialog.FolderName;
                break;
            case "unityRuntimePackage":
                UnityRuntimePackageBox.Text = dialog.FolderName;
                break;
        }
        ToolLogService.Info("设置", $"选择目录：{dialog.FolderName}");
    }

    private string GetInitialDirectory(string? tag) => tag switch
    {
        "tables" => TableDirectoryBox.Text,
        "client" => ClientOutputBox.Text,
        "server" => ServerOutputBox.Text,
        "clientCode" => ClientCodeOutputBox.Text,
        "serverCode" => ServerCodeOutputBox.Text,
        "unityRuntimePackage" => UnityRuntimePackageBox.Text,
        _ => ProjectRootBox.Text
    };

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var defaults = SettingsStore.CreateDefault();
        settings.ProjectRootDirectory = defaults.ProjectRootDirectory;
        settings.TableDirectory = defaults.TableDirectory;
        settings.ClientOutputDirectory = defaults.ClientOutputDirectory;
        settings.ServerOutputDirectory = defaults.ServerOutputDirectory;
        settings.ClientCodeOutputDirectory = defaults.ClientCodeOutputDirectory;
        settings.ServerCodeOutputDirectory = defaults.ServerCodeOutputDirectory;
        settings.UnityRuntimePackageDirectory = defaults.UnityRuntimePackageDirectory;
        settings.BuildScriptPath = defaults.BuildScriptPath;
        settings.AppearanceMode = defaults.AppearanceMode;
        settings.AccentColor = defaults.AccentColor;
        settings.BackgroundColor = defaults.BackgroundColor;
        settings.ForegroundColor = defaults.ForegroundColor;
        settings.FontFamilyName = defaults.FontFamilyName;
        settings.FontSize = defaults.FontSize;
        settings.Zoom = defaults.Zoom;
        settings.BuildShortcut = defaults.BuildShortcut;
        settings.RefreshShortcut = defaults.RefreshShortcut;
        settings.VerticalWheelScrollStep = defaults.VerticalWheelScrollStep;
        settings.HorizontalWheelScrollStep = defaults.HorizontalWheelScrollStep;
        settings.MainSidebarWidth = defaults.MainSidebarWidth;
        settings.WorkbenchTableListWidth = defaults.WorkbenchTableListWidth;
        settings.WorkbenchDetailHeight = defaults.WorkbenchDetailHeight;
        settings.WorkbenchIssuesHeight = defaults.WorkbenchIssuesHeight;
        settings.SettingsSidebarWidth = defaults.SettingsSidebarWidth;
        ApplyLayoutSizes();
        settings.ArrayInnerSeparator = defaults.ArrayInnerSeparator;
        settings.ArrayMiddleSeparator = defaults.ArrayMiddleSeparator;
        settings.ArrayOuterSeparator = defaults.ArrayOuterSeparator;
        isInitializing = true;
        LoadControls();
        isInitializing = false;
        ThemeManager.Apply(settings);
        PersistSettings();
        ToolLogService.Info("设置", "已恢复默认设置。");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        settings.ProjectRootDirectory = ProjectRootBox.Text.Trim();
        settings.TableDirectory = TableDirectoryBox.Text.Trim();
        settings.ClientOutputDirectory = ClientOutputBox.Text.Trim();
        settings.ServerOutputDirectory = ServerOutputBox.Text.Trim();
        settings.ClientCodeOutputDirectory = ClientCodeOutputBox.Text.Trim();
        settings.ServerCodeOutputDirectory = ServerCodeOutputBox.Text.Trim();
        settings.UnityRuntimePackageDirectory = UnityRuntimePackageBox.Text.Trim();
        settings.ArrayInnerSeparator = ArrayInnerSeparatorBox.Text;
        settings.ArrayMiddleSeparator = ArrayMiddleSeparatorBox.Text;
        settings.ArrayOuterSeparator = ArrayOuterSeparatorBox.Text;

        if (!ValidateSettings())
            return;

        SettingsStore.Save(settings);
        ThemeManager.Apply(settings);
        ToolLogService.Success("设置", "设置已保存。");
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateSettings()
    {
        if (!new TableTool.Core.Models.ArraySeparatorOptions(
                settings.ArrayInnerSeparator,
                settings.ArrayMiddleSeparator,
                settings.ArrayOuterSeparator).IsValid)
        {
            MessageBox.Show("数组分隔符必须是三个互不相同的非空单字符，不能使用空白或逗号。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(settings.ProjectRootDirectory)
            || string.IsNullOrWhiteSpace(settings.TableDirectory)
            || string.IsNullOrWhiteSpace(settings.ClientOutputDirectory)
            || string.IsNullOrWhiteSpace(settings.ServerOutputDirectory)
            || string.IsNullOrWhiteSpace(settings.ClientCodeOutputDirectory)
            || string.IsNullOrWhiteSpace(settings.ServerCodeOutputDirectory)
            || string.IsNullOrWhiteSpace(settings.UnityRuntimePackageDirectory))
        {
            MessageBox.Show("项目根目录、配置表目录、Unity包目录和四个输出目录都必须填写。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var packageError = UnityRuntimePackageLocator.Validate(settings);
        if (packageError is not null)
        {
            MessageBox.Show(packageError, "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var outputs = new[]
        {
            Path.GetFullPath(settings.ClientOutputDirectory),
            Path.GetFullPath(settings.ServerOutputDirectory),
            Path.GetFullPath(settings.ClientCodeOutputDirectory),
            Path.GetFullPath(settings.ServerCodeOutputDirectory)
        };
        if (outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != outputs.Length)
        {
            MessageBox.Show("客户端/服务器的数据与代码输出目录不能相同。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        for (var left = 0; left < outputs.Length; left++)
        {
            for (var right = left + 1; right < outputs.Length; right++)
            {
                if (IsSameOrAncestor(outputs[left], outputs[right]) || IsSameOrAncestor(outputs[right], outputs[left]))
                {
                    MessageBox.Show("输出目录之间不能互相包含。", "设置", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
        }

        var tableRoot = Path.GetFullPath(settings.TableDirectory);
        if (outputs.Any(output => IsSameOrAncestor(output, tableRoot)))
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

    private void PersistSettings()
    {
        try
        {
            SettingsStore.Save(settings);
        }
        catch
        {
            // 保存失败时仍保留当前页面状态，点击“保存设置”可以再次尝试。
        }
    }

    private void SettingsView_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        ScrollWheelService.Handle(e, e.OriginalSource as DependencyObject, settings);

    private void ApplyLayoutSizes()
    {
        SettingsNavColumn.Width = new GridLength(Math.Clamp(settings.SettingsSidebarWidth, 140, 280));
    }

    private void LayoutSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        settings.SettingsSidebarWidth = SettingsNavColumn.Width.IsAbsolute ? SettingsNavColumn.Width.Value : 155;
        SettingsStore.Save(settings);
    }
}
