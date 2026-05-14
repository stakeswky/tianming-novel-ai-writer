using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

/// <summary>
/// M7 Lane A "跟随系统" 设置 page VM（MVP）。
///
/// 当前 Lane 0 已在 AvaloniaShellServiceCollectionExtensions 中接通：
///   MacOSSystemAppearanceMonitor → PortableSystemFollowController → ThemeBridge.ApplyAsync
/// 该链路在 app 启动时自动启用，无用户 toggle API（lib 层 PortableSystemFollowController
/// 仅暴露 HandleAppearanceChangedAsync）。
///
/// 定时主题（PortableTimeBasedThemeSettings + FileTimeBasedThemeSettingsStore）lib
/// 已端口但尚未 DI 注册，用户控制 UI 占位等待后续 milestone。
///
/// 本 VM 仅暴露状态文案，让 SettingsShell 子导航完整可点；真实 enable/disable
/// toggle 与 schedule 编辑器留给后续 sub-plan（待 lib 层补 controller toggle API
/// + DI 注册 FileTimeBasedThemeSettingsStore 后展开）。
/// </summary>
public partial class ThemeFollowSystemViewModel : ObservableObject
{
    [ObservableProperty] private string _followSystemStatusText =
        "跟随系统外观：已启用（macOS 系统外观变化时自动切换 Light / Dark）。";

    [ObservableProperty] private string _scheduleStatusText =
        "定时主题：未启用。后续 milestone 会提供基于时间窗 / 节假日 / 星期的自动切换配置。";
}
