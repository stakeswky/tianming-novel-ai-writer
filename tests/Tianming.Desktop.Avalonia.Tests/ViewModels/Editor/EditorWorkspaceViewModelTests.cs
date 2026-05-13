using System;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.ViewModels.Editor;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Editor;

public class EditorWorkspaceViewModelTests
{
    [Fact]
    public void New_workspace_has_one_default_tab()
    {
        var vm = NewWorkspace();
        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.ActiveTab);
    }

    [Fact]
    public void Default_tab_chapterId_and_title_are_set()
    {
        var vm = NewWorkspace();
        Assert.NotNull(vm.ActiveTab);
        Assert.False(string.IsNullOrEmpty(vm.ActiveTab!.ChapterId));
        Assert.False(string.IsNullOrEmpty(vm.ActiveTab!.Title));
    }

    [Fact]
    public void AddTab_appends_and_activates()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal("ch-2", vm.ActiveTab!.ChapterId);
    }

    [Fact]
    public void CloseTab_removes_and_activates_neighbor()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        vm.AddTab("ch-3", "第 3 章");
        Assert.Equal("ch-3", vm.ActiveTab!.ChapterId);

        vm.CloseTab(vm.ActiveTab);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.NotNull(vm.ActiveTab);
    }

    [Fact]
    public void ActivateTab_switches_active()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        var first = vm.Tabs[0];
        vm.ActivateTab(first);
        Assert.Same(first, vm.ActiveTab);
    }

    private static EditorWorkspaceViewModel NewWorkspace()
    {
        var store = new FakeDraftStore();
        var fake = new FakeTimerScheduler();
        var sch = new AutoSaveScheduler(fake, TimeSpan.FromMilliseconds(50));
        return new EditorWorkspaceViewModel("proj-test", store, sch);
    }
}
