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
