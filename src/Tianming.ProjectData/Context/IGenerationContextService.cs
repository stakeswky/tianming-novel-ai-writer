using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Context;

public interface IGenerationContextService
{
    Task<GenerationContext> BuildAsync(string chapterId, CancellationToken ct = default);
}

public sealed class GenerationContext
{
    public string ChapterId { get; set; } = string.Empty;

    public FactSnapshot FactSnapshot { get; set; } = new();

    public DesignElementNames DesignElements { get; set; } = new();

    public string PreviousChaptersSummary { get; set; } = string.Empty;
}
