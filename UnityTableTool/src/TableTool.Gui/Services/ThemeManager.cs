// 用途：根据设置更新应用颜色、字体和缩放资源。
// 最近修改日期：2026-10-06

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
            SetBrush("Brush.Window", ParseColor(settings.BackgroundColor, "#17191D"));
            SetBrush("Brush.Chrome", "#1B1E23");
            SetBrush("Brush.Panel", "#1B1E23");
            SetBrush("Brush.Surface", "#202329");
            SetBrush("Brush.SurfaceAlt", "#2D323B");
            SetBrush("Brush.SurfaceHover", "#272B32");
            SetBrush("Brush.Border", "#353A43");
            SetBrush("Brush.Text", ParseColor(settings.ForegroundColor, "#E2E6ED"));
            SetBrush("Brush.TextSecondary", "#939DAD");
            SetBrush("Brush.TextMuted", "#667181");
            SetBrush("Brush.RowSelected", "#3A3321");
            SetBrush("Brush.Overlay", "#111317");
        }
        else
        {
            SetBrush("Brush.Window", ParseColor(settings.BackgroundColor, "#F5F7FA"));
            SetBrush("Brush.Chrome", "#FFFFFF");
            SetBrush("Brush.Panel", "#FFFFFF");
            SetBrush("Brush.Surface", "#F8FAFC");
            SetBrush("Brush.SurfaceAlt", "#E9EDF3");
            SetBrush("Brush.SurfaceHover", "#EDF2F7");
            SetBrush("Brush.Border", "#D5DAE2");
            SetBrush("Brush.Text", ParseColor(settings.ForegroundColor, "#17191D"));
            SetBrush("Brush.TextSecondary", "#5E6878");
            SetBrush("Brush.TextMuted", "#7C8798");
            SetBrush("Brush.RowSelected", "#FFF1C9");
            SetBrush("Brush.Overlay", "#E8ECF2");
        }

        SetBrush("Brush.Success", dark ? "#54D6A0" : "#16845B");
        SetBrush("Brush.Warning", dark ? "#E7B95E" : "#A36B00");
        SetBrush("Brush.Error", dark ? "#FF7B7B" : "#C53A3A");
        Application.Current.Resources["Font.Interface"] = new FontFamily(settings.FontFamilyName);
        Application.Current.Resources["Font.Mono"] = new FontFamily("Consolas, Cascadia Mono");
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
}
