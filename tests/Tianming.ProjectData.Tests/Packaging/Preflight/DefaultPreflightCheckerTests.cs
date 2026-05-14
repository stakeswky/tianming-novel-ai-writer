using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Xunit;

namespace Tianming.ProjectData.Tests.Packaging.Preflight;

public class DefaultPreflightCheckerTests
{
    [Fact]
    public async Task Warns_when_no_chapters_generated()
    {
        using var workspace = new TempDirectory();
        var checker = new DefaultPreflightChecker(workspace.Path);

        var report = await checker.CheckAsync();

        Assert.Contains(report.Issues, issue => issue.Code == "NoChapters");
    }

    [Fact]
    public async Task Warns_when_staged_changes_exist()
    {
        using var workspace = new TempDirectory();
        var stagedDir = Path.Combine(workspace.Path, ".staged");
        Directory.CreateDirectory(stagedDir);
        await File.WriteAllTextAsync(Path.Combine(stagedDir, "stg-001.json"), "{}");
        var checker = new DefaultPreflightChecker(workspace.Path);

        var report = await checker.CheckAsync();

        Assert.Contains(report.Issues, issue => issue.Code == "StagedChangesPending");
    }

    [Fact]
    public async Task Passes_when_chapters_exist_and_no_staging()
    {
        using var workspace = new TempDirectory();
        var chaptersDir = Path.Combine(workspace.Path, "Generated", "chapters");
        Directory.CreateDirectory(chaptersDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "content");
        var checker = new DefaultPreflightChecker(workspace.Path);

        var report = await checker.CheckAsync();

        Assert.True(report.IsPass);
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-pre-{System.Guid.NewGuid():N}");

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
