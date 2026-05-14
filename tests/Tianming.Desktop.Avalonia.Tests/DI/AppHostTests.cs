using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.ViewModels;
using TM.Framework.Appearance;
using TM.Framework.Notifications;
using TM.Framework.Platform;
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
        Assert.NotNull(vm.Chrome);
        Assert.NotNull(vm.StatusBar);
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

    [Fact]
    public void Build_ResolvesAllInfraProbes()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<IRuntimeInfoProvider>());
        Assert.NotNull(sp.GetRequiredService<IBreadcrumbSource>());
        Assert.NotNull(sp.GetRequiredService<IKeychainHealthProbe>());
        Assert.NotNull(sp.GetRequiredService<IOnnxHealthProbe>());
    }

    [Fact]
    public async Task Build_ResolvesMacOSPlatformSinks()
    {
        await using var sp = (ServiceProvider)AppHost.Build();
        Assert.IsType<MacOSSystemAppearanceMonitor>(
            sp.GetRequiredService<IPortableSystemAppearanceMonitor>());
        Assert.NotNull(sp.GetRequiredService<PortableSystemFollowRuntime>());
        Assert.IsType<MacOSNotificationSink>(
            sp.GetRequiredService<IPortableNotificationSink>());
        Assert.NotNull(sp.GetRequiredService<PortableNotificationDispatcher>());
        Assert.IsType<MacOSSpeechOutput>(
            sp.GetRequiredService<IPortableSpeechOutput>());
        Assert.NotNull(sp.GetRequiredService<IPortableNotificationSoundPlayer>());
    }

    [Fact]
    public void Build_ResolvesShellVms()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<AppChromeViewModel>());
        Assert.NotNull(sp.GetRequiredService<AppStatusBarViewModel>());
    }

    [Fact]
    public void Build_ResolvesSystemProxyService_AndHttpClient()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<IPortableSystemProxyService>());
        Assert.NotNull(sp.GetRequiredService<AvaloniaSystemHttpProxy>());
        Assert.NotNull(sp.GetRequiredService<IHttpClientFactory>());
        Assert.NotNull(sp.GetRequiredService<HttpClient>());
    }
}
