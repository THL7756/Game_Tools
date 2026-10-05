// 用途：实现 Excel Diff 主界面的文件选择、比较和双向替换操作。
// 编写时间：2026-10-05。
// 作者：Codex。
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using TableDiff.Core.Models;
using TableDiff.Core.Services;
using TableDiff.Gui.ViewModels;

namespace TableDiff.Gui;

public partial class MainWindow : Window
{
    private ExcelDiffResult? _diff;

    public MainWindow()
    {
        InitializeComponent();
        DiffGrid.ItemsSource = Entries;
    }

    private ObservableCollection<DiffEntryViewModel> Entries { get; } = [];

    private void BrowseLeft_Click(object sender, RoutedEventArgs e)
    {
        LeftPathBox.Text = PickExcelFile() ?? LeftPathBox.Text;
    }

    private void BrowseRight_Click(object sender, RoutedEventArgs e)
    {
        RightPathBox.Text = PickExcelFile() ?? RightPathBox.Text;
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(LeftPathBox.Text) || !File.Exists(RightPathBox.Text))
        {
            MessageBox.Show("请选择存在的左右 Excel 文件。", "TableDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _diff = ExcelDiffEngine.Compare(LeftPathBox.Text, RightPathBox.Text, new ExcelDiffOptions { KeyColumn = KeyColumnBox.Text });
            Entries.Clear();
            foreach (var entry in _diff.Entries)
            {
                Entries.Add(new DiffEntryViewModel(entry));
            }

            StatusText.Text = $"新增 {_diff.AddedCount}，删除 {_diff.RemovedCount}，修改 {_diff.ModifiedCount}。勾选变更后可以双向替换。";
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "比较失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyLeftToRight_Click(object sender, RoutedEventArgs e)
    {
        Apply(ApplyDirection.LeftToRight);
    }

    private void ApplyRightToLeft_Click(object sender, RoutedEventArgs e)
    {
        Apply(ApplyDirection.RightToLeft);
    }

    private void Apply(ApplyDirection direction)
    {
        if (_diff is null)
        {
            MessageBox.Show("请先完成比较。", "TableDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selected = Entries.Where(item => item.IsSelected).Select(item => item.Entry).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show("请至少选择一条变更。", "TableDiff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = ExcelPatchApplier.Apply(LeftPathBox.Text, RightPathBox.Text, _diff, direction, selected);
            if (!result.Succeeded)
            {
                MessageBox.Show("替换被阻止：目标文件已经发生变化。\n" + string.Join("\n", result.Conflicts), "冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"已应用 {result.AppliedCount} 条变更。\n备份：{result.BackupPath}", "替换完成", MessageBoxButton.OK, MessageBoxImage.Information);
            Compare_Click(this, new RoutedEventArgs());
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "替换失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? PickExcelFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel 文件|*.xlsx;*.xls;*.xlsm|所有文件|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
