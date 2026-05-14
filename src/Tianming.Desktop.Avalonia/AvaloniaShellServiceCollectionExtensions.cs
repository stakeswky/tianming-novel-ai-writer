using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Common.Models;
using TM.Framework.Appearance;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Services;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using TM.Services.Framework.AI.Monitoring;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.BookPipeline;
using TM.Services.Modules.ProjectData.BookPipeline.Steps;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Generation.Wal;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Backup;
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
using TM.Services.Modules.ProjectData.StagedChanges;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Packaging;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using TM.Services.Modules.ProjectData.Tracking.Locator;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using Tianming.Desktop.Avalonia.ViewModels.Book;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Tianming.Desktop.Avalonia.ViewModels.Packaging;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.AI;
using Tianming.Desktop.Avalonia.Views.Book;
using Tianming.Desktop.Avalonia.Views.Design;
using Tianming.Desktop.Avalonia.Views.Editor;
using Tianming.Desktop.Avalonia.Views.Generate;
using Tianming.Desktop.Avalonia.Views.Packaging;
using Tianming.Desktop.Avalonia.Views.Shell;

namespace Tianming.Desktop.Avalonia;

public static class AvaloniaShellServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions StagedDataJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

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
        s.AddSingleton<IDispatcherScheduler, AvaloniaDispatcherScheduler>();
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

        // M6.5 ContextService 拆分
        s.AddSingleton<LedgerRuleSetProvider>();
        s.AddSingleton(sp =>
            new ProjectContextDataBuilder(
                sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
                sp.GetRequiredService<ModuleDataAdapter<ChapterCategory, ChapterData>>(),
                sp.GetRequiredService<ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>>(),
                sp.GetRequiredService<ModuleDataAdapter<FactionRulesCategory, FactionRulesData>>(),
                sp.GetRequiredService<ModuleDataAdapter<LocationRulesCategory, LocationRulesData>>(),
                sp.GetRequiredService<ModuleDataAdapter<PlotRulesCategory, PlotRulesData>>(),
                sp.GetRequiredService<ModuleDataAdapter<WorldRulesCategory, WorldRulesData>>()));
        s.AddSingleton<IDesignContextService>(sp =>
            new DesignContextService(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IGenerationContextService>(sp =>
            new GenerationContextService(
                sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
                sp.GetRequiredService<ProjectContextDataBuilder>()));
        s.AddSingleton<IValidationContextService>(sp =>
            new ValidationContextService(
                sp.GetRequiredService<ProjectContextDataBuilder>(),
                sp.GetRequiredService<LedgerRuleSetProvider>()));
        s.AddSingleton<IPackagingContextService>(sp =>
            new PackagingContextService(
                sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
                sp.GetRequiredService<IDesignContextService>()));
        s.AddSingleton<IPreflightChecker>(sp =>
            new DefaultPreflightChecker(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IBookExporter, ZipBookExporter>();
        s.AddSingleton<IProjectBackupService>(sp =>
            new FileProjectBackupService(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        // M6.2 Humanize + CHANGES Canonicalize
        s.AddSingleton<FileHumanizeRulesStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            return new FileHumanizeRulesStore(Path.Combine(paths.AppSupportDirectory, "Humanize"));
        });
        s.AddSingleton<IHumanizeRule>(sp =>
        {
            var cfg = sp.GetRequiredService<FileHumanizeRulesStore>().Load();
            return new PhraseReplaceRule(cfg.PhraseReplacements);
        });
        s.AddSingleton<IHumanizeRule, PunctuationRule>();
        s.AddSingleton<IHumanizeRule>(sp =>
        {
            var cfg = sp.GetRequiredService<FileHumanizeRulesStore>().Load();
            return new SentenceLengthRule(cfg.SentenceLongThreshold);
        });
        s.AddSingleton<HumanizePipeline>(sp =>
            new HumanizePipeline(sp.GetServices<IHumanizeRule>()));

        // M6.3 WAL + 生成恢复
        s.AddTransient<IGenerationJournal>(sp =>
            new FileGenerationJournal(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton(sp =>
            new GenerationRecoveryService(
                () => new FileGenerationJournal(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot),
                async (_, _, _) =>
                {
                    // M6.3 only discovers pending WAL entries at startup. Automatic
                    // generation resume is intentionally left for a later lane.
                    await Task.CompletedTask.ConfigureAwait(false);
                }));

        // M6.1 Tracking 债务检测
        s.AddSingleton<ITrackingDebtDetector, EntityDriftDetector>();
        s.AddSingleton<ITrackingDebtDetector, OmissionDetector>();
        s.AddSingleton<ITrackingDebtDetector, DeadlineDetector>();
        s.AddSingleton<ITrackingDebtDetector, PledgeDetector>();
        s.AddSingleton<ITrackingDebtDetector, SecretRevealDetector>();
        s.AddSingleton(sp => new TrackingDebtRegistry(sp.GetServices<ITrackingDebtDetector>()));

        // M6.4 校验分层 + 向量定位
        s.AddSingleton(sp => new FileVectorSearchService(
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
            sp.GetRequiredService<ITextEmbedder>()));
        s.AddSingleton<FileVectorSearchServiceAdapter>();
        s.AddSingleton<IVectorSearchService>(sp => sp.GetRequiredService<FileVectorSearchServiceAdapter>());
        s.AddSingleton<IConsistencyLayer, StructuralLayer>();
        s.AddSingleton<IConsistencyLayer, EntityLayer>();
        s.AddSingleton<IConsistencyLayer, ForeshadowLayer>();
        s.AddSingleton<IConsistencyLayer, TimelineLayer>();
        s.AddSingleton<IConsistencyLayer, RelationshipLayer>();
        s.AddSingleton(sp => new LayeredConsistencyChecker(sp.GetServices<IConsistencyLayer>()));
        s.AddSingleton(sp => new ConsistencyIssueLocator(sp.GetRequiredService<IVectorSearchService>()));

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
        s.AddSingleton<IBookGenerationJournal>(sp =>
            new FileBookGenerationJournal(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IBookPipelineStep, DesignStep>();
        s.AddSingleton<IBookPipelineStep, OutlineStep>();
        s.AddSingleton<IBookPipelineStep, VolumeStep>();
        s.AddSingleton<IBookPipelineStep, ChapterPlanningStep>();
        s.AddSingleton<IBookPipelineStep, BlueprintStep>();
        s.AddSingleton<IBookPipelineStep, GenerateStep>();
        s.AddSingleton<IBookPipelineStep, HumanizeStep>();
        s.AddSingleton<IBookPipelineStep, GateStep>();
        s.AddSingleton<IBookPipelineStep, SaveStep>();
        s.AddSingleton<IBookPipelineStep, IndexStep>();
        s.AddSingleton(sp =>
            new BookGenerationOrchestrator(
                sp.GetServices<IBookPipelineStep>(),
                sp.GetRequiredService<IBookGenerationJournal>()));
        s.AddTransient<BookPipelineViewModel>();

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
        s.AddSingleton<IAIModelRouter>(sp => new DefaultAIModelRouter(
            () => sp.GetRequiredService<FileAIConfigurationStore>().GetAllConfigurations()));
        s.AddSingleton<RoutedChatClient>(sp => new RoutedChatClient(
            sp.GetRequiredService<OpenAICompatibleChatClient>(),
            sp.GetRequiredService<IAIModelRouter>(),
            sp.GetRequiredService<FileAIConfigurationStore>()));
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
        s.AddTransient<PackagingViewModel>();

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
        s.AddSingleton<IStagedChangeStore>(sp =>
            new FileStagedChangeStore(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new LookupDataTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new ReadChapterTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new SearchReferencesTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddSingleton<IConversationTool>(sp =>
            new ContentEditTool(sp.GetRequiredService<IStagedChangeStore>()));
        s.AddSingleton<IConversationTool>(sp =>
            new DataEditTool(sp.GetRequiredService<IStagedChangeStore>()));
        s.AddSingleton<IConversationTool>(sp =>
            new WorkspaceEditTool(sp.GetRequiredService<IStagedChangeStore>()));
        s.AddSingleton<IStagedChangeApprover>(sp => new StagedChangeApprover(
            sp.GetRequiredService<IStagedChangeStore>(),
            content: async (chapterId, newContent, ct) =>
            {
                var projectRoot = sp.GetRequiredService<ICurrentProjectService>().ProjectRoot;
                var store = new ChapterContentStore(Path.Combine(projectRoot, "Generated", "chapters"));
                ct.ThrowIfCancellationRequested();
                await store.SaveChapterAsync(chapterId, newContent).ConfigureAwait(false);
            },
            data: (category, dataId, dataJson, ct) => ApplyDataChangeAsync(sp, category, dataId, dataJson, ct),
            workspace: async (relativePath, newContent, ct) =>
            {
                var projectRoot = sp.GetRequiredService<ICurrentProjectService>().ProjectRoot;
                await WriteWorkspaceFileAsync(projectRoot, relativePath, newContent, ct).ConfigureAwait(false);
            }));
        s.AddSingleton<ConversationOrchestrator>(sp =>
            new ConversationOrchestrator(
                sp.GetRequiredService<OpenAICompatibleChatClient>(),
                sp.GetRequiredService<TagBasedThinkingStrategy>(),
                sp.GetRequiredService<IFileSessionStore>(),
                sp.GetServices<IConversationTool>(),
                sp.GetRequiredService<AskModeMapper>(),
                sp.GetRequiredService<PlanModeMapper>(),
                sp.GetRequiredService<AgentModeMapper>(),
                sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
                sp.GetRequiredService<IAIModelRouter>(),
                sp.GetRequiredService<FileAIConfigurationStore>()));
        s.AddSingleton<IConversationOrchestrator>(sp => sp.GetRequiredService<ConversationOrchestrator>());
        s.AddSingleton<IReferenceSuggestionSource, ReferenceSuggestionSource>();

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
        reg.Register<BookPipelineViewModel,     BookPipelinePage>(PageKeys.BookPipeline);
        reg.Register<ModelManagementViewModel,  ModelManagementPage>(PageKeys.AIModels);
        reg.Register<ApiKeysViewModel,          ApiKeysPage>(PageKeys.AIKeys);
        reg.Register<PromptManagementViewModel, PromptManagementPage>(PageKeys.AIPrompts);
        reg.Register<UsageStatisticsViewModel,  UsageStatisticsPage>(PageKeys.AIUsage);
        reg.Register<PackagingViewModel,        PackagingPage>(PageKeys.Packaging);
        return reg;
    }

    private static Task ApplyDataChangeAsync(
        IServiceProvider sp,
        string category,
        string dataId,
        string dataJson,
        CancellationToken ct)
        => category switch
        {
            "Characters" or "characters" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>>(),
                dataId,
                dataJson,
                ct),
            "WorldRules" or "worldrules" or "world" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<WorldRulesCategory, WorldRulesData>>(),
                dataId,
                dataJson,
                ct),
            "Factions" or "factions" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<FactionRulesCategory, FactionRulesData>>(),
                dataId,
                dataJson,
                ct),
            "Locations" or "locations" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<LocationRulesCategory, LocationRulesData>>(),
                dataId,
                dataJson,
                ct),
            "Plot" or "plot" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<PlotRulesCategory, PlotRulesData>>(),
                dataId,
                dataJson,
                ct),
            "CreativeMaterials" or "creativematerials" or "materials" => ApplyTypedDataChangeAsync(
                sp.GetRequiredService<ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData>>(),
                dataId,
                dataJson,
                ct),
            _ => throw new InvalidOperationException($"Unsupported staged data category '{category}'."),
        };

    private static async Task ApplyTypedDataChangeAsync<TCategory, TData>(
        ModuleDataAdapter<TCategory, TData> adapter,
        string dataId,
        string dataJson,
        CancellationToken ct)
        where TCategory : class, ICategory
        where TData : class, IDataItem
    {
        ct.ThrowIfCancellationRequested();
        await adapter.LoadAsync().ConfigureAwait(false);

        var item = JsonSerializer.Deserialize<TData>(dataJson, StagedDataJsonOptions)
            ?? throw new InvalidOperationException("Staged data payload could not be deserialized.");

        var existing = adapter.GetData().FirstOrDefault(entry => string.Equals(entry.Id, dataId, StringComparison.Ordinal));
        item.Id = string.IsNullOrWhiteSpace(item.Id) ? dataId : item.Id;

        if (string.IsNullOrWhiteSpace(item.Category))
        {
            item.Category = existing?.Category
                ?? adapter.GetCategories().FirstOrDefault()?.Name
                ?? throw new InvalidOperationException("No category available for staged data change.");
        }

        if (string.IsNullOrWhiteSpace(item.CategoryId))
        {
            item.CategoryId = existing?.CategoryId ?? string.Empty;
        }

        await adapter.UpdateAsync(item).ConfigureAwait(false);
    }

    private static async Task WriteWorkspaceFileAsync(
        string projectRoot,
        string relativePath,
        string newContent,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Workspace staged change requires a relativePath.");
        }

        var rootFullPath = Path.GetFullPath(projectRoot);
        var targetPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        if (!targetPath.StartsWith(rootFullPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Workspace staged change path escapes the project root.");
        }

        var parent = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, newContent, ct).ConfigureAwait(false);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}
