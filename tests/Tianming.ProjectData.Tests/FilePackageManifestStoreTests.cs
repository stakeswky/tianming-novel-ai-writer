using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Publishing;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FilePackageManifestStoreTests
{
    [Fact]
    public async Task SaveManifestAsync_persists_manifest_and_reports_publish_status()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        var manifest = CreateManifest(2, designCharactersEnabled: true, generateChapterEnabled: false);

        await store.SaveManifestAsync(manifest);

        var loaded = await store.GetManifestAsync();
        var status = await store.GetPublishStatusAsync(changedModuleCount: 3);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
        Assert.Equal("BOOK-A", loaded.SourceBookId);
        Assert.True(status.IsPublished);
        Assert.Equal(2, status.CurrentVersion);
        Assert.True(status.NeedsRepublish);
        Assert.Equal(3, status.ChangedModuleCount);
    }

    [Fact]
    public async Task SaveCurrentToHistoryAsync_copies_manifest_and_retains_newest_versions()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path) { RetainCount = 2 };

        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(2, designCharactersEnabled: false, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(3, designCharactersEnabled: true, generateChapterEnabled: false));
        Assert.True(await store.SaveCurrentToHistoryAsync());

        var history = await store.GetAllHistoryAsync(includeCurrent: false);

        Assert.Equal([3, 2], history.Select(entry => entry.Version).ToList());
        Assert.False(Directory.Exists(System.IO.Path.Combine(workspace.Path, "History", "v1")));
    }

    [Fact]
    public async Task GetVersionDiffAsync_reports_enabled_module_changes()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);

        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(2, designCharactersEnabled: false, generateChapterEnabled: true));

        var diff = await store.GetVersionDiffAsync(1);

        Assert.Equal(2, diff.CurrentVersion);
        Assert.Equal(1, diff.HistoryVersion);
        var item = Assert.Single(diff.DiffItems);
        Assert.Equal("Design/Characters", item.ModulePath);
        Assert.Equal(DiffType.EnabledChanged, item.Type);
        Assert.Equal("禁用", item.CurrentState);
        Assert.Equal("启用", item.HistoryState);
    }

    [Fact]
    public async Task RestoreVersionAsync_restores_history_manifest_and_preserves_previous_current_in_history()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);

        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(2, designCharactersEnabled: false, generateChapterEnabled: false));

        var restored = await store.RestoreVersionAsync(1);

        var current = await store.GetManifestAsync();
        var history = await store.GetAllHistoryAsync(includeCurrent: false);

        Assert.True(restored);
        Assert.NotNull(current);
        Assert.Equal(1, current.Version);
        Assert.True(current.EnabledModules["Design"]["Characters"]);
        Assert.Contains(history, entry => entry.Version == 2);
    }

    [Fact]
    public async Task RestoreVersionAsync_blocks_generated_content_overwrite_without_confirmation()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(2, designCharactersEnabled: false, generateChapterEnabled: false));
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(workspace.Path, "Generated", "vol1_ch001.md"), "正文");

        var restored = await store.RestoreVersionAsync(1);

        var current = await store.GetManifestAsync();
        Assert.False(restored);
        Assert.NotNull(current);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task RestoreVersionAsync_allows_generated_content_overwrite_when_confirmed()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        await store.SaveManifestAsync(CreateManifest(2, designCharactersEnabled: false, generateChapterEnabled: false));
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(workspace.Path, "Generated", "vol1_ch001.md"), "正文");

        var restored = await store.RestoreVersionAsync(1, allowGeneratedContentOverwrite: true);

        var current = await store.GetManifestAsync();
        Assert.True(restored);
        Assert.NotNull(current);
        Assert.Equal(1, current.Version);
    }

    [Fact]
    public async Task CanRestoreVersionAsync_reports_missing_version_and_generated_content_confirmation_requirement()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(workspace.Path, "Generated", "vol1_ch001.md"), "正文");

        var missing = await store.CanRestoreVersionAsync(99);
        var existing = await store.CanRestoreVersionAsync(1);

        Assert.False(missing.CanRestore);
        Assert.False(missing.RequiresGeneratedContentConfirmation);
        Assert.Contains("不存在", missing.Message);
        Assert.False(existing.CanRestore);
        Assert.True(existing.RequiresGeneratedContentConfirmation);
        Assert.Contains("生成正文", existing.Message);
    }

    [Fact]
    public async Task ClearAllAsync_removes_manifest_history_and_generated_outputs()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(CreateManifest(1, designCharactersEnabled: true, generateChapterEnabled: true));
        Assert.True(await store.SaveCurrentToHistoryAsync());
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "Generated"));
        File.WriteAllText(System.IO.Path.Combine(workspace.Path, "Generated", "vol1_ch1.md"), "正文");
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "VectorIndex"));
        File.WriteAllText(System.IO.Path.Combine(workspace.Path, "vector_degraded.flag"), "1");

        await store.ClearAllAsync();

        Assert.Null(await store.GetManifestAsync());
        Assert.Empty(await store.GetAllHistoryAsync(includeCurrent: false));
        Assert.False(Directory.Exists(System.IO.Path.Combine(workspace.Path, "Generated")));
        Assert.False(Directory.Exists(System.IO.Path.Combine(workspace.Path, "VectorIndex")));
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "vector_degraded.flag")));
    }

    private static ManifestInfo CreateManifest(int version, bool designCharactersEnabled, bool generateChapterEnabled)
    {
        return new ManifestInfo
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            Version = version,
            PublishTime = new DateTime(2026, 5, 10, 12, version, 0),
            Files =
            {
                ["Design"] = ["characters.json"],
                ["Generate"] = ["chapter.json"]
            },
            EnabledModules =
            {
                ["Design"] = new Dictionary<string, bool> { ["Characters"] = designCharactersEnabled },
                ["Generate"] = new Dictionary<string, bool> { ["Chapter"] = generateChapterEnabled }
            },
            Statistics = new StatisticsInfo
            {
                TotalCharacters = 7,
                TotalLocations = 3,
                TotalChapters = 12,
                TotalWords = 34567
            }
        };
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-package-{Guid.NewGuid():N}");

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
