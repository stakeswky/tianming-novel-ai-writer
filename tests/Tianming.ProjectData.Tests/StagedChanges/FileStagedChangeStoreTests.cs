using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.ProjectData.Tests.StagedChanges;

public sealed class FileStagedChangeStoreTests
{
    [Fact]
    public async Task Stage_assigns_id_and_returns_it()
    {
        var dir = CreateTempDir();
        var store = new FileStagedChangeStore(dir);

        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-001",
            NewContentSnippet = "new content",
        });

        Assert.False(string.IsNullOrEmpty(id));
        Assert.StartsWith("stg-", id);
    }

    [Fact]
    public async Task Get_returns_staged_change_by_id()
    {
        var dir = CreateTempDir();
        var store = new FileStagedChangeStore(dir);

        var id = await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        var back = await store.GetAsync(id);

        Assert.NotNull(back);
        Assert.Equal("ch-001", back!.TargetId);
    }

    [Fact]
    public async Task ListPending_returns_all_staged_changes()
    {
        var dir = CreateTempDir();
        var store = new FileStagedChangeStore(dir);

        await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        await store.StageAsync(new StagedChange { TargetId = "ch-002" });

        var pending = await store.ListPendingAsync();

        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task Remove_clears_staged_change()
    {
        var dir = CreateTempDir();
        var store = new FileStagedChangeStore(dir);

        var id = await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        await store.RemoveAsync(id);

        Assert.Null(await store.GetAsync(id));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-stg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
