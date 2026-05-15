using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Framework.Appearance;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class ThemeSettingsViewModelTests
{
    [Fact]
    public void Ctor_loads_current_theme_from_controller()
    {
        var (controller, _) = BuildController(PortableThemeType.Dark);
        var vm = new ThemeSettingsViewModel(controller);

        Assert.Equal(PortableThemeType.Dark, vm.SelectedTheme);
    }

    [Fact]
    public async Task ApplyTheme_invokes_controller_switch_when_value_changes()
    {
        var (controller, applyCalls) = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        vm.SelectedTheme = PortableThemeType.Dark;
        await vm.ApplyThemeCommand.ExecuteAsync(null);

        Assert.Equal(PortableThemeType.Dark, controller.CurrentTheme);
        Assert.Single(applyCalls);
    }

    [Fact]
    public async Task ApplyTheme_skips_lib_short_circuit_when_value_unchanged()
    {
        var (controller, applyCalls) = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        // 不改 SelectedTheme，直接 apply。VM 不再前置短路（删除是为了让 Auto 支持
        // re-trigger 解析），但 lib PortableThemeStateController.SwitchThemeAsync 内部
        // 在 plan.ThemeType == _state.CurrentTheme 时短路，不会触发实际 apply。
        await vm.ApplyThemeCommand.ExecuteAsync(null);

        Assert.Empty(applyCalls);
    }

    [Fact]
    public async Task Apply_Auto_then_SelectedTheme_stays_Auto_in_VM()
    {
        // 回归测试：之前 VM 订阅 controller.ThemeChanged 自动覆盖 SelectedTheme，
        // 选 Auto 应用后 controller 把 Auto resolve 成 Light/Dark，ThemeChanged 事件
        // 把 SelectedTheme 改回 Light → UI 回显错误（巡检 P2）。
        // 修复后 VM 不订阅 controller.ThemeChanged，SelectedTheme 保持用户选择的 Auto。
        var (controller, _) = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        vm.SelectedTheme = PortableThemeType.Auto;
        await vm.ApplyThemeCommand.ExecuteAsync(null);

        Assert.Equal(PortableThemeType.Auto, vm.SelectedTheme);
        // controller 内部把 Auto resolve 成 Light/Dark（lib 行为），但 VM 不跟随
        Assert.NotEqual(PortableThemeType.Auto, controller.CurrentTheme);
    }

    private static (PortableThemeStateController controller, List<PortableThemeApplicationRequest> applyCalls) BuildController(PortableThemeType initial)
    {
        var state = new PortableThemeState { CurrentTheme = initial };
        var applyCalls = new List<PortableThemeApplicationRequest>();
        var controller = new PortableThemeStateController(
            state,
            req =>
            {
                applyCalls.Add(req);
                return Task.CompletedTask;
            });
        return (controller, applyCalls);
    }
}
