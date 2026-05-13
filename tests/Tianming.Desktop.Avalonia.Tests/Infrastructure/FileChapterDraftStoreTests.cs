using System;
using System.IO;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class FileChapterDraftStoreTests
{
    [Fact]
    public async Task SaveDraftAsync_writes_file_under_project()
    {
        var (store, root) = NewStore();
        await store.SaveDraftAsync("proj-a", "chap-1", "# hello\n世界");

        var file = Path.Combine(root, "proj-a", "chap-1.md");
        Assert.True(File.Exists(file));
        Assert.Equal("# hello\n世界", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task LoadDraftAsync_returns_null_when_missing()
    {
        var (store, _) = NewStore();
        Assert.Null(await store.LoadDraftAsync("nope", "missing"));
    }

    [Fact]
    public async Task LoadDraftAsync_returns_content_after_save()
    {
        var (store, _) = NewStore();
        await store.SaveDraftAsync("proj-b", "ch-2", "draft body");
        var result = await store.LoadDraftAsync("proj-b", "ch-2");
        Assert.Equal("draft body", result);
    }

    [Fact]
    public async Task SaveDraftAsync_overwrites_existing()
    {
        var (store, _) = NewStore();
        await store.SaveDraftAsync("proj-c", "ch-3", "v1");
        await store.SaveDraftAsync("proj-c", "ch-3", "v2");
        Assert.Equal("v2", await store.LoadDraftAsync("proj-c", "ch-3"));
    }

    [Fact]
    public async Task Empty_chapterId_throws()
    {
        var (store, _) = NewStore();
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveDraftAsync("p", "", "x"));
    }

    private static (FileChapterDraftStore store, string root) NewStore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-drafts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return (new FileChapterDraftStore(root), root);
    }
}
