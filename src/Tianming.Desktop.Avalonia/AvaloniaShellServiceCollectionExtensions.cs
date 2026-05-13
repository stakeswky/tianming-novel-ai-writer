using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;
using TM.Services.Framework.AI.SemanticKernel;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.Shell;

namespace Tianming.Desktop.Avalonia;

public static class AvaloniaShellServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaShell(this IServiceCollection s)
    {
        // Infra
        s.AddSingleton(AppPaths.Default);
        s.AddSingleton(sp => new WindowStateStore(
            System.IO.Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "window_state.json")));
        s.AddSingleton<AppLifecycle>();
        s.AddSingleton<DispatcherScheduler>();

        // M5：系统代理 → HttpClient 装配
        // AI 命名空间所有出站 HTTP 都走这个 named client，自动读 macOS 系统代理设置。
        s.AddSingleton<AvaloniaSystemHttpProxy>();
        s.AddHttpClient("tianming")
            .ConfigurePrimaryHttpMessageHandler(sp => new System.Net.Http.SocketsHttpHandler
            {
                Proxy = sp.GetRequiredService<AvaloniaSystemHttpProxy>(),
                UseProxy = true,
            });
        s.AddSingleton<System.Net.Http.HttpClient>(sp =>
            sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("tianming"));

        // Theme
        s.AddSingleton<PortableThemeState>(_ => new PortableThemeState());
        s.AddSingleton<PortableThemeStateController>(sp =>
        {
            var state = sp.GetRequiredService<PortableThemeState>();
            var bridge = sp.GetRequiredService<ThemeBridge>();
            return new PortableThemeStateController(state, bridge.ApplyAsync);
        });
        s.AddSingleton<ThemeBridge>();

        // Navigation
        s.AddSingleton<PageRegistry>(_ => RegisterPages(new PageRegistry()));
        s.AddSingleton<INavigationService, NavigationService>();

        // Infra probes / runtime
        s.AddSingleton<IRuntimeInfoProvider, RuntimeInfoProvider>();
        s.AddSingleton<IBreadcrumbSource, NavigationBreadcrumbSource>();
        s.AddSingleton<IKeychainHealthProbe, KeychainHealthProbe>();
        s.AddSingleton<IOnnxHealthProbe>(_ => new OnnxHealthProbe(EmbeddingSettings.Default));

        // ViewModels
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<ThreeColumnLayoutViewModel>();
        s.AddSingleton<LeftNavViewModel>();
        s.AddSingleton<RightConversationViewModel>();
        s.AddSingleton<AppChromeViewModel>();
        s.AddSingleton<AppStatusBarViewModel>();
        s.AddTransient<WelcomeViewModel>();
        s.AddTransient<DashboardViewModel>();
        s.AddTransient<PlaceholderViewModel>();

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<DashboardViewModel,   DashboardView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);
        return reg;
    }
}
