using System.Text.Json;
using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontConfigurationStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_default_configuration_when_file_is_missing()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontConfigurationStore(Path.Combine(workspace.Path, "font_config.json"));

        var config = await store.LoadAsync();

        Assert.Equal("Microsoft YaHei UI", config.UIFont.FontFamily);
        Assert.Equal(14, config.UIFont.FontSize);
        Assert.Equal("Consolas", config.EditorFont.FontFamily);
        Assert.Equal(13, config.EditorFont.FontSize);
        Assert.Equal(1.6, config.EditorFont.LineHeight);
    }

    [Fact]
    public async Task SaveAsync_persists_ui_and_editor_font_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "font_config.json");
        var store = new FileFontConfigurationStore(path);
        var config = PortableFontConfiguration.CreateDefault();
        config.UIFont = new PortableFontSettings { FontFamily = "PingFang SC", FontSize = 15, FontWeight = "Medium" };
        config.EditorFont = new PortableFontSettings { FontFamily = "JetBrains Mono", FontSize = 12, LineHeight = 1.7 };

        await store.SaveAsync(config);
        var reloaded = await new FileFontConfigurationStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("PingFang SC", reloaded.UIFont.FontFamily);
        Assert.Equal("Medium", reloaded.UIFont.FontWeight);
        Assert.Equal("JetBrains Mono", reloaded.EditorFont.FontFamily);
        Assert.Equal(1.7, reloaded.EditorFont.LineHeight);
    }

    [Fact]
    public async Task UpdateUIFontAsync_preserves_editor_font()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontConfigurationStore(Path.Combine(workspace.Path, "font_config.json"));
        await store.SaveAsync(new PortableFontConfiguration
        {
            UIFont = new PortableFontSettings { FontFamily = "Arial" },
            EditorFont = new PortableFontSettings { FontFamily = "Monaco", FontSize = 11 }
        });

        await store.UpdateUIFontAsync(new PortableFontSettings { FontFamily = "Helvetica Neue", FontSize = 16 });
        var config = await store.LoadAsync();

        Assert.Equal("Helvetica Neue", config.UIFont.FontFamily);
        Assert.Equal(16, config.UIFont.FontSize);
        Assert.Equal("Monaco", config.EditorFont.FontFamily);
        Assert.Equal(11, config.EditorFont.FontSize);
    }

    [Fact]
    public async Task ImportConfigurationAsync_loads_valid_configuration_and_rejects_invalid_files()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontConfigurationStore(Path.Combine(workspace.Path, "font_config.json"));
        var importPath = Path.Combine(workspace.Path, "import.json");
        var invalidPath = Path.Combine(workspace.Path, "invalid.json");
        await File.WriteAllTextAsync(
            importPath,
            JsonSerializer.Serialize(new PortableFontConfiguration
            {
                UIFont = new PortableFontSettings { FontFamily = "SF Pro Text", FontSize = 14 },
                EditorFont = new PortableFontSettings { FontFamily = "Menlo", FontSize = 12 }
            }));
        await File.WriteAllTextAsync(invalidPath, "{ invalid json");

        var imported = await store.ImportConfigurationAsync(importPath);
        var rejected = await store.ImportConfigurationAsync(invalidPath);
        var config = await store.LoadAsync();

        Assert.True(imported);
        Assert.False(rejected);
        Assert.Equal("SF Pro Text", config.UIFont.FontFamily);
        Assert.Equal("Menlo", config.EditorFont.FontFamily);
    }

    [Fact]
    public async Task ExportConfigurationAsync_writes_configuration_file()
    {
        using var workspace = new TempDirectory();
        var exportPath = Path.Combine(workspace.Path, "export.json");
        var store = new FileFontConfigurationStore(Path.Combine(workspace.Path, "font_config.json"));
        await store.SaveAsync(new PortableFontConfiguration
        {
            UIFont = new PortableFontSettings { FontFamily = "PingFang SC" },
            EditorFont = new PortableFontSettings { FontFamily = "Menlo" }
        });

        await store.ExportConfigurationAsync(exportPath);
        var exported = JsonSerializer.Deserialize<PortableFontConfiguration>(await File.ReadAllTextAsync(exportPath));

        Assert.False(File.Exists(exportPath + ".tmp"));
        Assert.NotNull(exported);
        Assert.Equal("PingFang SC", exported!.UIFont.FontFamily);
        Assert.Equal("Menlo", exported.EditorFont.FontFamily);
    }

    [Fact]
    public async Task ExportShareableAsync_writes_versioned_package()
    {
        using var workspace = new TempDirectory();
        var exportPath = Path.Combine(workspace.Path, "font.fontshare");
        var store = new FileFontConfigurationStore(
            Path.Combine(workspace.Path, "font_config.json"),
            () => new DateTime(2026, 5, 11, 15, 30, 0),
            () => "tester");

        await store.ExportShareableAsync(exportPath);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(exportPath));

        Assert.Equal("1.0", document.RootElement.GetProperty("Version").GetString());
        Assert.Equal("tester", document.RootElement.GetProperty("ExportBy").GetString());
        Assert.True(document.RootElement.TryGetProperty("Configuration", out var configuration));
        Assert.Equal("Consolas", configuration.GetProperty("editorFont").GetProperty("fontFamily").GetString());
    }

    [Fact]
    public async Task LoadAsync_recovers_from_invalid_json_or_incomplete_configuration()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "font_config.json");
        await File.WriteAllTextAsync(path, "{\"uiFont\":null}");
        var store = new FileFontConfigurationStore(path);

        var config = await store.LoadAsync();

        Assert.Equal("Microsoft YaHei UI", config.UIFont.FontFamily);
        Assert.Equal("Consolas", config.EditorFont.FontFamily);
    }
}
