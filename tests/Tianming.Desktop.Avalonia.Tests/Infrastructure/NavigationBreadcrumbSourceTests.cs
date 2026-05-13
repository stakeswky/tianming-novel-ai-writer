using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class NavigationBreadcrumbSourceTests
{
    private sealed class FakeVm { }
    private sealed class FakeView { }

    private static (NavigationService nav, NavigationBreadcrumbSource src) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm, FakeView>(PageKeys.Welcome);
        reg.Register<FakeVm, FakeView>(PageKeys.Dashboard);
        reg.Register<FakeVm, FakeView>(PageKeys.Settings);

        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();

        var nav = new NavigationService(sp, reg);
        var src = new NavigationBreadcrumbSource(nav);
        return (nav, src);
    }

    [Fact]
    public void Initial_HasRootSegment()
    {
        var (_, src) = Build();
        Assert.Single(src.Current);
        Assert.Equal("天命", src.Current[0].Label);
    }

    [Fact]
    public async System.Threading.Tasks.Task Navigate_AppendsPageSegment()
    {
        var (nav, src) = Build();
        IReadOnlyList<BreadcrumbSegment>? fired = null;
        src.SegmentsChanged += (_, s) => fired = s;

        await nav.NavigateAsync(PageKeys.Welcome);

        Assert.NotNull(fired);
        Assert.Equal(2, fired!.Count);
        Assert.Equal("天命", fired[0].Label);
        Assert.Equal("欢迎", fired[1].Label);
    }

    [Fact]
    public async System.Threading.Tasks.Task Navigate_UnknownLabel_FallsBackToPageKeyId()
    {
        var registry = new PageRegistry();
        registry.Register<FakeVm, FakeView>(new PageKey("uncharted"));
        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();
        var nav2 = new NavigationService(sp, registry);
        var src2 = new NavigationBreadcrumbSource(nav2);

        await nav2.NavigateAsync(new PageKey("uncharted"));

        // 没有内置标签 → 退化到 PageKey.Id
        Assert.Equal("uncharted", src2.Current[1].Label);
    }
}
