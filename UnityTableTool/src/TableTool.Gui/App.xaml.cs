// 用途：注册桌面应用的全局异常记录，避免页面切换异常直接关闭程序。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TableTool.Gui.Services;

namespace TableTool.Gui;

public partial class App : System.Windows.Application
{
    private static readonly object LogLock = new();
    private static readonly DependencyProperty MoveCaretToEndAfterClickProperty =
        DependencyProperty.RegisterAttached(
            "MoveCaretToEndAfterClick",
            typeof(bool),
            typeof(App),
            new PropertyMetadata(false));

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
        ToolLogService.Info("系统", "工具已启动");
    }

    internal static void LogUiError(string context, Exception error) =>
        WriteError(context, error, handled: true);

    private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        MoveCaretToEnd(sender);

    private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.IsKeyboardFocused)
            textBox.SetValue(MoveCaretToEndAfterClickProperty, true);
    }

    private void TextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox
            || textBox.GetValue(MoveCaretToEndAfterClickProperty) is not bool moveCaretToEnd
            || !moveCaretToEnd)
        {
            return;
        }

        textBox.ClearValue(MoveCaretToEndAfterClickProperty);
        MoveCaretToEnd(textBox);
    }

    private static void MoveCaretToEnd(object sender)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectionLength = 0;
            textBox.CaretIndex = textBox.Text.Length;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteError("UI线程未处理异常", e.Exception, handled: true);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception error)
            WriteError("应用程序未处理异常", error, handled: false);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteError("后台任务未处理异常", e.Exception, handled: true);
        e.SetObserved();
    }

    private static void WriteError(string context, Exception error, bool handled)
    {
        ToolLogService.Error("系统", $"{context}：{error.Message}");
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityTableTool",
                "ui-errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var builder = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context} handled={handled}")
                .AppendLine(error.ToString());
            lock (LogLock)
                File.AppendAllText(path, builder.ToString());
        }
        catch
        {
            // 异常记录失败时不能再次抛出异常影响程序退出流程。
        }
    }
}
