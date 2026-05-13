using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.Design;
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
        s.AddSingleton<ICurrentProjectService, CurrentProjectService>();

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

        // M4.1 设计模块：6 schema (singleton) + 6 adapter (transient) + 6 VM (transient)
        s.AddSingleton<WorldRulesSchema>();
        s.AddSingleton<CharacterRulesSchema>();
        s.AddSingleton<FactionRulesSchema>();
        s.AddSingleton<LocationRulesSchema>();
        s.AddSingleton<PlotRulesSchema>();
        s.AddSingleton<CreativeMaterialsSchema>();

        s.AddTransient(sp => new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(
            sp.GetRequiredService<WorldRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(
            sp.GetRequiredService<CharacterRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(
            sp.GetRequiredService<FactionRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(
            sp.GetRequiredService<LocationRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<PlotRulesCategory, PlotRulesData>(
            sp.GetRequiredService<PlotRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData>(
            sp.GetRequiredService<CreativeMaterialsSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        s.AddTransient<WorldRulesViewModel>();
        s.AddTransient<CharacterRulesViewModel>();
        s.AddTransient<FactionRulesViewModel>();
        s.AddTransient<LocationRulesViewModel>();
        s.AddTransient<PlotRulesViewModel>();
        s.AddTransient<CreativeMaterialsViewModel>();

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<DashboardViewModel,   DashboardView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);

        // M4.1：6 设计页（VM 不同，View 全部用 DesignModulePage）
        reg.Register<WorldRulesViewModel,        DesignModulePage>(PageKeys.DesignWorld);
        reg.Register<CharacterRulesViewModel,    DesignModulePage>(PageKeys.DesignCharacter);
        reg.Register<FactionRulesViewModel,      DesignModulePage>(PageKeys.DesignFaction);
        reg.Register<LocationRulesViewModel,     DesignModulePage>(PageKeys.DesignLocation);
        reg.Register<PlotRulesViewModel,         DesignModulePage>(PageKeys.DesignPlot);
        reg.Register<CreativeMaterialsViewModel, DesignModulePage>(PageKeys.DesignMaterials);
        return reg;
    }
}
