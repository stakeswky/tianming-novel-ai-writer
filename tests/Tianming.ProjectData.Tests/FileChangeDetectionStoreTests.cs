using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.ChangeDetection;
using TM.Services.Modules.ProjectData.Models.Publishing;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileChangeDetectionStoreTests
{
    [Fact]
    public async Task GetAllStatusesAsync_reports_never_changed_and_latest_against_manifest_time()
    {
        using var workspace = new TempDirectory();
        var publishTime = new DateTime(2026, 5, 10, 10, 0, 0);
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(new ManifestInfo
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            PublishTime = publishTime,
            Version = 1
        });

        var worldDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "GlobalSettings", "WorldRules");
        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var factionDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "FactionRules");
        Directory.CreateDirectory(worldDir);
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(factionDir);
        var worldFile = System.IO.Path.Combine(worldDir, "WorldRules.json");
        var characterFile = System.IO.Path.Combine(characterDir, "Characters.json");
        var categoryFile = System.IO.Path.Combine(characterDir, "categories.json");
        var factionFile = System.IO.Path.Combine(factionDir, "Factions.json");
        await File.WriteAllTextAsync(worldFile, """[{ "Id": "W1" }]""");
        await File.WriteAllTextAsync(characterFile, """[{ "Id": "C1" }, { "Id": "C2" }]""");
        await File.WriteAllTextAsync(categoryFile, """[{ "Id": "cat" }]""");
        await File.WriteAllTextAsync(factionFile, """[{ "Id": "F1" }]""");
        File.SetLastWriteTime(worldFile, publishTime.AddMinutes(-5));
        File.SetLastWriteTime(characterFile, publishTime.AddMinutes(5));
        File.SetLastWriteTime(categoryFile, publishTime.AddMinutes(10));
        File.SetLastWriteTime(factionFile, publishTime.AddMinutes(-1));

        var detector = new FileChangeDetectionStore(workspace.Path);

        var statuses = await detector.GetAllStatusesAsync();

        var world = Assert.Single(statuses, status => status.ModulePath == "Design/GlobalSettings");
        var elements = Assert.Single(statuses, status => status.ModulePath == "Design/Elements");
        var generate = Assert.Single(statuses, status => status.ModulePath == "Generate/GlobalSettings");
        Assert.Equal(ChangeStatusType.Latest, world.Status);
        Assert.Equal(1, world.ItemCount);
        Assert.Equal(ChangeStatusType.Changed, elements.Status);
        Assert.Equal(3, elements.ItemCount);
        Assert.Equal(ChangeStatusType.Never, generate.Status);
    }

    [Fact]
    public async Task GetChangedModulesAsync_and_mark_all_as_packaged_update_statuses()
    {
        using var workspace = new TempDirectory();
        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var worldDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "GlobalSettings", "WorldRules");
        var factionDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "FactionRules");
        var locationDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "LocationRules");
        var plotDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "PlotRules");
        var outlineDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "GlobalSettings", "Outline");
        var volumeDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "Elements", "VolumeDesign");
        var chapterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "Elements", "Chapter");
        var blueprintDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "Elements", "Blueprint");
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(worldDir);
        Directory.CreateDirectory(factionDir);
        Directory.CreateDirectory(locationDir);
        Directory.CreateDirectory(plotDir);
        Directory.CreateDirectory(outlineDir);
        Directory.CreateDirectory(volumeDir);
        Directory.CreateDirectory(chapterDir);
        Directory.CreateDirectory(blueprintDir);
        var fileWriteTime = new DateTime(2026, 5, 10, 10, 0, 0);
        var files = new[]
        {
            System.IO.Path.Combine(characterDir, "Characters.json"),
            System.IO.Path.Combine(worldDir, "WorldRules.json"),
            System.IO.Path.Combine(factionDir, "Factions.json"),
            System.IO.Path.Combine(locationDir, "Locations.json"),
            System.IO.Path.Combine(plotDir, "Plots.json"),
            System.IO.Path.Combine(outlineDir, "Outline.json"),
            System.IO.Path.Combine(volumeDir, "VolumeDesign.json"),
            System.IO.Path.Combine(chapterDir, "Chapter.json"),
            System.IO.Path.Combine(blueprintDir, "Blueprint.json")
        };
        await File.WriteAllTextAsync(files[0], """[{ "Id": "C1" }]""");
        foreach (var file in files.Skip(1))
            await File.WriteAllTextAsync(file, """[]""");
        foreach (var file in files)
            File.SetLastWriteTime(file, fileWriteTime);
        var detector = new FileChangeDetectionStore(workspace.Path);

        var changedBefore = await detector.GetChangedModulesAsync();
        await detector.MarkAllAsPackagedAsync(new DateTime(2026, 5, 10, 11, 0, 0));
        var changedAfter = await detector.GetChangedModulesAsync();

        Assert.Contains("Design/Elements", changedBefore);
        Assert.Empty(changedAfter);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-change-detection-{Guid.NewGuid():N}");

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
