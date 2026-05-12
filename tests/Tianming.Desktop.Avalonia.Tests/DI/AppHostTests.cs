using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class AppHostTests
{
    [Fact]
    public void Build_ResolvesNavigationService()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var nav = sp.GetRequiredService<INavigationService>();
        Assert.NotNull(nav);
    }

    [Fact]
    public void Build_ResolvesMainWindowViewModel()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var vm = sp.GetRequiredService<MainWindowViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void Build_RegistersAllM3Pages()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var reg = sp.GetRequiredService<PageRegistry>();
        Assert.Contains(PageKeys.Welcome,   reg.Keys);
        Assert.Contains(PageKeys.Dashboard, reg.Keys);
        Assert.Contains(PageKeys.Settings,  reg.Keys);
    }
}
