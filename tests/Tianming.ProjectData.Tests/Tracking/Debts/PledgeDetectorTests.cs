using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class PledgeDetectorTests
{
    [Fact]
    public async Task Detects_overdue_pledge()
    {
        var detector = new PledgeDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Pledges = new PledgeGuide
            {
                Pledges = new()
                {
                    ["p1"] = new PledgeEntry
                    {
                        Name = "下周必去找他",
                        PromisedAtChapter = "vol1_ch3",
                        DeadlineChapter = "vol1_ch8",
                        IsFulfilled = false,
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch9", new ChapterChanges(), new FactSnapshot(), ctx);

        var debt = Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Pledge, debt.Category);
        Assert.Contains("下周必去找他", debt.Description);
    }

    [Fact]
    public async Task Skips_pledge_before_deadline()
    {
        var detector = new PledgeDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Pledges = new PledgeGuide
            {
                Pledges = new()
                {
                    ["p1"] = new PledgeEntry
                    {
                        DeadlineChapter = "vol1_ch8",
                        IsFulfilled = false,
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch5", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Empty(debts);
    }
}
