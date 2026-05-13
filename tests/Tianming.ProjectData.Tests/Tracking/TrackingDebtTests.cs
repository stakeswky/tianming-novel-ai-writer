using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class TrackingDebtTests
{
    [Fact]
    public void TrackingDebt_serializes_round_trip()
    {
        var debt = new TrackingDebt
        {
            Id = "debt-001",
            Category = TrackingDebtCategory.EntityDrift,
            ChapterId = "vol1_ch5",
            EntityId = "char-shen-yan",
            Description = "沈砚 HairColor 由黑变棕",
            Severity = TrackingDebtSeverity.High,
            DetectedAtChapter = "vol1_ch5",
            EvidenceJson = "{\"old\":\"黑\",\"new\":\"棕\"}",
            ResolvedAtChapter = null,
        };

        var json = JsonSerializer.Serialize(debt);
        var back = JsonSerializer.Deserialize<TrackingDebt>(json);

        Assert.NotNull(back);
        Assert.Equal("debt-001", back!.Id);
        Assert.Equal(TrackingDebtCategory.EntityDrift, back.Category);
        Assert.Equal("沈砚 HairColor 由黑变棕", back.Description);
        Assert.Null(back.ResolvedAtChapter);
    }

    [Fact]
    public void All_five_categories_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.EntityDrift));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Omission));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Deadline));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Pledge));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.SecretReveal));
    }
}
