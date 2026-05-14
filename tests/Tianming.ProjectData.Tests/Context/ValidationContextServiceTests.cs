using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class ValidationContextServiceTests
{
    [Fact]
    public async Task Build_returns_bundle_with_rule_and_snapshot()
    {
        var svc = new ValidationContextService(
            ct => Task.FromResult(new LedgerRuleSet()),
            (chapterId, ct) => Task.FromResult(new FactSnapshot()));

        var bundle = await svc.BuildAsync("ch-001");

        Assert.Equal("ch-001", bundle.ChapterId);
        Assert.NotNull(bundle.RuleSet);
        Assert.NotNull(bundle.FactSnapshot);
    }

    [Fact]
    public async Task Build_passes_chapter_id_to_snapshot_provider()
    {
        string? requestedChapterId = null;
        var svc = new ValidationContextService(
            ct => Task.FromResult(new LedgerRuleSet()),
            (chapterId, ct) =>
            {
                requestedChapterId = chapterId;
                return Task.FromResult(new FactSnapshot());
            });

        await svc.BuildAsync("ch-002");

        Assert.Equal("ch-002", requestedChapterId);
    }
}
