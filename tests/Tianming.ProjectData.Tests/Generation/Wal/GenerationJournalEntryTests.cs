using System;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class GenerationJournalEntryTests
{
    [Fact]
    public void Entry_round_trips()
    {
        var entry = new GenerationJournalEntry
        {
            ChapterId = "ch-005",
            Step = GenerationStep.GateDone,
            Timestamp = new DateTime(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc),
            PayloadJson = "{\"gateOk\":true}",
        };

        var json = JsonSerializer.Serialize(entry);
        var back = JsonSerializer.Deserialize<GenerationJournalEntry>(json);

        Assert.NotNull(back);
        Assert.Equal(GenerationStep.GateDone, back!.Step);
        Assert.Equal("ch-005", back.ChapterId);
    }

    [Fact]
    public void All_six_steps_defined()
    {
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.PrepareStart));
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.PrepareDone));
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.GateDone));
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.ContentSaved));
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.TrackingDone));
        Assert.True(Enum.IsDefined(typeof(GenerationStep), GenerationStep.Done));
    }
}
