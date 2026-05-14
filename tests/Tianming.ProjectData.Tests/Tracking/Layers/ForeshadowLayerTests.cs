using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class ForeshadowLayerTests
{
    [Fact]
    public async Task Flags_payoff_before_setup()
    {
        var layer = new ForeshadowLayer();
        var changes = new ChapterChanges
        {
            ForeshadowingActions =
            [
                new ForeshadowingAction
                {
                    ForeshadowId = "f-001",
                    Action = "payoff"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            ForeshadowingStatus =
            [
                new ForeshadowingStatusSnapshot
                {
                    Id = "f-001",
                    IsSetup = false,
                    IsResolved = false
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Foreshadow", issue.Layer);
        Assert.Equal("PayoffBeforeSetup", issue.IssueType);
    }

    [Fact]
    public async Task Flags_setup_after_resolution()
    {
        var layer = new ForeshadowLayer();
        var changes = new ChapterChanges
        {
            ForeshadowingActions =
            [
                new ForeshadowingAction
                {
                    ForeshadowId = "f-002",
                    Action = "setup"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            ForeshadowingStatus =
            [
                new ForeshadowingStatusSnapshot
                {
                    Id = "f-002",
                    IsSetup = true,
                    IsResolved = true
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Foreshadow", issue.Layer);
        Assert.Equal("ForeshadowingRollback", issue.IssueType);
    }
}
