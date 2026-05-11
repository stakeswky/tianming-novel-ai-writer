using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class FileVectorSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_uses_keyword_fallback_and_ranks_matching_chunks()
    {
        using var workspace = new TempDirectory();
        WriteChapter(workspace.Path, "vol1_ch1", "林青在山门发现银钥匙。\n\n守门人提到旧盟约。");
        WriteChapter(workspace.Path, "vol1_ch2", "林青进入集市，寻找山门传闻。\n\n这里没有关键物品。");
        var service = new FileVectorSearchService(workspace.Path);

        await service.InitializeAsync();
        var results = await service.SearchAsync("山门 银钥匙", topK: 2);

        Assert.Equal(VectorSearchMode.Keyword, service.CurrentMode);
        Assert.Equal(["vol1_ch1", "vol1_ch2"], results.Select(result => result.ChapterId).ToArray());
        Assert.True(results[0].Score > results[1].Score);
        Assert.Contains("银钥匙", results[0].Content);
    }

    [Fact]
    public async Task SearchByChapterAsync_returns_ordered_chunks_for_specific_chapter()
    {
        using var workspace = new TempDirectory();
        var first = new string('甲', 520);
        var second = new string('乙', 120);
        WriteChapter(workspace.Path, "vol1_ch1", $"{first}\n\n{second}");
        var service = new FileVectorSearchService(workspace.Path);

        var results = await service.SearchByChapterAsync("vol1_ch1", topK: 2);

        Assert.Equal([0, 1], results.Select(result => result.Position).ToArray());
        Assert.All(results, result => Assert.Equal("vol1_ch1", result.ChapterId));
        Assert.All(results, result => Assert.Equal(1.0, result.Score));
    }

    [Fact]
    public async Task RemoveChapterAsync_invalidates_cached_chunks()
    {
        using var workspace = new TempDirectory();
        WriteChapter(workspace.Path, "vol1_ch1", "银钥匙藏在山门。");
        var service = new FileVectorSearchService(workspace.Path);
        Assert.NotEmpty(await service.SearchByChapterAsync("vol1_ch1"));

        File.Delete(System.IO.Path.Combine(workspace.Path, "vol1_ch1.md"));
        await service.RemoveChapterAsync("vol1_ch1");

        Assert.Empty(await service.SearchByChapterAsync("vol1_ch1"));
        Assert.Empty(await service.SearchAsync("银钥匙"));
    }

    [Fact]
    public async Task SearchAsync_returns_empty_when_chapter_directory_is_missing()
    {
        using var workspace = new TempDirectory();
        var missing = System.IO.Path.Combine(workspace.Path, "missing");
        var service = new FileVectorSearchService(missing);

        await service.InitializeAsync();

        Assert.Equal(VectorSearchMode.Keyword, service.CurrentMode);
        Assert.Empty(await service.SearchAsync("anything"));
    }

    [Fact]
    public async Task SearchAsync_uses_injected_embedder_when_available()
    {
        using var workspace = new TempDirectory();
        WriteChapter(workspace.Path, "vol1_ch1", "银钥匙唤醒山门阵法。");
        WriteChapter(workspace.Path, "vol1_ch2", "集市里只有普通传闻。");
        using var embedder = new HashingTextEmbedder();
        var service = new FileVectorSearchService(workspace.Path, embedder);

        await service.InitializeAsync();
        var results = await service.SearchAsync("银钥匙 阵法", topK: 2);

        Assert.Equal(VectorSearchMode.LocalEmbedding, service.CurrentMode);
        Assert.Equal("vol1_ch1", results[0].ChapterId);
        Assert.True(results[0].Score > 0);
    }

    [Fact]
    public void HashingTextEmbedder_returns_normalized_vectors_and_cosine_similarity()
    {
        using var embedder = new HashingTextEmbedder(dimension: 32);

        var first = embedder.Embed("银钥匙 山门");
        var second = embedder.Embed("银钥匙 山门");
        var unrelated = embedder.Embed("集市 酒楼");

        Assert.Equal(32, first.Length);
        Assert.Equal(1, Math.Round(Math.Sqrt(first.Sum(value => value * value)), 6));
        Assert.True(embedder.Similarity(first, second) > embedder.Similarity(first, unrelated));
        Assert.Equal(0, embedder.Similarity(first, Array.Empty<float>()));
    }

    private static void WriteChapter(string directory, string chapterId, string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Combine(directory, $"{chapterId}.md"), content);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-vector-{Guid.NewGuid():N}");

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
