using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class PackagingContextServiceTests
{
    [Fact]
    public async Task BuildSnapshot_lists_generated_chapters_from_real_contract_and_design_refs()
    {
        using var workspace = new TempDirectory();
        var chaptersDir = Path.Combine(workspace.Path, "Generated", "chapters");
        Directory.CreateDirectory(chaptersDir);
        var chapterStore = new ChapterContentStore(chaptersDir);
        await chapterStore.SaveChapterAsync("vol1_ch1", "# 第1章 山门\n\nChapter 1 content");
        await chapterStore.SaveChapterAsync("vol1_ch2", "# 第2章 夜路\n\nChapter 2 content");
        await WriteModuleDataAsync(
            workspace.Path,
            Path.Combine("Design", "Elements", "Characters"),
            new object[]
            {
                new
                {
                    Id = "char-001",
                    Name = "沈砚",
                    Category = "角色",
                    Description = "剑客",
                }
            });

        var design = new DesignContextService(workspace.Path);
        var svc = new PackagingContextService(workspace.Path, design);

        var snapshot = await svc.BuildSnapshotAsync();

        Assert.Equal(2, snapshot.ChapterIds.Count);
        Assert.Contains("vol1_ch1", snapshot.ChapterIds);
        Assert.Contains("vol1_ch2", snapshot.ChapterIds);
        Assert.Single(snapshot.AllDesignReferences);
        Assert.Equal(workspace.Path, snapshot.ProjectRoot);
    }

    [Fact]
    public async Task BuildSnapshot_returns_empty_chapters_when_directory_is_missing()
    {
        using var workspace = new TempDirectory();
        var design = new DesignContextService(workspace.Path);
        var svc = new PackagingContextService(workspace.Path, design);

        var snapshot = await svc.BuildSnapshotAsync();

        Assert.Empty(snapshot.ChapterIds);
        Assert.Equal(workspace.Path, snapshot.ProjectRoot);
    }

    private static async Task WriteModuleDataAsync(string projectRoot, string moduleRelativePath, object[] data)
    {
        var moduleDir = Path.Combine(projectRoot, moduleRelativePath);
        Directory.CreateDirectory(moduleDir);
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "categories.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "built_in_categories.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "data.json"), JsonSerializer.Serialize(data));
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-pc-{System.Guid.NewGuid():N}");

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
