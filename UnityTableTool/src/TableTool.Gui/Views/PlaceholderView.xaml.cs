// 用途：展示尚未实现的工作台模块占位页面。
// 编写日期：2026-10-06
// 作者：Codex（按用户需求修改）

using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TableTool.Gui.Views;

public partial class PlaceholderView : System.Windows.Controls.UserControl
{
    public PlaceholderView(string title, string description, string iconName)
    {
        InitializeComponent();
        TitleText.Text = title;
        DescriptionText.Text = description;
        try
        {
            var resource = Application.GetResourceStream(
                new Uri($"/UnityTableTool;component/Assets/Icons/{iconName}", UriKind.Relative));
            if (resource is null)
            {
                ModuleIcon.Visibility = Visibility.Collapsed;
                return;
            }

            using (resource.Stream)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = resource.Stream;
                image.EndInit();
                image.Freeze();
                ModuleIcon.Source = image;
            }
        }
        catch (Exception)
        {
            // 占位页不能因为图标资源缺失而影响模块切换。
            ModuleIcon.Visibility = Visibility.Collapsed;
        }
    }
}
