// 用途：为 WPF DataGrid 提供可勾选的 Diff 条目模型。
// 编写时间：2026-10-05。
// 作者：Codex。
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableDiff.Core.Models;

namespace TableDiff.Gui.ViewModels;

public sealed class DiffEntryViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public DiffEntryViewModel(DiffEntry entry)
    {
        Entry = entry;
    }

    public DiffEntry Entry { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string Kind => Entry.Kind.ToString();

    public string SheetName => Entry.SheetName;

    public string RowKey => Entry.RowKey;

    public string ColumnName => Entry.ColumnName;

    public string LeftValue => Entry.LeftValue ?? "";

    public string RightValue => Entry.RightValue ?? "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
