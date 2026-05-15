using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

/// <summary>
/// M7 Lane A 外观主题设置 page VM。让用户控制 PortableThemeStateController 的当前主题。
///
/// **设计决策**：不订阅 controller.ThemeChanged 自动覆盖 SelectedTheme。
/// 原因：lib 的 PortableThemeApplicationPlanner 会把 Auto 在 SwitchThemeAsync
/// 内部 resolve 成 Light/Dark（写到 state.CurrentTheme），ThemeChanged 事件的
/// NewTheme 也是 resolved 值。如果 VM 自动同步，用户选 Auto 应用后 ComboBox
/// 会立即回显成 Light/Dark，丢失"用户意图是 Auto"的视觉反馈。
/// 因此 VM 只保留用户的原始 Selected 选择，不自动跟随 controller 的 resolved 状态。
/// </summary>
public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly PortableThemeStateController _controller;

    [ObservableProperty] private PortableThemeType _selectedTheme;

    public ThemeSettingsViewModel(PortableThemeStateController controller)
    {
        _controller = controller;
        _selectedTheme = controller.CurrentTheme;
    }

    [RelayCommand]
    private async Task ApplyThemeAsync()
    {
        // 不与 _controller.CurrentTheme 比较 — Auto 在 controller 内部已 resolve 成
        // Light/Dark，用户连续点"应用"应允许 re-trigger Auto 解析（系统外观可能变了）。
        await _controller.SwitchThemeAsync(SelectedTheme);
    }
}
