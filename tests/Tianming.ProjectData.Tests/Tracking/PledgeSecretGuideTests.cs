using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Guides;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class PledgeSecretGuideTests
{
    [Fact]
    public void PledgeGuide_round_trip()
    {
        var guide = new PledgeGuide
        {
            SourceBookId = "book-1",
        };
        guide.Pledges["p1"] = new PledgeEntry
        {
            Name = "下周必去看他",
            PromisedByCharacterId = "char-001",
            PromisedAtChapter = "vol1_ch3",
            DeadlineChapter = "vol1_ch8",
            IsFulfilled = false,
        };
        var json = JsonSerializer.Serialize(guide);
        var back = JsonSerializer.Deserialize<PledgeGuide>(json);

        Assert.NotNull(back);
        Assert.Single(back!.Pledges);
        Assert.False(back.Pledges["p1"].IsFulfilled);
    }

    [Fact]
    public void SecretGuide_round_trip()
    {
        var guide = new SecretGuide { SourceBookId = "book-1" };
        guide.Secrets["s1"] = new SecretEntry
        {
            Name = "主角真实身份",
            IsRevealed = false,
            ExpectedRevealChapter = "vol5_ch50",
            ActualRevealChapter = string.Empty,
            HoldersCharacterIds = new() { "char-002", "char-003" },
        };
        var json = JsonSerializer.Serialize(guide);
        var back = JsonSerializer.Deserialize<SecretGuide>(json);

        Assert.NotNull(back);
        Assert.Single(back!.Secrets);
        Assert.False(back.Secrets["s1"].IsRevealed);
    }
}
