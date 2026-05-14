using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Context;

public sealed class GenerationContextService : IGenerationContextService
{
    public delegate Task<FactSnapshot> FactSnapshotProvider(string chapterId, CancellationToken ct);

    public delegate Task<DesignElementNames> DesignNamesProvider(CancellationToken ct);

    public delegate Task<string> PreviousChaptersSummaryProvider(string chapterId, CancellationToken ct);

    private readonly string _projectRoot;
    private readonly FactSnapshotProvider _factProvider;
    private readonly DesignNamesProvider _namesProvider;
    private readonly PreviousChaptersSummaryProvider _summaryProvider;
    private readonly ProjectContextDataBuilder? _builder;

    public GenerationContextService(
        string projectRoot,
        FactSnapshotProvider factProvider,
        DesignNamesProvider namesProvider,
        PreviousChaptersSummaryProvider summaryProvider)
    {
        _projectRoot = projectRoot;
        _factProvider = factProvider;
        _namesProvider = namesProvider;
        _summaryProvider = summaryProvider;
    }

    public GenerationContextService(
        string projectRoot,
        ProjectContextDataBuilder builder)
    {
        _projectRoot = projectRoot;
        _builder = builder;
        _factProvider = (chapterId, ct) => _builder.BuildFactSnapshotAsync(chapterId, ct);
        _namesProvider = ct => _builder.BuildDesignElementNamesAsync(ct);
        _summaryProvider = (chapterId, ct) => _builder.BuildPreviousChaptersSummaryAsync(chapterId, ct);
    }

    public GenerationContextService(
        string projectRoot,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning.ChapterCategory, TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning.ChapterData> chapterAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Characters.CharacterRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Characters.CharacterRulesData> characterAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Factions.FactionRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Factions.FactionRulesData> factionAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Location.LocationRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Location.LocationRulesData> locationAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Plot.PlotRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Plot.PlotRulesData> plotAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Worldview.WorldRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Worldview.WorldRulesData> worldRuleAdapter)
        : this(projectRoot, new ProjectContextDataBuilder(projectRoot, chapterAdapter, characterAdapter, factionAdapter, locationAdapter, plotAdapter, worldRuleAdapter))
    {
    }

    public async Task<GenerationContext> BuildAsync(string chapterId, CancellationToken ct = default)
    {
        var fact = await _factProvider(chapterId, ct).ConfigureAwait(false);
        var names = await _namesProvider(ct).ConfigureAwait(false);
        var summary = await _summaryProvider(chapterId, ct).ConfigureAwait(false);

        return new GenerationContext
        {
            ChapterId = chapterId,
            FactSnapshot = fact,
            DesignElements = names,
            PreviousChaptersSummary = summary,
        };
    }
}
