using TM.Modules.AIAssistant.PromptTools.VersionTesting.Models;
using TM.Modules.AIAssistant.PromptTools.VersionTesting.Services;
using Xunit;

namespace Tianming.AI.Tests;

public class FilePromptVersionStoreTests
{
    [Fact]
    public void Loads_versions_ordered_by_created_time_descending()
    {
        using var workspace = new TempDirectory();
        var versionsPath = Path.Combine(workspace.Path, "test_versions.json");
        File.WriteAllText(versionsPath, """
        [
          { "Id": "older", "Name": "Older", "CreatedTime": "2026-01-01T00:00:00Z", "ModifiedTime": "2026-01-01T00:00:00Z" },
          { "Id": "newer", "Name": "Newer", "CreatedTime": "2026-02-01T00:00:00Z", "ModifiedTime": "2026-02-01T00:00:00Z" }
        ]
        """);

        var store = new FilePromptVersionStore(versionsPath);

        Assert.Equal(["newer", "older"], store.GetAllVersions().Select(version => version.Id).ToArray());
    }

    [Fact]
    public void Add_update_delete_and_clear_versions_persist_changes()
    {
        using var workspace = new TempDirectory();
        var versionsPath = Path.Combine(workspace.Path, "test_versions.json");
        var store = new FilePromptVersionStore(versionsPath);

        store.AddVersion(new TestVersionData
        {
            Id = "v1",
            Name = "初版测试",
            PromptId = "prompt-1",
            VersionNumber = "1.0",
            TestInput = "输入",
            ExpectedOutput = "输出"
        });
        store.UpdateVersion(new TestVersionData
        {
            Id = "v1",
            Name = "初版测试",
            PromptId = "prompt-1",
            VersionNumber = "1.1",
            ActualOutput = "实际输出",
            Rating = 4,
            TestNotes = "可用",
            TestStatus = "已通过",
            TestTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)
        });

        var reloaded = new FilePromptVersionStore(versionsPath);
        var version = Assert.Single(reloaded.GetAllVersions());
        Assert.Equal("1.1", version.VersionNumber);
        Assert.Equal("实际输出", version.ActualOutput);
        Assert.Equal(4, version.Rating);
        Assert.Equal("已通过", version.TestStatus);

        reloaded.DeleteVersion("v1");
        Assert.Empty(new FilePromptVersionStore(versionsPath).GetAllVersions());

        reloaded.AddVersion(new TestVersionData { Id = "v2", Name = "A" });
        reloaded.AddVersion(new TestVersionData { Id = "v3", Name = "B" });
        Assert.Equal(2, reloaded.ClearAllVersions());
        Assert.Empty(new FilePromptVersionStore(versionsPath).GetAllVersions());
    }

    [Fact]
    public void AddVersion_rejects_missing_name()
    {
        using var workspace = new TempDirectory();
        var store = new FilePromptVersionStore(Path.Combine(workspace.Path, "test_versions.json"));

        var ex = Assert.Throws<ArgumentException>(() => store.AddVersion(new TestVersionData()));

        Assert.Contains("版本名称不能为空", ex.Message);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-prompt-version-{Guid.NewGuid():N}");

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
