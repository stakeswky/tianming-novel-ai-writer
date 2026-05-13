using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Services;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Monitoring;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.AI;
using Tianming.Desktop.Avalonia.Views.Design;
using Tianming.Desktop.Avalonia.Views.Editor;
using Tianming.Desktop.Avalonia.Views.Generate;
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

        // M4.3 章节编辑器基础设施
        s.AddSingleton<ITimerScheduler, DispatcherTimerScheduler>();
        s.AddSingleton<IChapterDraftStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            return new FileChapterDraftStore(System.IO.Path.Combine(paths.AppSupportDirectory, "Drafts"));
        });
        s.AddSingleton<AutoSaveScheduler>(sp =>
            new AutoSaveScheduler(sp.GetRequiredService<ITimerScheduler>(), System.TimeSpan.FromSeconds(2)));

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

        // M4.2 生成规划：4 schema (singleton) + 4 adapter (transient) + 4 VM (transient) + ChapterPipelineVM (transient)
        s.AddSingleton<OutlineSchema>();
        s.AddSingleton<VolumeDesignSchema>();
        s.AddSingleton<ChapterPlanningSchema>();
        s.AddSingleton<BlueprintSchema>();

        s.AddTransient(sp => new ModuleDataAdapter<OutlineCategory, OutlineData>(
            sp.GetRequiredService<OutlineSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData>(
            sp.GetRequiredService<VolumeDesignSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<ChapterCategory, ChapterData>(
            sp.GetRequiredService<ChapterPlanningSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<BlueprintCategory, BlueprintData>(
            sp.GetRequiredService<BlueprintSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        // M4.4 章节生成状态追踪
        s.AddSingleton<ChapterGenerationStore>(sp =>
            new ChapterGenerationStore(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        s.AddTransient<OutlineViewModel>();
        s.AddTransient<VolumeDesignViewModel>();
        s.AddTransient<ChapterPlanningViewModel>(sp =>
            new ChapterPlanningViewModel(
                sp.GetRequiredService<ModuleDataAdapter<ChapterCategory, ChapterData>>(),
                sp.GetRequiredService<ChapterGenerationStore>()));
        s.AddTransient<BlueprintViewModel>();
        s.AddTransient<ChapterPipelineViewModel>(sp =>
            new ChapterPipelineViewModel(
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ChapterGenerationStore>(),
                sp.GetRequiredService<ModuleDataAdapter<ChapterCategory, ChapterData>>()));

        // M4.6 AI 管理：模型配置 / Keychain / 提示词 / 用量统计。
        s.AddSingleton<IApiKeySecretStore>(_ => new MacOSKeychainApiKeySecretStore(new ProcessSecurityCommandRunner()));
        s.AddSingleton<FileAIConfigurationStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            var root = Path.Combine(paths.AppSupportDirectory, "AI");
            return new FileAIConfigurationStore(
                Path.Combine(root, "Library"),
                Path.Combine(root, "Configurations"),
                sp.GetRequiredService<IApiKeySecretStore>());
        });
        s.AddSingleton<FilePromptTemplateStore>(sp =>
        {
            var root = Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "Prompts");
            return new FilePromptTemplateStore(
                Path.Combine(root, "categories.json"),
                Path.Combine(root, "Templates"),
                Path.Combine(root, "BuiltInTemplates"));
        });
        s.AddSingleton<FileUsageStatisticsService>(sp =>
        {
            var root = Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "Usage");
            return new FileUsageStatisticsService(Path.Combine(root, "api_statistics.json"));
        });
        s.AddTransient<ModelManagementViewModel>();
        s.AddTransient<ApiKeysViewModel>();
        s.AddTransient<PromptManagementViewModel>();
        s.AddTransient<UsageStatisticsViewModel>();

        // M4.5+ AI 对话面板：编排器、工具与会话持久化。
        s.AddSingleton<OpenAICompatibleChatClient>(sp =>
            new OpenAICompatibleChatClient(sp.GetRequiredService<System.Net.Http.HttpClient>()));
        s.AddSingleton<TagBasedThinkingStrategy>();
        s.AddSingleton<AskModeMapper>();
        s.AddSingleton<IPlanParser, PlanStepParser>();
        s.AddSingleton<PlanModeMapper>(sp =>
            new PlanModeMapper(sp.GetRequiredService<IPlanParser>()));
        s.AddSingleton<AgentModeMapper>();
        s.AddSingleton<IFileSessionStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            return new FileSessionStore(Path.Combine(paths.AppSupportDirectory, "Sessions"));
        });
        s.AddSingleton<IConversationTool>(sp =>
            new LookupDataTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new ReadChapterTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new SearchReferencesTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<ConversationOrchestrator>(sp =>
            new ConversationOrchestrator(
                sp.GetRequiredService<OpenAICompatibleChatClient>(),
                sp.GetRequiredService<TagBasedThinkingStrategy>(),
                sp.GetRequiredService<IFileSessionStore>(),
                sp.GetServices<IConversationTool>(),
                sp.GetRequiredService<AskModeMapper>(),
                sp.GetRequiredService<PlanModeMapper>(),
                sp.GetRequiredService<AgentModeMapper>(),
                sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        // M4.3 章节编辑器 VM
        s.AddTransient<EditorWorkspaceViewModel>(sp =>
            new EditorWorkspaceViewModel(
                projectId: "default",
                sp.GetRequiredService<IChapterDraftStore>(),
                sp.GetRequiredService<AutoSaveScheduler>()));

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<DashboardViewModel,   DashboardView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);

        // M4.3 章节编辑器
        reg.Register<EditorWorkspaceViewModel, EditorWorkspaceView>(PageKeys.Editor);

        // M4.1：6 设计页（VM 不同，View 全部用 DesignModulePage）
        reg.Register<WorldRulesViewModel,        DesignModulePage>(PageKeys.DesignWorld);
        reg.Register<CharacterRulesViewModel,    DesignModulePage>(PageKeys.DesignCharacter);
        reg.Register<FactionRulesViewModel,      DesignModulePage>(PageKeys.DesignFaction);
        reg.Register<LocationRulesViewModel,     DesignModulePage>(PageKeys.DesignLocation);
        reg.Register<PlotRulesViewModel,         DesignModulePage>(PageKeys.DesignPlot);
        reg.Register<CreativeMaterialsViewModel, DesignModulePage>(PageKeys.DesignMaterials);

        // M4.2：4 schema 页（VM 不同，View 全部复用 DesignModulePage）+ 1 ChapterPipelinePage（独立 view）
        reg.Register<OutlineViewModel,          DesignModulePage>(PageKeys.GenerateOutline);
        reg.Register<VolumeDesignViewModel,     DesignModulePage>(PageKeys.GenerateVolume);
        reg.Register<ChapterPlanningViewModel,  DesignModulePage>(PageKeys.GenerateChapter);
        reg.Register<BlueprintViewModel,        DesignModulePage>(PageKeys.GenerateBlueprint);
        reg.Register<ChapterPipelineViewModel,  ChapterPipelinePage>(PageKeys.GeneratePipeline);
        reg.Register<ModelManagementViewModel,  ModelManagementPage>(PageKeys.AIModels);
        reg.Register<ApiKeysViewModel,          ApiKeysPage>(PageKeys.AIKeys);
        reg.Register<PromptManagementViewModel, PromptManagementPage>(PageKeys.AIPrompts);
        reg.Register<UsageStatisticsViewModel,  UsageStatisticsPage>(PageKeys.AIUsage);
        return reg;
    }
}
