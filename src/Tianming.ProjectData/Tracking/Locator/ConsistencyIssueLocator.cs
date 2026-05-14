using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Locator;

public sealed class ConsistencyIssueLocator
{
    private readonly IVectorSearchService _search;

    public ConsistencyIssueLocator(IVectorSearchService search)
    {
        _search = search;
    }

    public async Task<ConsistencyIssue> LocateAsync(
        ConsistencyIssue issue,
        string chapterId,
        CancellationToken ct = default)
    {
        if (issue == null)
            throw new ArgumentNullException(nameof(issue));

        if (string.IsNullOrWhiteSpace(issue.EntityId) || string.IsNullOrWhiteSpace(chapterId))
            return issue;

        var results = await _search.SearchByChapterAsync(chapterId, topK: 8).ConfigureAwait(false);
        var bestMatch = results
            .Where(result =>
                string.Equals(result.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase)
                && result.Content.Contains(issue.EntityId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Position)
            .FirstOrDefault();

        if (bestMatch == null)
            return issue;

        issue.ChunkPosition = bestMatch.Position;
        issue.VectorScore = bestMatch.Score;
        return issue;
    }
}
