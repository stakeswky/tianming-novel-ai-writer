using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Navigation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Navigation;

public class NavigationServiceTests
{
    private sealed class FakeVm1 { }
    private sealed class FakeVm2 { }
    private sealed class FakeView { }

    private static (NavigationService nav, PageRegistry reg) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm1, FakeView>(PageKeys.Welcome);
        reg.Register<FakeVm2, FakeView>(PageKeys.Dashboard);
        var services = new ServiceCollection();
        services.AddTransient<FakeVm1>();
        services.AddTransient<FakeVm2>();
        var sp = services.BuildServiceProvider();
        return (new NavigationService(sp, reg), reg);
    }

    [Fact]
    public async Task Navigate_ResolvesVmFromDI()
    {
        var (nav, _) = Build();
        await nav.NavigateAsync(PageKeys.Welcome);
        Assert.Equal(PageKeys.Welcome, nav.CurrentKey);
        Assert.IsType<FakeVm1>(nav.CurrentViewModel);
    }

    [Fact]
    public async Task Navigate_FiresEvent()
    {
        var (nav, _) = Build();
        PageKey? fired = null;
        nav.CurrentKeyChanged += (_, k) => fired = k;
        await nav.NavigateAsync(PageKeys.Welcome);
        Assert.Equal(PageKeys.Welcome, fired);
    }

    [Fact]
    public async Task GoBack_RestoresPrevious()
    {
        var (nav, _) = Build();
        await nav.NavigateAsync(PageKeys.Welcome);
        await nav.NavigateAsync(PageKeys.Dashboard);
        Assert.True(nav.CanGoBack);
        await nav.GoBackAsync();
        Assert.Equal(PageKeys.Welcome, nav.CurrentKey);
        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public async Task Navigate_UnknownKey_Throws()
    {
        var (nav, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => nav.NavigateAsync(new PageKey("unknown")));
    }
}
