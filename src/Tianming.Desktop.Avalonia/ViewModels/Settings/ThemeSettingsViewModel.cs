using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

/// <summary>
/// M7 Lane A 外观主题设置 page VM。让用户控制 PortableThemeStateController 的当前主题。
/// 自动跟随 controller.ThemeChanged 事件（系统跟随 / 调度切换时同步显示）。
/// </summary>
public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly PortableThemeStateController _controller;

    [ObservableProperty] private PortableThemeType _selectedTheme;

    public ThemeSettingsViewModel(PortableThemeStateController controller)
    {
        _controller = controller;
        _selectedTheme = controller.CurrentTheme;
        controller.ThemeChanged += OnControllerThemeChanged;
    }

    private void OnControllerThemeChanged(object? sender, PortableThemeChangedEventArgs args)
    {
        SelectedTheme = args.NewTheme;
    }

    [RelayCommand]
    private async Task ApplyThemeAsync()
    {
        if (SelectedTheme == _controller.CurrentTheme)
            return;
        await _controller.SwitchThemeAsync(SelectedTheme);
    }
}
