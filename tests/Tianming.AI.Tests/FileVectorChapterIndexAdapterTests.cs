using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.AI.Tests;

public class FileVectorChapterIndexAdapterTests
{
    [Fact]
    public async Task IndexChapterAsync_invalidates_stale_vector_cache_after_rewrite()
    {
        using var workspace = new TempDirectory();
        var chapterPath = Path.Combine(workspace.Path, "vol1_ch1.md");
        await File.WriteAllTextAsync(chapterPath, "# 第1章\n\n旧线索在寒潭。");
        var search = new FileVectorSearchService(workspace.Path);
        IChapterDerivedIndex adapter = new FileVectorChapterIndexAdapter(search);

        Assert.Single(await search.SearchAsync("旧线索", topK: 1));

        await File.WriteAllTextAsync(chapterPath, "# 第1章\n\n新线索在山门。");
        await adapter.IndexChapterAsync(
            "vol1_ch1",
            chapterPath,
            "# 第1章\n\n新线索在山门。",
            new ChapterChanges());

        Assert.Empty(await search.SearchAsync("旧线索", topK: 1));
        var results = await search.SearchAsync("新线索", topK: 1);
        Assert.Single(results);
        Assert.Equal("vol1_ch1", results[0].ChapterId);
    }

    [Fact]
    public async Task RemoveChapterAsync_drops_cached_chunks_after_file_delete()
    {
        using var workspace = new TempDirectory();
        var chapterPath = Path.Combine(workspace.Path, "vol1_ch2.md");
        await File.WriteAllTextAsync(chapterPath, "# 第2章\n\n清理线索留在密室。");
        var search = new FileVectorSearchService(workspace.Path);
        IChapterDerivedIndex adapter = new FileVectorChapterIndexAdapter(search);

        Assert.Single(await search.SearchByChapterAsync("vol1_ch2", topK: 1));

        File.Delete(chapterPath);
        await adapter.RemoveChapterAsync("vol1_ch2");

        Assert.Empty(await search.SearchByChapterAsync("vol1_ch2", topK: 1));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-vector-adapter-{Guid.NewGuid():N}");

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
