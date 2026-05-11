using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileChapterKeywordIndexTests
{
    [Fact]
    public async Task IndexChapterAsync_extracts_keywords_and_searches_matching_chapters()
    {
        using var workspace = new TempDirectory();
        var index = new FileChapterKeywordIndex(workspace.Path);

        await index.IndexChapterAsync("vol1_ch1", new ChapterChanges
        {
            CharacterStateChanges = [new CharacterStateChange { CharacterId = "C7M3VT2K9P4NA" }],
            NewPlotPoints = [new PlotPointChange { Keywords = ["试炼", "山门"] }],
            ForeshadowingActions = [new ForeshadowingAction { ForeshadowId = "F7M3VT2K9P4NA" }],
            ItemTransfers = [new ItemTransferChange { ItemName = "玉佩" }]
        });
        await index.IndexChapterAsync("vol1_ch2", new ChapterChanges
        {
            NewPlotPoints = [new PlotPointChange { Keywords = ["山门"] }]
        });

        var directHits = await index.SearchAsync(["试炼"]);
        var rankedHits = await index.SearchAsync(["山门", "玉佩"]);
        var indexedIds = await index.GetIndexedChapterIdsAsync();

        Assert.Equal(["vol1_ch1"], directHits);
        Assert.Equal(["vol1_ch1", "vol1_ch2"], rankedHits);
        Assert.Equal(["vol1_ch1", "vol1_ch2"], indexedIds.Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task RemoveChapterAsync_removes_chapter_from_all_keyword_lists()
    {
        using var workspace = new TempDirectory();
        var index = new FileChapterKeywordIndex(workspace.Path);

        await index.IndexChapterAsync("vol1_ch1", new ChapterChanges
        {
            CharacterStateChanges = [new CharacterStateChange { CharacterId = "C7M3VT2K9P4NA" }],
            NewPlotPoints = [new PlotPointChange { Keywords = ["试炼"] }]
        });

        await index.RemoveChapterAsync("vol1_ch1");

        Assert.Empty(await index.SearchAsync(["C7M3VT2K9P4NA", "试炼"]));
        Assert.Empty(await index.GetIndexedChapterIdsAsync());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-keywords-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
