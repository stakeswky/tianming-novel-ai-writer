using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileModuleEnabledStoreTests
{
    [Fact]
    public async Task SetModuleEnabledAsync_updates_all_function_json_arrays_except_categories()
    {
        using var workspace = new TempDirectory();
        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var factionDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "FactionRules");
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(factionDir);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(characterDir, "Characters.json"),
            """[{ "Id": "C1", "IsEnabled": true }, { "Id": "C2", "IsEnabled": true }]""");
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(characterDir, "categories.json"),
            """[{ "Id": "cat", "IsEnabled": true }]""");
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(factionDir, "Factions.json"),
            """[{ "Id": "F1", "IsEnabled": true }]""");

        var store = new FileModuleEnabledStore(workspace.Path);

        var updated = await store.SetModuleEnabledAsync("Design", "Elements", enabled: false);
        var stats = await store.GetModuleEnabledStatsAsync("Design", "Elements");
        using var characters = JsonDocument.Parse(await File.ReadAllTextAsync(System.IO.Path.Combine(characterDir, "Characters.json")));
        using var categories = JsonDocument.Parse(await File.ReadAllTextAsync(System.IO.Path.Combine(characterDir, "categories.json")));

        Assert.Equal(3, updated);
        Assert.Equal(0, stats.EnabledCount);
        Assert.Equal(3, stats.TotalCount);
        Assert.All(characters.RootElement.EnumerateArray(), item => Assert.False(item.GetProperty("IsEnabled").GetBoolean()));
        Assert.True(categories.RootElement[0].GetProperty("IsEnabled").GetBoolean());
    }

    [Fact]
    public async Task GetEnabledModulePathsAsync_returns_false_for_modules_with_no_enabled_items()
    {
        using var workspace = new TempDirectory();
        var outlineDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "GlobalSettings", "Outline");
        var chapterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "Elements", "Chapter");
        Directory.CreateDirectory(outlineDir);
        Directory.CreateDirectory(chapterDir);
        await File.WriteAllTextAsync(System.IO.Path.Combine(outlineDir, "Outline.json"), """[{ "Id": "O1", "IsEnabled": true }]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(chapterDir, "Chapter.json"), """[{ "Id": "CH1", "IsEnabled": false }]""");

        var store = new FileModuleEnabledStore(workspace.Path);

        var enabled = await store.GetEnabledModulePathsAsync();

        Assert.True(enabled["Generate/GlobalSettings"]);
        Assert.False(enabled["Generate/Elements"]);
        Assert.True(enabled["Design/GlobalSettings"]);
        Assert.True(enabled["Design/Elements"]);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-module-enabled-{Guid.NewGuid():N}");

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
