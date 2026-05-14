using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class ThemeFollowSystemViewModelTests
{
    [Fact]
    public void Ctor_exposes_followsystem_status_and_schedule_placeholders()
    {
        // 简化 Lane A MVP：
        // - Lane 0 已让 MacOSSystemAppearanceMonitor → PortableSystemFollowController →
        //   ThemeBridge 链路在启动时自动接通，无 user toggle API（lib 层缺）
        // - FileTimeBasedThemeSettingsStore 尚未 DI 注册，定时主题 deferred
        // 当前 VM 仅暴露状态说明文案 + 占位字段，让 SettingsShell 子导航完整可用
        var vm = new ThemeFollowSystemViewModel();

        Assert.False(string.IsNullOrWhiteSpace(vm.FollowSystemStatusText));
        Assert.False(string.IsNullOrWhiteSpace(vm.ScheduleStatusText));
    }
}
