using System;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public enum GenerationStep
{
    PrepareStart,
    PrepareDone,
    GateDone,
    ContentSaved,
    TrackingDone,
    Done,
}

public sealed class GenerationJournalEntry
{
    [JsonPropertyName("ChapterId")]
    public string ChapterId { get; set; } = string.Empty;

    [JsonPropertyName("Step")]
    public GenerationStep Step { get; set; }

    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("PayloadJson")]
    public string PayloadJson { get; set; } = string.Empty;
}
