using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class OmissionDetectorTests
{
    [Fact]
    public async Task Detects_missing_expected_setup_for_foreshadow()
    {
        var detector = new OmissionDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsSetup = false,
                        ExpectedSetupChapter = "vol1_ch5",
                        ActualSetupChapter = string.Empty,
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch5", new ChapterChanges(), new FactSnapshot(), ctx);

        var debt = Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Omission, debt.Category);
    }

    [Fact]
    public async Task Skips_when_expected_setup_is_for_another_chapter()
    {
        var detector = new OmissionDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsSetup = false,
                        ExpectedSetupChapter = "vol1_ch6",
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch5", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Empty(debts);
    }
}
