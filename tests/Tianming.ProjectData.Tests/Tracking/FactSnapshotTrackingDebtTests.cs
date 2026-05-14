using System.Collections.Generic;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FactSnapshotTrackingDebtTests
{
    [Fact]
    public void FactSnapshot_has_TrackingDebts_default_empty()
    {
        var snap = new FactSnapshot();

        Assert.NotNull(snap.TrackingDebts);
        Assert.Empty(snap.TrackingDebts);
    }

    [Fact]
    public void FactSnapshot_serializes_TrackingDebts()
    {
        var snap = new FactSnapshot
        {
            TrackingDebts = new List<TrackingDebt>
            {
                new() { Id = "d1", Category = TrackingDebtCategory.Pledge, ChapterId = "vol1_ch3", Description = "未兑现承诺" },
            },
        };
        var json = JsonSerializer.Serialize(snap);
        var back = JsonSerializer.Deserialize<FactSnapshot>(json);

        Assert.NotNull(back);
        Assert.Single(back!.TrackingDebts);
        Assert.Equal(TrackingDebtCategory.Pledge, back.TrackingDebts[0].Category);
    }
}
