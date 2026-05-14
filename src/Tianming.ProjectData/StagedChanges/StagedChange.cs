using System;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.StagedChanges;

public enum StagedChangeType
{
    WorkspaceEdit,
    DataEdit,
    ContentEdit,
}

public sealed class StagedChange
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ChangeType")]
    public StagedChangeType ChangeType { get; set; }

    [JsonPropertyName("TargetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("OldContentSnippet")]
    public string OldContentSnippet { get; set; } = string.Empty;

    [JsonPropertyName("NewContentSnippet")]
    public string NewContentSnippet { get; set; } = string.Empty;

    [JsonPropertyName("PayloadJson")]
    public string PayloadJson { get; set; } = string.Empty;

    [JsonPropertyName("Reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
