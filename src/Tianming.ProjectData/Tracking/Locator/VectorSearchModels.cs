namespace TM.Services.Modules.ProjectData.Tracking.Locator;

public sealed class VectorSearchResult
{
    public string ChapterId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
}

public enum VectorSearchMode
{
    None,
    Keyword,
    LocalEmbedding,
    Hybrid
}
