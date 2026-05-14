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
}
