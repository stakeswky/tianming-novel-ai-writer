using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Locator;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Locator;

public class ConsistencyIssueLocatorTests
{
    private sealed class StubVectorSearchService : IVectorSearchService
    {
        public List<VectorSearchResult> Results { get; set; } = [];

        public VectorSearchMode CurrentMode => VectorSearchMode.Keyword;

        public Task<List<VectorSearchResult>> SearchAsync(string query, int topK = 5)
        {
            return Task.FromResult(Results);
        }

        public Task<List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2)
        {
            return Task.FromResult(Results);
        }
    }

    [Fact]
    public async Task Locates_issue_using_entity_id_within_chapter_chunks()
    {
        var search = new StubVectorSearchService
        {
            Results =
            [
                new VectorSearchResult
                {
                    ChapterId = "ch-001",
                    Position = 2,
                    Content = "char-001 在这里跌回练气阶段。",
                    Score = 0.93
                }
            ]
        };
        var locator = new ConsistencyIssueLocator(search);
        var issue = new ConsistencyIssue { EntityId = "char-001", IssueType = "LevelRegression" };

        var located = await locator.LocateAsync(issue, "ch-001");

        Assert.Equal(2, located.ChunkPosition);
        Assert.InRange(located.VectorScore, 0.92d, 0.94d);
    }

    [Fact]
    public async Task Returns_issue_unchanged_when_no_matching_chunk_exists()
    {
        var search = new StubVectorSearchService
        {
            Results =
            [
                new VectorSearchResult
                {
                    ChapterId = "ch-001",
                    Position = 0,
                    Content = "无关内容",
                    Score = 0.51
                }
            ]
        };
        var locator = new ConsistencyIssueLocator(search);
        var issue = new ConsistencyIssue { EntityId = "char-999", IssueType = "LevelRegression" };

        var located = await locator.LocateAsync(issue, "ch-001");

        Assert.Equal(-1, located.ChunkPosition);
        Assert.Equal(0d, located.VectorScore);
    }
}
