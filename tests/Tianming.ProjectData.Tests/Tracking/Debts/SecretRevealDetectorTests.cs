using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class SecretRevealDetectorTests
{
    [Fact]
    public async Task Detects_unexpected_reveal()
    {
        var detector = new SecretRevealDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Secrets = new SecretGuide
            {
                Secrets = new()
                {
                    ["s1"] = new SecretEntry
                    {
                        Name = "主角真实身份",
                        IsRevealed = true,
                        ExpectedRevealChapter = "vol5_ch50",
                        ActualRevealChapter = "vol1_ch5",
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol1_ch5", new ChapterChanges(), new FactSnapshot(), ctx);

        var debt = Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.SecretReveal, debt.Category);
        Assert.Contains("主角真实身份", debt.Description);
    }

    [Fact]
    public async Task Skips_secret_revealed_at_expected_chapter()
    {
        var detector = new SecretRevealDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Secrets = new SecretGuide
            {
                Secrets = new()
                {
                    ["s1"] = new SecretEntry
                    {
                        Name = "x",
                        IsRevealed = true,
                        ExpectedRevealChapter = "vol5_ch50",
                        ActualRevealChapter = "vol5_ch50",
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("vol5_ch50", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Empty(debts);
    }
}
