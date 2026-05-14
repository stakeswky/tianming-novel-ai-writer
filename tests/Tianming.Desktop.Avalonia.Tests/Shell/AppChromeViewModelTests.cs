using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Shell;

public class AppChromeViewModelTests
{
    private sealed class FakeVm { }
    private sealed class FakeView { }

    private static (NavigationService nav, AppChromeViewModel vm) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm, FakeView>(PageKeys.Welcome, "欢迎");
        reg.Register<FakeVm, FakeView>(PageKeys.Dashboard, "仪表盘");
        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();
        var nav = new NavigationService(sp, reg);
        var src = new NavigationBreadcrumbSource(nav, reg);
        return (nav, new AppChromeViewModel(src, nav));
    }

    [Fact]
    public void Initial_SegmentsHasRoot()
    {
        var (_, vm) = Build();
        Assert.Single(vm.Segments);
        Assert.Equal("天命", vm.Segments[0].Label);
    }

    [Fact]
    public async Task Navigate_UpdatesSegments()
    {
        var (nav, vm) = Build();
        await nav.NavigateAsync(PageKeys.Dashboard);
        Assert.Equal(2, vm.Segments.Count);
        Assert.Equal("仪表盘", vm.Segments[1].Label);
    }

    [Fact]
    public async Task NavigateCommand_TargetNull_NoOp()
    {
        var (_, vm) = Build();
        await vm.NavigateCommand.ExecuteAsync(new BreadcrumbSegment("天命", null));
        Assert.Single(vm.Segments);
    }
}
