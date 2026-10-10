// 用途：统一处理工作台、设置页和浮层的行/列滚轮滚动。
// 编写日期：2026-10-09
// 作者：Codex（按用户需求修改）

using System.Runtime.CompilerServices;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TableTool.Gui.Models;

namespace TableTool.Gui.Services;

public static class ScrollWheelService
{
    private sealed record ScrollAccumulator(double Vertical, double Horizontal);

    private static readonly ConditionalWeakTable<ScrollViewer, ScrollAccumulator> Accumulators = new();

    public static void Handle(
        MouseWheelEventArgs e,
        DependencyObject? source,
        AppSettings settings,
        DependencyObject? fallback = null)
    {
        var horizontal = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var scrollViewer = FindScrollViewer(source, horizontal) ?? FindScrollViewer(fallback, horizontal);
        if (scrollViewer is null)
            return;

        var configuredStep = horizontal
            ? Math.Clamp(settings.HorizontalWheelScrollStep, 1, 9999)
            : Math.Clamp(settings.VerticalWheelScrollStep, 1, 9999);
        var notches = Math.Clamp(e.Delta / 120d, -3d, 3d);
        var amount = -notches * configuredStep;
        if (Math.Abs(amount) < 0.01)
            return;

        if (scrollViewer.CanContentScroll)
        {
            Accumulators.TryGetValue(scrollViewer, out var current);
            current ??= new ScrollAccumulator(0, 0);
            var verticalTotal = current.Vertical + (horizontal ? 0 : amount);
            var horizontalTotal = current.Horizontal + (horizontal ? amount : 0);
            var verticalLines = (int)Math.Truncate(verticalTotal);
            var horizontalLines = (int)Math.Truncate(horizontalTotal);
            MoveLines(scrollViewer, verticalLines, horizontalLines);
            Accumulators.Remove(scrollViewer);
            Accumulators.Add(scrollViewer, new ScrollAccumulator(
                verticalTotal - verticalLines,
                horizontalTotal - horizontalLines));
        }
        else
        {
            var fontSize = Application.Current.Resources["Size.Interface"] is double size ? size : 13d;
            if (horizontal)
            {
                var unit = Math.Max(36, fontSize * 3.5);
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + amount * unit);
            }
            else
            {
                var unit = Math.Max(22, fontSize * 1.6);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + amount * unit);
            }
        }

        e.Handled = true;
    }

    private static void MoveLines(ScrollViewer scrollViewer, int verticalLines, int horizontalLines)
    {
        if (verticalLines > 0)
        {
            for (var index = 0; index < verticalLines; index++)
                scrollViewer.LineDown();
        }
        else if (verticalLines < 0)
        {
            for (var index = 0; index < -verticalLines; index++)
                scrollViewer.LineUp();
        }

        if (horizontalLines > 0)
        {
            for (var index = 0; index < horizontalLines; index++)
                scrollViewer.LineRight();
        }
        else if (horizontalLines < 0)
        {
            for (var index = 0; index < -horizontalLines; index++)
                scrollViewer.LineLeft();
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject? source, bool horizontal)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer && CanScroll(scrollViewer, horizontal))
                return scrollViewer;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool CanScroll(ScrollViewer scrollViewer, bool horizontal)
    {
        var visibility = horizontal
            ? scrollViewer.HorizontalScrollBarVisibility
            : scrollViewer.VerticalScrollBarVisibility;
        if (visibility == ScrollBarVisibility.Disabled)
            return false;
        return horizontal ? scrollViewer.ScrollableWidth > 0 : scrollViewer.ScrollableHeight > 0;
    }
}
