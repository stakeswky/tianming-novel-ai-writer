using System.Linq;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class TrackingDebtRegistryTests
{
    [Fact]
    public async Task DetectAll_runs_all_detectors_and_aggregates()
    {
        var registry = new TrackingDebtRegistry(new ITrackingDebtDetector[]
        {
            new EntityDriftDetector(),
            new OmissionDetector(),
            new DeadlineDetector(),
            new PledgeDetector(),
            new SecretRevealDetector(),
        });

        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry { Name = "x", IsResolved = false, IsOverdue = true },
                },
            },
        };

        var debts = await registry.DetectAllAsync("vol1_ch10", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Contains(debts, debt => debt.Category == TrackingDebtCategory.Deadline);
    }

    [Fact]
    public void Registry_exposes_five_detector_categories()
    {
        var registry = new TrackingDebtRegistry(new ITrackingDebtDetector[]
        {
            new EntityDriftDetector(),
            new OmissionDetector(),
            new DeadlineDetector(),
            new PledgeDetector(),
            new SecretRevealDetector(),
        });

        var categories = registry.SupportedCategories.OrderBy(category => category).ToArray();

        Assert.Equal(5, categories.Length);
    }
}
