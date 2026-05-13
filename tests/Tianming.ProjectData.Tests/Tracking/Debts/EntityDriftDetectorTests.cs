using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class EntityDriftDetectorTests
{
    [Fact]
    public async Task Detects_hair_color_drift_for_character()
    {
        var detector = new EntityDriftDetector();
        var prev = new FactSnapshot();
        prev.CharacterDescriptions["char-shen"] = new CharacterCoreDescription
        {
            Id = "char-shen",
            Name = "沈砚",
            HairColor = "黑",
            EyeColor = "黑",
            Appearance = "高瘦",
        };
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "char-shen",
                    FieldChanges = new() { ["HairColor"] = "棕" },
                },
            ],
        };

        var debts = await detector.DetectAsync("vol1_ch5", changes, prev, new TrackingDebtDetectionContext());

        var debt = Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.EntityDrift, debt.Category);
        Assert.Equal("char-shen", debt.EntityId);
        Assert.Contains("HairColor", debt.Description);
        Assert.Contains("\"old\":\"黑\"", debt.EvidenceJson);
        Assert.Contains("\"new\":\"棕\"", debt.EvidenceJson);
    }

    [Fact]
    public async Task No_debt_when_field_unchanged()
    {
        var detector = new EntityDriftDetector();
        var prev = new FactSnapshot();
        prev.CharacterDescriptions["char-shen"] = new CharacterCoreDescription
        {
            Id = "char-shen",
            Name = "沈砚",
            HairColor = "黑",
        };
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "char-shen",
                    FieldChanges = new() { ["Mood"] = "怒" },
                },
            ],
        };

        var debts = await detector.DetectAsync("vol1_ch5", changes, prev, new TrackingDebtDetectionContext());

        Assert.Empty(debts);
    }
}
