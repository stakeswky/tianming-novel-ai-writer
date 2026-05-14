using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Models.Tracking
{
    public class ConsistencyIssue
    {
        [JsonPropertyName("EntityId")] public string EntityId { get; set; } = string.Empty;
        [JsonPropertyName("IssueType")] public string IssueType { get; set; } = string.Empty;
        [JsonPropertyName("Expected")] public string Expected { get; set; } = string.Empty;
        [JsonPropertyName("Actual")] public string Actual { get; set; } = string.Empty;
        [JsonPropertyName("Layer")] public string Layer { get; set; } = string.Empty;
        [JsonPropertyName("ChunkPosition")] public int ChunkPosition { get; set; } = -1;
        [JsonPropertyName("VectorScore")] public double VectorScore { get; set; }

        public override string ToString()
        {
            return $"[{IssueType}] 实体: {EntityId}, 期望: {Expected}, 实际: {Actual}";
        }
    }
}
