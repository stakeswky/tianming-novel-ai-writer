using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeResourcePaletteTests
{
    [Fact]
    public void GetBrushes_returns_original_required_light_theme_keys()
    {
        var brushes = PortableThemeResourcePalette.GetBrushes(PortableThemeType.Light);

        Assert.Equal("#F1F5F9", brushes["UnifiedBackground"]);
        Assert.Equal("#FFFFFF", brushes["ContentBackground"]);
        Assert.Equal("#1E293B", brushes["TextPrimary"]);
        Assert.Equal("#3B82F6", brushes["PrimaryColor"]);
        Assert.Equal("#DC2626", brushes["DangerHover"]);
        Assert.Equal(PortableThemeResourcePalette.RequiredBrushKeys.Count, brushes.Count);
    }

    [Fact]
    public void GetBrushes_returns_original_dark_and_modern_blue_theme_values()
    {
        var dark = PortableThemeResourcePalette.GetBrushes(PortableThemeType.Dark);
        var modernBlue = PortableThemeResourcePalette.GetBrushes(PortableThemeType.ModernBlue);

        Assert.Equal("#0F172A", dark["UnifiedBackground"]);
        Assert.Equal("#F1F5F9", dark["TextPrimary"]);
        Assert.Equal("#60A5FA", dark["PrimaryColor"]);
        Assert.Equal("#0A1628", modernBlue["UnifiedBackground"]);
        Assert.Equal("#112240", modernBlue["ContentBackground"]);
        Assert.Equal("#1890FF", modernBlue["PrimaryColor"]);
    }

    [Fact]
    public void GetBrushes_resolves_auto_from_system_snapshot()
    {
        var brushes = PortableThemeResourcePalette.GetBrushes(
            PortableThemeType.Auto,
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal("#0F172A", brushes["UnifiedBackground"]);
        Assert.Equal("#60A5FA", brushes["PrimaryColor"]);
    }

    [Fact]
    public void GetBrushes_returns_high_contrast_accessibility_palette()
    {
        var brushes = PortableThemeResourcePalette.GetBrushes(PortableThemeType.HighContrast);

        Assert.Equal("#000000", brushes["UnifiedBackground"]);
        Assert.Equal("#000000", brushes["ContentBackground"]);
        Assert.Equal("#FFFFFF", brushes["TextPrimary"]);
        Assert.Equal("#FFFF00", brushes["PrimaryColor"]);
    }

    [Fact]
    public void GetAllBuiltInPalettes_covers_all_planner_builtins_with_required_keys()
    {
        var palettes = PortableThemeResourcePalette.GetAllBuiltInPalettes();

        Assert.Equal(15, palettes.Count);
        foreach (var palette in palettes)
        {
            Assert.All(PortableThemeResourcePalette.RequiredBrushKeys, key => Assert.True(palette.Brushes.ContainsKey(key), key));
            Assert.All(palette.Brushes.Values, value => Assert.Matches("^#[0-9A-F]{6}$", value));
        }
    }

    [Fact]
    public void GetBrushes_rejects_custom_without_resource_file()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            PortableThemeResourcePalette.GetBrushes(PortableThemeType.Custom));

        Assert.Contains("custom", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
