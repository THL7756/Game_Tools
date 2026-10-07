// 用途：提供主题适配的打表结果弹窗，替代系统 MessageBox。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System.Windows;
using System.Windows.Media.Imaging;

namespace TableTool.Gui.Views;

public partial class BuildResultDialog : Window
{
    public BuildResultDialog(bool success)
    {
        InitializeComponent();

        var title = success ? "打表成功" : "打表失败";
        ResultTitle.Text = title;
        ResultMessage.Visibility = Visibility.Collapsed;
        CloseButton.Content = "确定";
        ResultIcon.Source = new BitmapImage(new Uri(
            success
                ? "pack://application:,,,/UnityTableTool;component/Assets/Icons/check.png"
                : "pack://application:,,,/UnityTableTool;component/Assets/Icons/triangle-alert.png"));
        Title = ResultTitle.Text;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
