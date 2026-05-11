using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterContentStoreTests
{
    [Fact]
    public async Task SaveChapterAsync_writes_content_atomically_and_reads_it_back()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);

        var result = await store.SaveChapterAsync("vol1_ch2", "# 第2章 雪夜\n\n林衡入山。");
        var content = await store.GetChapterAsync("vol1_ch2");

        Assert.False(result.HadExistingFile);
        Assert.True(result.ContentChanged);
        Assert.Equal("# 第2章 雪夜\n\n林衡入山。", content);
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, ".staging", "vol1_ch2.md")));
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "vol1_ch2.md.bak")));
    }

    [Fact]
    public async Task SaveChapterAsync_reports_unchanged_rewrites()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);

        await store.SaveChapterAsync("vol1_ch2", "# 第2章 雪夜\n\n林衡入山。");
        var result = await store.SaveChapterAsync("vol1_ch2", "# 第2章 雪夜\n\n林衡入山。");

        Assert.True(result.HadExistingFile);
        Assert.False(result.ContentChanged);
    }

    [Fact]
    public async Task GetGeneratedChaptersAsync_returns_sorted_metadata()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol2_ch1", "# 第1章 第二卷\n\n青璃 arrives.");
        await store.SaveChapterAsync("vol1_ch10", "# 第10章 后山\n\n林衡入山。");
        await store.SaveChapterAsync("vol1_ch2", "# 第2章 山门\n\n林衡出发。");

        var chapters = await store.GetGeneratedChaptersAsync();

        Assert.Equal(["vol1_ch2", "vol1_ch10", "vol2_ch1"], chapters.Select(chapter => chapter.Id).ToArray());
        Assert.Equal("第2章 山门", chapters[0].Title);
        Assert.True(chapters[0].WordCount > 0);
    }

    [Fact]
    public async Task DeleteChapterAsync_removes_existing_file_only()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol1_ch2", "# 第2章 山门\n\n林衡出发。");

        Assert.True(await store.DeleteChapterAsync("vol1_ch2"));
        Assert.False(await store.DeleteChapterAsync("vol1_ch2"));
        Assert.False(store.ChapterExists("vol1_ch2"));
    }

    [Fact]
    public async Task VolumeExistsAsync_detects_generated_chapters_in_volume()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol2_ch1", "# 第1章 第二卷\n\n林衡出发。");

        Assert.True(await store.VolumeExistsAsync(2));
        Assert.False(await store.VolumeExistsAsync(1));
    }

    [Fact]
    public async Task GenerateNextChapterIdFromSourceAsync_returns_next_chapter_in_same_volume()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol1_ch2", "# 第2章 山门\n\n林衡出发。");

        var next = await store.GenerateNextChapterIdFromSourceAsync("vol1_ch2");

        Assert.Equal("vol1_ch3", next);
    }

    [Fact]
    public async Task GenerateNextChapterIdFromSourceAsync_rejects_missing_source_and_existing_target()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol1_ch2", "# 第2章 山门\n\n林衡出发。");
        await store.SaveChapterAsync("vol1_ch3", "# 第3章 已存在\n\n林衡入山。");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GenerateNextChapterIdFromSourceAsync("vol1_ch1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GenerateNextChapterIdFromSourceAsync("vol1_ch2"));
    }

    [Fact]
    public async Task GenerateNextChapterIdFromSourceAsync_can_cross_to_next_volume_range()
    {
        using var workspace = new TempDirectory();
        var store = new ChapterContentStore(workspace.Path);
        await store.SaveChapterAsync("vol1_ch10", "# 第10章 卷末\n\n林衡出发。");

        var next = await store.GenerateNextChapterIdFromSourceAsync(
            "vol1_ch10",
            [
                new ChapterVolumeRange { VolumeNumber = 1, EndChapter = 10 },
                new ChapterVolumeRange { VolumeNumber = 2, StartChapter = 3 }
            ]);

        Assert.Equal("vol2_ch3", next);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-store-{Guid.NewGuid():N}");

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
