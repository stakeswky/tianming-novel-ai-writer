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
    public async Task ApplyTheme_skips_when_value_unchanged()
    {
        var (controller, applyCalls) = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        // 不改 SelectedTheme，直接 apply
        await vm.ApplyThemeCommand.ExecuteAsync(null);

        Assert.Empty(applyCalls); // controller 内部短路（CurrentTheme == plan.ThemeType）
    }

    [Fact]
    public async Task Controller_ThemeChanged_updates_SelectedTheme()
    {
        var (controller, _) = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        // 模拟外部（如 SystemFollow / 调度服务）切换主题
        await controller.SwitchThemeAsync(PortableThemeType.Dark);

        Assert.Equal(PortableThemeType.Dark, vm.SelectedTheme);
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
