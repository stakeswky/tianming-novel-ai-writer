using System.Collections.Generic;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Locator;

public interface IVectorSearchService
{
    VectorSearchMode CurrentMode { get; }

    Task<List<VectorSearchResult>> SearchAsync(string query, int topK = 5);

    Task<List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2);
}
