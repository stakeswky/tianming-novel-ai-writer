using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class DeadlineDetectorTests
{
    [Fact]
    public async Task Detects_overdue_foreshadowing()
    {
        var detector = new DeadlineDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsSetup = true,
                        IsResolved = false,
                        IsOverdue = true,
                        ExpectedPayoffChapter = "vol1_ch10",
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch11", new ChapterChanges(), new FactSnapshot(), ctx);

        var debt = Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Deadline, debt.Category);
        Assert.Equal("fs-1", debt.EntityId);
        Assert.Contains("金鳞匕首", debt.Description);
    }

    [Fact]
    public async Task Skips_resolved_foreshadowing()
    {
        var detector = new DeadlineDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "x",
                        IsResolved = true,
                        IsOverdue = false,
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch11", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Empty(debts);
    }
}
