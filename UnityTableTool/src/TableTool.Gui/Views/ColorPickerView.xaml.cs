// 用途：实现主题颜色的 HSV/HEX 取色、即时预览、提交和取消交互。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TableTool.Gui.Views;

public partial class ColorPickerView : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(Color),
        typeof(ColorPickerView),
        new PropertyMetadata(Colors.White, ColorChanged));

    private double hue;
    private double saturation;
    private double value;
    private Color originalColor;
    private bool isChangingColor;
    private bool isCanceling;
    private bool isDraggingSaturationValue;
    private bool isDraggingHue;

    public ColorPickerView()
    {
        InitializeComponent();
        UpdateFromColor(Color);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public string ColorKey { get; set; } = string.Empty;

    public event EventHandler? PreviewChanged;
    public event EventHandler? Committed;
    public event EventHandler? Canceled;

    public void SetHex(string value)
    {
        if (!TryParseHex(value, out var color))
            return;
        Color = color;
    }

    public string GetHex() => ToHex(Color);

    private static void ColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPickerView picker)
            return;
        picker.UpdateFromColor((Color)e.NewValue);
        if (!picker.isChangingColor && picker.PickerPopup.IsOpen)
            picker.PreviewChanged?.Invoke(picker, EventArgs.Empty);
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        originalColor = Color;
        isCanceling = false;
        PickerPopup.IsOpen = true;
        HexBox.Focus();
        HexBox.SelectAll();
    }

    private void PickerPopup_Closed(object? sender, EventArgs e)
    {
        if (isCanceling)
        {
            isCanceling = false;
            SetColorWithoutPreview(originalColor);
            Canceled?.Invoke(this, EventArgs.Empty);
            return;
        }

        Committed?.Invoke(this, EventArgs.Empty);
    }

    private void SaturationValueCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        isDraggingSaturationValue = true;
        SaturationValueCanvas.CaptureMouse();
        UpdateSaturationValue(e.GetPosition(SaturationValueCanvas));
    }

    private void SaturationValueCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDraggingSaturationValue && e.LeftButton == MouseButtonState.Pressed)
            UpdateSaturationValue(e.GetPosition(SaturationValueCanvas));
    }

    private void SaturationValueCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        isDraggingSaturationValue = false;
        SaturationValueCanvas.ReleaseMouseCapture();
    }

    private void HueCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        isDraggingHue = true;
        HueCanvas.CaptureMouse();
        UpdateHue(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDraggingHue && e.LeftButton == MouseButtonState.Pressed)
            UpdateHue(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        isDraggingHue = false;
        HueCanvas.ReleaseMouseCapture();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isChangingColor || !HexBox.IsKeyboardFocusWithin || !TryParseHex(HexBox.Text, out var color))
            return;
        SetColorWithoutPreview(color);
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HexBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PickerPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            isCanceling = true;
            PickerPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void UpdateSaturationValue(Point point)
    {
        saturation = Math.Clamp(point.X / SaturationValueCanvas.ActualWidth, 0, 1);
        value = 1 - Math.Clamp(point.Y / SaturationValueCanvas.ActualHeight, 0, 1);
        UpdateColorFromHsv();
    }

    private void UpdateHue(Point point)
    {
        hue = Math.Clamp(point.X / HueCanvas.ActualWidth, 0, 1) * 360;
        UpdateColorFromHsv();
    }

    private void UpdateColorFromHsv()
    {
        SetColorWithoutPreview(HsvToColor(hue, saturation, value));
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFromColor(Color color)
    {
        (hue, saturation, value) = RgbToHsv(color);
        isChangingColor = true;
        SwatchBorder.Background = new SolidColorBrush(color);
        PreviewBorder.Background = new SolidColorBrush(color);
        HexSummary.Text = ToHex(color);
        HexBox.Text = ToHex(color);
        SaturationValueBase.Background = new SolidColorBrush(HsvToColor(hue, 1, 1));
        Canvas.SetLeft(SaturationValueThumb, saturation * SaturationValueCanvas.Width - SaturationValueThumb.Width / 2);
        Canvas.SetTop(SaturationValueThumb, (1 - value) * SaturationValueCanvas.Height - SaturationValueThumb.Height / 2);
        Canvas.SetLeft(HueThumb, hue / 360 * HueCanvas.Width - HueThumb.Width / 2);
        isChangingColor = false;
    }

    private void SetColorWithoutPreview(Color color)
    {
        isChangingColor = true;
        Color = color;
        isChangingColor = false;
    }

    private static bool TryParseHex(string? text, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        try
        {
            var parsed = ColorConverter.ConvertFromString(text.Trim());
            if (parsed is Color converted && converted.A == byte.MaxValue)
            {
                color = converted;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var match = value - chroma;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromRgb(ToByte((red + match) * 255), ToByte((green + match) * 255), ToByte((blue + match) * 255));
    }

    private static (double Hue, double Saturation, double Value) RgbToHsv(Color color)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        var hue = delta == 0
            ? 0
            : max == red
                ? 60 * ((green - blue) / delta % 6)
                : max == green
                    ? 60 * ((blue - red) / delta + 2)
                    : 60 * ((red - green) / delta + 4);
        if (hue < 0)
            hue += 360;
        return (hue, max == 0 ? 0 : delta / max, max);
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
