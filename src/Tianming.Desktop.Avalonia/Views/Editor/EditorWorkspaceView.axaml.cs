using Avalonia.Controls;
using Avalonia.Interactivity;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Editor;

namespace Tianming.Desktop.Avalonia.Views.Editor;

public partial class EditorWorkspaceView : UserControl
{
    public EditorWorkspaceView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is EditorWorkspaceViewModel vm)
        {
            // 从 NavigationService 读取导航参数（chapterId）
            if (App.Services?.GetService(typeof(INavigationService)) is INavigationService nav
                && nav.LastParameter is string chapterId)
            {
                vm.OpenChapter(chapterId);
            }
        }
    }
}
