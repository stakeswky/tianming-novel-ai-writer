using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Publishing;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FilePackagePublisherTests
{
    [Fact]
    public async Task PublishDefaultAsync_packages_enabled_modules_updates_manifest_and_preserves_previous_history()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(new ManifestInfo
        {
            ProjectName = "旧项目",
            SourceBookId = "BOOK-A",
            Version = 1,
            PublishTime = new DateTime(2026, 5, 10, 8, 0, 0),
            Files = { ["Design"] = ["old.json"] },
            EnabledModules = { ["Design"] = new Dictionary<string, bool> { ["Elements"] = true } }
        });
        Directory.CreateDirectory(System.IO.Path.Combine(workspace.Path, "Published", "Design"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(workspace.Path, "Published", "Design", "old.json"), "{}");

        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var factionDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "FactionRules");
        var locationDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "LocationRules");
        var plotDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "PlotRules");
        var outlineDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "GlobalSettings", "Outline");
        var chaptersDir = System.IO.Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(factionDir);
        Directory.CreateDirectory(locationDir);
        Directory.CreateDirectory(plotDir);
        Directory.CreateDirectory(outlineDir);
        Directory.CreateDirectory(chaptersDir);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(characterDir, "Characters.json"),
            """[{ "Id": "C1", "Name": "林衡", "IsEnabled": true, "SourceBookId": "BOOK-A" }]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(factionDir, "Factions.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(locationDir, "Locations.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(plotDir, "Plots.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(outlineDir, "Outline.json"), """{ "Title": "天命" }""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(chaptersDir, "vol1_ch001.md"), "第一章\n星火入梦。");

        var publisher = new FilePackagePublisher(workspace.Path);

        var result = await publisher.PublishDefaultAsync(new PackagePublishRequest
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            EnabledModulePaths =
            {
                ["Design/GlobalSettings"] = false,
                ["Design/Elements"] = true,
                ["Generate/GlobalSettings"] = true,
                ["Generate/Elements"] = false
            }
        });

        var manifest = await store.GetManifestAsync();
        var history = await store.GetAllHistoryAsync(includeCurrent: false);
        using var elements = JsonDocument.Parse(await File.ReadAllTextAsync(
            System.IO.Path.Combine(workspace.Path, "Published", "Design", "elements.json")));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Version);
        Assert.Equal(["Design/Elements", "Generate/GlobalSettings"], result.PackagedModules);
        Assert.NotNull(manifest);
        Assert.Equal("天命", manifest.ProjectName);
        Assert.Equal("BOOK-A", manifest.SourceBookId);
        Assert.Equal(2, manifest.Version);
        Assert.Equal(["elements.json"], manifest.Files["Design"]);
        Assert.Equal(["globalsettings.json"], manifest.Files["Generate"]);
        Assert.True(manifest.EnabledModules["Design"]["Elements"]);
        Assert.False(manifest.EnabledModules["Design"]["GlobalSettings"]);
        Assert.Equal(1, manifest.Statistics.TotalCharacters);
        Assert.Equal(1, manifest.Statistics.TotalChapters);
        Assert.Equal(7, manifest.Statistics.TotalWords);
        Assert.Single(elements.RootElement.GetProperty("data").GetProperty("characterrules").GetProperty("characters").EnumerateArray());
        var entry = Assert.Single(history);
        Assert.Equal(1, entry.Version);
    }

    [Fact]
    public async Task PublishDefaultAsync_marks_packaged_module_statuses_latest()
    {
        using var workspace = new TempDirectory();
        var worldDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "GlobalSettings", "WorldRules");
        Directory.CreateDirectory(worldDir);
        await File.WriteAllTextAsync(System.IO.Path.Combine(worldDir, "WorldRules.json"), """[{ "Id": "W1", "SourceBookId": "BOOK-A" }]""");
        var publisher = new FilePackagePublisher(workspace.Path);

        var result = await publisher.PublishDefaultAsync(new PackagePublishRequest
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            EnabledModulePaths =
            {
                ["Design/GlobalSettings"] = true,
                ["Design/Elements"] = false,
                ["Generate/GlobalSettings"] = false,
                ["Generate/Elements"] = false
            }
        });

        var detector = new FileChangeDetectionStore(workspace.Path);
        var changedModules = await detector.GetChangedModulesAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("Design/GlobalSettings", changedModules);
    }

    [Fact]
    public async Task PublishDefaultAsync_fails_when_scope_is_missing_or_enabled_data_is_missing()
    {
        using var workspace = new TempDirectory();
        var publisher = new FilePackagePublisher(workspace.Path);

        var missingScope = await publisher.PublishDefaultAsync(new PackagePublishRequest { ProjectName = "天命" });
        var missingData = await publisher.PublishDefaultAsync(new PackagePublishRequest
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            EnabledModulePaths = { ["Design/Elements"] = true }
        });

        Assert.False(missingScope.IsSuccess);
        Assert.Contains("Scope", missingScope.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(missingData.IsSuccess);
        Assert.Contains("Design/Elements", missingData.Message);
    }

    [Fact]
    public async Task PublishModuleAsync_packages_only_requested_module_and_preserves_manifest_files_for_other_modules()
    {
        using var workspace = new TempDirectory();
        var store = new FilePackageManifestStore(workspace.Path);
        await store.SaveManifestAsync(new ManifestInfo
        {
            ProjectName = "天命",
            SourceBookId = "BOOK-A",
            Version = 3,
            PublishTime = new DateTime(2026, 5, 10, 9, 0, 0),
            Files =
            {
                ["Generate"] = ["globalsettings.json"]
            },
            EnabledModules =
            {
                ["Generate"] = new Dictionary<string, bool> { ["GlobalSettings"] = true }
            }
        });

        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var factionDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "FactionRules");
        var locationDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "LocationRules");
        var plotDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "PlotRules");
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(factionDir);
        Directory.CreateDirectory(locationDir);
        Directory.CreateDirectory(plotDir);
        await File.WriteAllTextAsync(System.IO.Path.Combine(characterDir, "Characters.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(factionDir, "Factions.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(locationDir, "Locations.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(plotDir, "Plots.json"), """[]""");

        var publisher = new FilePackagePublisher(workspace.Path);

        var result = await publisher.PublishModuleAsync(
            "Design",
            new PackagePublishRequest
            {
                ProjectName = "天命",
                SourceBookId = "BOOK-A",
                EnabledModulePaths =
                {
                    ["Design/GlobalSettings"] = false,
                    ["Design/Elements"] = true,
                    ["Generate/GlobalSettings"] = true,
                    ["Generate/Elements"] = false
                }
            });

        var manifest = await store.GetManifestAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Version);
        Assert.Equal(["Design/Elements"], result.PackagedModules);
        Assert.NotNull(manifest);
        Assert.Equal(["globalsettings.json"], manifest.Files["Generate"]);
        Assert.Equal(["elements.json"], manifest.Files["Design"]);
        Assert.True(File.Exists(System.IO.Path.Combine(workspace.Path, "Published", "Design", "elements.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "Published", "Generate", "elements.json")));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-package-publisher-{Guid.NewGuid():N}");

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
