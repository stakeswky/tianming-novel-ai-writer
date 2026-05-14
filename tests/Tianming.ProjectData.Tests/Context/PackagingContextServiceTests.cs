using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class PackagingContextServiceTests
{
    [Fact]
    public async Task BuildSnapshot_lists_all_chapters_and_design_refs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pc-{System.Guid.NewGuid():N}");
        var chaptersDir = Path.Combine(root, "Generate", "Chapters");
        var characterDir = Path.Combine(root, "Design", "Elements", "Characters");
        Directory.CreateDirectory(chaptersDir);
        Directory.CreateDirectory(characterDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "Chapter 1 content");
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-002.md"), "Chapter 2 content");
        await File.WriteAllTextAsync(
            Path.Combine(characterDir, "char-001.json"),
            "{\"Id\":\"char-001\",\"Name\":\"沈砚\",\"Summary\":\"剑客\"}");

        var design = new DesignContextService(root);
        var svc = new PackagingContextService(root, design);

        var snapshot = await svc.BuildSnapshotAsync();

        Assert.Equal(2, snapshot.ChapterIds.Count);
        Assert.Contains("ch-001", snapshot.ChapterIds);
        Assert.Contains("ch-002", snapshot.ChapterIds);
        Assert.Single(snapshot.AllDesignReferences);
        Assert.Equal(root, snapshot.ProjectRoot);
    }

    [Fact]
    public async Task BuildSnapshot_returns_empty_chapters_when_directory_is_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var design = new DesignContextService(root);
        var svc = new PackagingContextService(root, design);

        var snapshot = await svc.BuildSnapshotAsync();

        Assert.Empty(snapshot.ChapterIds);
        Assert.Equal(root, snapshot.ProjectRoot);
    }
}
