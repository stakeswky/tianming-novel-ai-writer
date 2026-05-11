using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableUIResolutionSettingsStoreTests
{
    [Fact]
    public void Default_settings_match_original_resolution_defaults()
    {
        var settings = PortableUIResolutionSettings.CreateDefault();

        Assert.Equal(1920, settings.WindowWidth);
        Assert.Equal(1080, settings.WindowHeight);
        Assert.True(settings.UsePreset);
        Assert.Equal(PortablePresetResolution.FullHD, settings.Preset);
        Assert.Equal(100, settings.ScalePercent);
        Assert.Equal(800, settings.MinWidth);
        Assert.Equal(600, settings.MinHeight);
        Assert.Equal(0, settings.MaxWidth);
        Assert.Equal(0, settings.MaxHeight);
    }

    [Theory]
    [InlineData(PortablePresetResolution.HD, 1280, 720, "720p (1280×720)")]
    [InlineData(PortablePresetResolution.FullHD, 1920, 1080, "1080p (1920×1080)")]
    [InlineData(PortablePresetResolution.QHD, 2560, 1440, "1440p (2560×1440)")]
    [InlineData(PortablePresetResolution.Custom, 1920, 1080, "自定义")]
    public void Preset_helpers_match_original_display_names(
        PortablePresetResolution preset,
        int expectedWidth,
        int expectedHeight,
        string expectedDisplayName)
    {
        var resolution = PortableUIResolutionSettings.GetPresetResolution(preset);

        Assert.Equal((expectedWidth, expectedHeight), resolution);
        Assert.Equal(expectedDisplayName, PortableUIResolutionSettings.GetPresetDisplayName(preset));
    }

    [Theory]
    [InlineData(PortableUIScaleLevel.Scale100, "100% (标准)")]
    [InlineData(PortableUIScaleLevel.Scale125, "125% (稍大)")]
    [InlineData(PortableUIScaleLevel.Scale150, "150% (较大)")]
    [InlineData(PortableUIScaleLevel.Scale200, "200% (很大)")]
    public void Scale_display_names_match_original_copy(
        PortableUIScaleLevel scale,
        string expectedDisplayName)
    {
        Assert.Equal(expectedDisplayName, PortableUIResolutionSettings.GetScaleDisplayName(scale));
    }

    [Fact]
    public void ValidateSize_enforces_minimum_and_optional_maximum()
    {
        var settings = PortableUIResolutionSettings.CreateDefault();
        settings.MaxWidth = 1600;
        settings.MaxHeight = 1000;

        Assert.False(settings.ValidateSize(799, 800));
        Assert.False(settings.ValidateSize(1200, 599));
        Assert.False(settings.ValidateSize(1601, 900));
        Assert.False(settings.ValidateSize(1200, 1001));
        Assert.True(settings.ValidateSize(1600, 1000));
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileUIResolutionSettingsStore(path);
        var settings = PortableUIResolutionSettings.CreateDefault();
        settings.WindowWidth = 1440;
        settings.WindowHeight = 900;
        settings.UsePreset = false;
        settings.Preset = PortablePresetResolution.Custom;
        settings.ScalePercent = 125;

        await store.SaveAsync(settings);
        var reloaded = await new FileUIResolutionSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(1440, reloaded.WindowWidth);
        Assert.Equal(900, reloaded.WindowHeight);
        Assert.False(reloaded.UsePreset);
        Assert.Equal(PortablePresetResolution.Custom, reloaded.Preset);
        Assert.Equal(125, reloaded.ScalePercent);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var missingStore = new FileUIResolutionSettingsStore(path);

        Assert.Equal(1920, (await missingStore.LoadAsync()).WindowWidth);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var invalidStore = new FileUIResolutionSettingsStore(path);

        Assert.Equal(1080, (await invalidStore.LoadAsync()).WindowHeight);
    }
}
