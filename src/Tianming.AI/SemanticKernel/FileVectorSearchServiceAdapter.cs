using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking.Locator;
using AiVectorSearchMode = TM.Services.Framework.AI.SemanticKernel.VectorSearchMode;
using AiVectorSearchResult = TM.Services.Framework.AI.SemanticKernel.VectorSearchResult;
using LocatorVectorSearchMode = TM.Services.Modules.ProjectData.Tracking.Locator.VectorSearchMode;
using LocatorVectorSearchResult = TM.Services.Modules.ProjectData.Tracking.Locator.VectorSearchResult;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class FileVectorSearchServiceAdapter : IVectorSearchService
{
    private readonly FileVectorSearchService _inner;

    public FileVectorSearchServiceAdapter(FileVectorSearchService inner)
    {
        _inner = inner;
    }

    public LocatorVectorSearchMode CurrentMode => _inner.CurrentMode switch
    {
        AiVectorSearchMode.None => LocatorVectorSearchMode.None,
        AiVectorSearchMode.Keyword => LocatorVectorSearchMode.Keyword,
        AiVectorSearchMode.LocalEmbedding => LocatorVectorSearchMode.LocalEmbedding,
        AiVectorSearchMode.Hybrid => LocatorVectorSearchMode.Hybrid,
        _ => LocatorVectorSearchMode.None
    };

    public async Task<List<LocatorVectorSearchResult>> SearchAsync(string query, int topK = 5)
    {
        var results = await _inner.SearchAsync(query, topK).ConfigureAwait(false);
        return results.Select(Map).ToList();
    }

    public async Task<List<LocatorVectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2)
    {
        var results = await _inner.SearchByChapterAsync(chapterId, topK).ConfigureAwait(false);
        return results.Select(Map).ToList();
    }

    private static LocatorVectorSearchResult Map(AiVectorSearchResult result)
    {
        return new LocatorVectorSearchResult
        {
            ChapterId = result.ChapterId,
            Position = result.Position,
            Content = result.Content,
            Score = result.Score
        };
    }
}
