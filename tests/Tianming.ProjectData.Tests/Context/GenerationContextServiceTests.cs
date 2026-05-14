using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class GenerationContextServiceTests
{
    [Fact]
    public async Task Build_returns_context_with_chapter_id_set()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new GenerationContextService(
            root,
            (chapterId, ct) => Task.FromResult(new FactSnapshot()),
            ct => Task.FromResult(new DesignElementNames()),
            (chapterId, ct) => Task.FromResult("Previous chapters summary text"));

        var context = await svc.BuildAsync("ch-005");

        Assert.Equal("ch-005", context.ChapterId);
        Assert.NotNull(context.FactSnapshot);
        Assert.NotNull(context.DesignElements);
        Assert.Equal("Previous chapters summary text", context.PreviousChaptersSummary);
    }

    [Fact]
    public async Task Build_passes_chapter_id_to_fact_and_summary_providers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string? factChapterId = null;
        string? summaryChapterId = null;

        var svc = new GenerationContextService(
            root,
            (chapterId, ct) =>
            {
                factChapterId = chapterId;
                return Task.FromResult(new FactSnapshot());
            },
            ct => Task.FromResult(new DesignElementNames()),
            (chapterId, ct) =>
            {
                summaryChapterId = chapterId;
                return Task.FromResult(string.Empty);
            });

        await svc.BuildAsync("ch-009");

        Assert.Equal("ch-009", factChapterId);
        Assert.Equal("ch-009", summaryChapterId);
    }
}
