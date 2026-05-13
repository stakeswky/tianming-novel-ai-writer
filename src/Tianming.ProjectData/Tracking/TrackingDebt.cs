using System;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Models.Tracking
{
    public enum TrackingDebtCategory
    {
        EntityDrift,
        Omission,
        Deadline,
        Pledge,
        SecretReveal,
    }

    public enum TrackingDebtSeverity
    {
        Low,
        Medium,
        High,
    }

    public sealed class TrackingDebt
    {
        [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("Category")] public TrackingDebtCategory Category { get; set; }
        [JsonPropertyName("ChapterId")] public string ChapterId { get; set; } = string.Empty;
        [JsonPropertyName("EntityId")] public string EntityId { get; set; } = string.Empty;
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("Severity")] public TrackingDebtSeverity Severity { get; set; } = TrackingDebtSeverity.Medium;
        [JsonPropertyName("DetectedAtChapter")] public string DetectedAtChapter { get; set; } = string.Empty;
        [JsonPropertyName("EvidenceJson")] public string EvidenceJson { get; set; } = string.Empty;
        [JsonPropertyName("ResolvedAtChapter")] public string? ResolvedAtChapter { get; set; }
        [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
