using System;
using System.Collections.Generic;
using System.Linq;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class SettingsShellViewModelTests
{
    [Fact]
    public void Ctor_exposes_theme_and_followsystem_subnav_entries()
    {
        var vm = new SettingsShellViewModel();

        Assert.NotEmpty(vm.SubNavItems);
        Assert.Contains(vm.SubNavItems, i => i.Key == PageKeys.SettingsTheme);
        Assert.Contains(vm.SubNavItems, i => i.Key == PageKeys.SettingsFollowSystem);
    }

    [Fact]
    public void Selecting_subnav_item_updates_SelectedItem()
    {
        var vm = new SettingsShellViewModel();
        var followItem = vm.SubNavItems.First(i => i.Key == PageKeys.SettingsFollowSystem);

        vm.SelectedItem = followItem;

        Assert.Equal(PageKeys.SettingsFollowSystem, vm.SelectedItem!.Key);
    }

    [Fact]
    public void Ctor_defaults_SelectedItem_to_theme()
    {
        var vm = new SettingsShellViewModel();

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal(PageKeys.SettingsTheme, vm.SelectedItem!.Key);
    }

    [Fact]
    public void Selecting_theme_subnav_resolves_ThemeSettingsViewModel_via_PageRegistry()
    {
        // 构造一个真 PageRegistry 注册了 SettingsTheme → StubSubVm，
        // IServiceProvider stub 解析 StubSubVm 返回 sentinel 实例。
        var pages = new PageRegistry();
        pages.Register<StubSubVm, object>(PageKeys.SettingsTheme, "外观主题");
        pages.Register<StubFollowVm, object>(PageKeys.SettingsFollowSystem, "跟随系统");
        var stubTheme  = new StubSubVm();
        var stubFollow = new StubFollowVm();
        var sp = new StubServiceProvider(new()
        {
            { typeof(StubSubVm),    stubTheme },
            { typeof(StubFollowVm), stubFollow },
        });

        var vm = new SettingsShellViewModel(pages, sp);

        // 默认 SelectedItem = SettingsTheme → CurrentPageViewModel 应解析为 stubTheme
        Assert.Same(stubTheme, vm.CurrentPageViewModel);

        // 切到 SettingsFollowSystem → CurrentPageViewModel 同步切
        var followItem = vm.SubNavItems.First(i => i.Key == PageKeys.SettingsFollowSystem);
        vm.SelectedItem = followItem;
        Assert.Same(stubFollow, vm.CurrentPageViewModel);
    }

    private sealed class StubSubVm { }
    private sealed class StubFollowVm { }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _map;
        public StubServiceProvider(Dictionary<Type, object> map) => _map = map;
        public object? GetService(Type t) => _map.TryGetValue(t, out var v) ? v : null;
    }
}
