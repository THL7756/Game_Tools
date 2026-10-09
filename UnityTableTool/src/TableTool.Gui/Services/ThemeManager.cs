// 用途：根据设置更新应用颜色、字体和缩放资源。
// 最近修改日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TableTool.Gui.Models;

namespace TableTool.Gui.Services;

public static class ThemeManager
{
    public static event EventHandler? ThemeChanged;

    public static void Apply(AppSettings settings)
    {
        var dark = settings.AppearanceMode switch
        {
            AppearanceMode.Light => false,
            AppearanceMode.Dark => true,
            _ => IsSystemDark()
        };

        var accent = ParseColor(settings.AccentColor, "#4D8DF7");
        SetBrush("Brush.Accent", accent);
        SetBrush("Brush.AccentSoft", Blend(accent, dark ? "#17191D" : "#FFFFFF", 0.22));
        SetBrush("Brush.AccentMuted", Blend(accent, dark ? "#17191D" : "#FFFFFF", 0.12));

        if (dark)
        {
            var background = (string.Equals(settings.BackgroundColor, "#F5F7FA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(settings.BackgroundColor, "#17191D", StringComparison.OrdinalIgnoreCase))
                ? "#000000"
                : settings.BackgroundColor;
            var foreground = (string.Equals(settings.ForegroundColor, "#17191D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(settings.ForegroundColor, "#E2E6ED", StringComparison.OrdinalIgnoreCase))
                ? "#FFFFFF"
                : settings.ForegroundColor;
            SetBrush("Brush.Window", ParseColor(background, "#000000"));
            SetBrush("Brush.Chrome", "#101010");
            SetBrush("Brush.Panel", "#111111");
            SetBrush("Brush.Surface", "#202329");
            SetBrush("Brush.SurfaceAlt", "#2D323B");
            SetBrush("Brush.SurfaceHover", "#272B32");
            SetBrush("Brush.Border", "#353A43");
            var darkForeground = EnsureContrast(ParseColor(foreground, "#FFFFFF"), background, true);
            SetBrush("Brush.Text", darkForeground);
            SetBrush("Brush.TextSecondary", Blend(darkForeground, background, 0.68));
            SetBrush("Brush.TextMuted", Blend(darkForeground, background, 0.45));
            SetBrush("Brush.RowSelected", Blend(accent, background, 0.18));
            SetBrush("Brush.SelectionCell", accent);
            SetBrush("Brush.SelectionCross", Blend(accent, background, 0.22));
            SetBrush("Brush.Overlay", "#111317");
        }
        else
        {
            var background = (string.Equals(settings.BackgroundColor, "#17191D", StringComparison.OrdinalIgnoreCase)
                || string.Equals(settings.BackgroundColor, "#F5F7FA", StringComparison.OrdinalIgnoreCase))
                ? "#FFFFFF"
                : settings.BackgroundColor;
            var foreground = (string.Equals(settings.ForegroundColor, "#E2E6ED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(settings.ForegroundColor, "#17191D", StringComparison.OrdinalIgnoreCase))
                ? "#000000"
                : settings.ForegroundColor;
            SetBrush("Brush.Window", ParseColor(background, "#FFFFFF"));
            SetBrush("Brush.Chrome", "#FFFFFF");
            SetBrush("Brush.Panel", "#FFFFFF");
            SetBrush("Brush.Surface", "#F8FAFC");
            SetBrush("Brush.SurfaceAlt", "#E9EDF3");
            SetBrush("Brush.SurfaceHover", "#EDF2F7");
            SetBrush("Brush.Border", "#D5DAE2");
            var lightForeground = EnsureContrast(ParseColor(foreground, "#000000"), background, false);
            SetBrush("Brush.Text", lightForeground);
            SetBrush("Brush.TextSecondary", Blend(lightForeground, background, 0.68));
            SetBrush("Brush.TextMuted", Blend(lightForeground, background, 0.45));
            SetBrush("Brush.RowSelected", Blend(accent, background, 0.18));
            SetBrush("Brush.SelectionCell", accent);
            SetBrush("Brush.SelectionCross", Blend(accent, background, 0.12));
            SetBrush("Brush.Overlay", "#E8ECF2");
        }

        ApplySystemMenuBrushes(dark);

        SetBrush("Brush.Success", dark ? "#54D6A0" : "#16845B");
        SetBrush("Brush.Warning", dark ? "#E7B95E" : "#A36B00");
        SetBrush("Brush.Error", dark ? "#FF7B7B" : "#C53A3A");
        SetBrush("Brush.SuccessSoft", dark ? "#17382D" : "#E2F4EC");
        SetBrush("Brush.WarningSoft", dark ? "#3A3321" : "#FFF1C9");
        SetBrush("Brush.ErrorSoft", dark ? "#3A2024" : "#FCE8E8");
        Application.Current.Resources["Font.Interface"] = new FontFamily(settings.FontFamilyName);
        Application.Current.Resources["Size.Interface"] = settings.FontSize;
        Application.Current.Resources["Size.Zoom"] = settings.Zoom / 100d;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return true;
        }
    }

    private static void SetBrush(string key, string color) =>
        Application.Current.Resources[key] = new SolidColorBrush(ParseColor(color, "#000000"));

    private static void SetBrush(string key, System.Windows.Media.Color color) =>
        Application.Current.Resources[key] = new SolidColorBrush(color);

    private static void ApplySystemMenuBrushes(bool dark)
    {
        SetSystemBrush(SystemColors.MenuBrushKey, dark ? "#111111" : "#FFFFFF");
        SetSystemBrush(SystemColors.MenuTextBrushKey, dark ? "#FFFFFF" : "#000000");
        SetSystemBrush(SystemColors.MenuHighlightBrushKey, dark ? "#272B32" : "#EDF2F7");
        SetSystemBrush(SystemColors.MenuBarBrushKey, dark ? "#101010" : "#FFFFFF");
        SetSystemBrush(SystemColors.ActiveBorderBrushKey, dark ? "#353A43" : "#D5DAE2");
    }

    private static void SetSystemBrush(object key, string color) =>
        Application.Current.Resources[key] = new SolidColorBrush(ParseColor(color, "#000000"));

    private static System.Windows.Media.Color ParseColor(string value, string fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }

    private static System.Windows.Media.Color Blend(System.Windows.Media.Color foreground, string backgroundHex, double amount)
    {
        var background = ParseColor(backgroundHex, "#000000");
        return System.Windows.Media.Color.FromRgb(
            (byte)(foreground.R * amount + background.R * (1 - amount)),
            (byte)(foreground.G * amount + background.G * (1 - amount)),
            (byte)(foreground.B * amount + background.B * (1 - amount)));
    }

    private static double ColorDistance(System.Windows.Media.Color a, System.Windows.Media.Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static System.Windows.Media.Color EnsureContrast(System.Windows.Media.Color foreground, string backgroundHex, bool dark)
    {
        var background = ParseColor(backgroundHex, dark ? "#000000" : "#FFFFFF");
        if (ColorDistance(foreground, background) < 60)
        {
            return (System.Windows.Media.Color)ColorConverter.ConvertFromString(dark ? "#FFFFFF" : "#000000");
        }
        return foreground;
    }
}
