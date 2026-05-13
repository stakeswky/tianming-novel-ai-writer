using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels;

/// <summary>
/// 验证 M5 NativeMenu 真实绑定的 6 个命令（关于 / 偏好 / 新建 / 打开 / 保存 / 退出）
/// 在 MainWindowViewModel 上正确生成（CommunityToolkit.Mvvm 源生成器），且能执行。
/// </summary>
public class MainWindowViewModelMenuTests
{
    [Fact]
    public async Task OpenPreferences_NavigatesToSettings()
    {
        var nav = new FakeNavigationService();
        var vm = BuildViewModel(nav);

        await vm.OpenPreferencesCommand.ExecuteAsync(null);

        Assert.Equal(PageKeys.Settings, nav.LastNavigatedKey);
    }

    [Fact]
    public async Task NewProject_NavigatesToWelcome()
    {
        var nav = new FakeNavigationService();
        var vm = BuildViewModel(nav);

        await vm.NewProjectCommand.ExecuteAsync(null);

        Assert.Equal(PageKeys.Welcome, nav.LastNavigatedKey);
    }

    [Fact]
    public async Task OpenProject_NavigatesToWelcome()
    {
        var nav = new FakeNavigationService();
        var vm = BuildViewModel(nav);

        await vm.OpenProjectCommand.ExecuteAsync(null);

        Assert.Equal(PageKeys.Welcome, nav.LastNavigatedKey);
    }

    [Fact]
    public void Save_IsStub_DoesNotThrow()
    {
        var vm = BuildViewModel(new FakeNavigationService());
        vm.SaveCommand.Execute(null);
    }

    [Fact]
    public void About_DoesNotThrow()
    {
        var vm = BuildViewModel(new FakeNavigationService());
        vm.AboutCommand.Execute(null);
    }

    [Fact]
    public void Quit_NoLifecycle_DoesNotThrow()
    {
        // Avalonia.Application.Current 在测试上下文为 null，命令应静默返回
        var vm = BuildViewModel(new FakeNavigationService());
        vm.QuitCommand.Execute(null);
    }

    [Fact]
    public void AllMenuCommands_AreNotNull()
    {
        var vm = BuildViewModel(new FakeNavigationService());

        Assert.NotNull(vm.AboutCommand);
        Assert.NotNull(vm.OpenPreferencesCommand);
        Assert.NotNull(vm.NewProjectCommand);
        Assert.NotNull(vm.OpenProjectCommand);
        Assert.NotNull(vm.SaveCommand);
        Assert.NotNull(vm.QuitCommand);
    }

    private static MainWindowViewModel BuildViewModel(INavigationService nav)
    {
        // 构造一组最简 stub 满足 MainWindowViewModel ctor 链
        var tmpRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "tianming-test-" + Guid.NewGuid().ToString("N"));
        var windowStore = new WindowStateStore(System.IO.Path.Combine(tmpRoot, "window.json"));
        var leftNav = new LeftNavViewModel(nav);
        var rightPanel = new RightConversationViewModel();
        var layout = new ThreeColumnLayoutViewModel(windowStore, nav, leftNav, rightPanel);
        var breadcrumb = new NavigationBreadcrumbSource(nav);
        var chrome = new AppChromeViewModel(breadcrumb, nav);
        var statusBar = new AppStatusBarViewModel(
            new StubRuntimeInfo(),
            new StubProbe(),
            new StubProbe());
        return new MainWindowViewModel(layout, chrome, statusBar, nav);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public PageKey? LastNavigatedKey { get; private set; }
        public PageKey? CurrentKey => LastNavigatedKey;
        public object? CurrentViewModel => null;
        public bool CanGoBack => false;
        public event EventHandler<PageKey>? CurrentKeyChanged;

        public Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default)
        {
            LastNavigatedKey = key;
            CurrentKeyChanged?.Invoke(this, key);
            return Task.CompletedTask;
        }

        public Task GoBackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubRuntimeInfo : IRuntimeInfoProvider
    {
        public string FrameworkDescription => "stub-net8";
        public bool IsLocalMode => true;
    }

    private sealed class StubProbe : IKeychainHealthProbe, IOnnxHealthProbe
    {
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
            => Task.FromResult(new StatusIndicator("stub", StatusKind.Success));
    }
}
