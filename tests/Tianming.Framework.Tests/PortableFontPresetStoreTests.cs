using System.Text.Json;
using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontPresetStoreTests
{
    [Fact]
    public async Task GetAllPresetsAsync_returns_builtin_presets_before_custom_presets()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontPresetStore(Path.Combine(workspace.Path, "presets.json"));

        await store.SaveAsPresetAsync(
            "Mac Custom",
            "custom font",
            new PortableFontSettings { FontFamily = "PingFang SC", FontSize = 15 });

        var presets = await store.GetAllPresetsAsync();

        Assert.Equal("办公场景", presets[0].Name);
        Assert.True(presets[0].IsBuiltIn);
        Assert.Equal("Mac Custom", presets[^1].Name);
        Assert.False(presets[^1].IsBuiltIn);
    }

    [Fact]
    public async Task SaveAsPresetAsync_updates_existing_custom_preset_by_name()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontPresetStore(Path.Combine(workspace.Path, "presets.json"));

        await store.SaveAsPresetAsync(
            "Draft",
            "first",
            new PortableFontSettings { FontFamily = "Arial", FontSize = 14 });
        await store.SaveAsPresetAsync(
            "Draft",
            "updated",
            new PortableFontSettings { FontFamily = "Monaco", FontSize = 12, LineHeight = 1.7 });

        var custom = await store.GetCustomPresetsAsync();

        var preset = Assert.Single(custom);
        Assert.Equal("updated", preset.Description);
        Assert.Equal("Monaco", preset.FontFamily);
        Assert.Equal(12, preset.FontSize);
        Assert.Equal(1.7, preset.LineHeight);
    }

    [Fact]
    public async Task DeletePresetAsync_removes_custom_preset_but_not_builtin_preset()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontPresetStore(Path.Combine(workspace.Path, "presets.json"));

        await store.SaveAsPresetAsync(
            "Disposable",
            "remove me",
            new PortableFontSettings { FontFamily = "Arial" });
        var removedCustom = await store.DeletePresetAsync("Disposable");
        var removedBuiltIn = await store.DeletePresetAsync("办公场景");

        Assert.True(removedCustom);
        Assert.False(removedBuiltIn);
        Assert.Empty(await store.GetCustomPresetsAsync());
    }

    [Fact]
    public async Task ImportPresetsAsync_marks_imports_as_custom_and_skips_duplicate_names()
    {
        using var workspace = new TempDirectory();
        var importPath = Path.Combine(workspace.Path, "import.json");
        var store = new FileFontPresetStore(Path.Combine(workspace.Path, "presets.json"));
        await store.SaveAsPresetAsync("Existing", "old", new PortableFontSettings { FontFamily = "Arial" });
        await File.WriteAllTextAsync(
            importPath,
            JsonSerializer.Serialize(new[]
            {
                new PortableFontPreset { Name = "Existing", Description = "duplicate", IsBuiltIn = true },
                new PortableFontPreset { Name = "Imported", Description = "new", FontFamily = "Fira Code", IsBuiltIn = true }
            }));

        var result = await store.ImportPresetsAsync(importPath);
        var custom = await store.GetCustomPresetsAsync();

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        var imported = Assert.Single(custom, preset => preset.Name == "Imported");
        Assert.False(imported.IsBuiltIn);
        Assert.Equal("Fira Code", imported.FontFamily);
    }

    [Fact]
    public async Task ExportPresetsAsync_writes_selected_presets_atomically()
    {
        using var workspace = new TempDirectory();
        var exportPath = Path.Combine(workspace.Path, "export.json");
        var store = new FileFontPresetStore(Path.Combine(workspace.Path, "presets.json"));

        await store.ExportPresetsAsync(
            exportPath,
            [new PortableFontPreset { Name = "Share", FontFamily = "JetBrains Mono", FontSize = 13 }]);

        var json = await File.ReadAllTextAsync(exportPath);
        var exported = JsonSerializer.Deserialize<List<PortableFontPreset>>(json);

        Assert.False(File.Exists(exportPath + ".tmp"));
        Assert.NotNull(exported);
        Assert.Equal("Share", Assert.Single(exported!).Name);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "presets.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var store = new FileFontPresetStore(path);

        var custom = await store.GetCustomPresetsAsync();

        Assert.Empty(custom);
    }
}
