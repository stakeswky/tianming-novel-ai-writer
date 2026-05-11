using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableImageColorAnalyzerTests
{
    [Fact]
    public void ExtractPalette_filters_extreme_pixels_and_returns_diverse_quantized_colors()
    {
        var pixels = Repeat(new PortableRgbColor(230, 40, 40), 20)
            .Concat(Repeat(new PortableRgbColor(45, 45, 230), 12))
            .Concat(Repeat(new PortableRgbColor(5, 5, 5), 8))
            .Concat(Repeat(new PortableRgbColor(250, 250, 250), 8));

        var palette = PortableImageColorAnalyzer.ExtractPalette(pixels, 2);

        Assert.Equal(2, palette.Count);
        Assert.Equal(new PortableRgbColor(210, 30, 30), palette[0]);
        Assert.Equal(new PortableRgbColor(30, 30, 210), palette[1]);
    }

    [Fact]
    public void ExtractPalette_returns_neutral_fallback_for_empty_pixels_or_invalid_count()
    {
        Assert.Equal(
            new[] { new PortableRgbColor(100, 100, 100) },
            PortableImageColorAnalyzer.ExtractPalette(Array.Empty<PortableRgbColor>(), 3));

        Assert.Empty(PortableImageColorAnalyzer.ExtractPalette(
            new[] { new PortableRgbColor(120, 120, 120) },
            0));
    }

    [Fact]
    public void Analyze_recommends_dark_theme_when_dark_pixels_dominate()
    {
        var pixels = Repeat(new PortableRgbColor(20, 20, 20), 7)
            .Concat(Repeat(new PortableRgbColor(180, 180, 180), 3));

        var result = PortableImageColorAnalyzer.Analyze(pixels);

        Assert.True(result.IsDark);
        Assert.Equal("dark", result.ThemeType);
        Assert.Equal("#ffffff", result.TextColor);
        Assert.Equal(0.7, result.DarkRatio, precision: 3);
        Assert.Contains("暗色图片", result.Notes);
    }

    [Fact]
    public void Analyze_recommends_light_theme_and_notes_very_bright_images()
    {
        var result = PortableImageColorAnalyzer.Analyze(Repeat(new PortableRgbColor(245, 245, 245), 5));

        Assert.False(result.IsDark);
        Assert.Equal("light", result.ThemeType);
        Assert.Equal("#212529", result.TextColor);
        Assert.Equal(1.0, result.LightRatio, precision: 3);
        Assert.Contains("图片非常亮", result.Notes);
    }

    [Fact]
    public void ReadBgra32Pixels_converts_channels_and_respects_stride_padding()
    {
        byte[] bgra =
        {
            30, 20, 10, 255,
            60, 50, 40, 128,
            99, 99,
            90, 80, 70, 255,
            120, 110, 100, 0,
            88, 88
        };

        var pixels = PortableImageColorAnalyzer.ReadBgra32Pixels(bgra, width: 2, height: 2, stride: 10);

        Assert.Equal(
            new[]
            {
                new PortableRgbColor(10, 20, 30),
                new PortableRgbColor(40, 50, 60),
                new PortableRgbColor(70, 80, 90),
                new PortableRgbColor(100, 110, 120)
            },
            pixels);
    }

    [Fact]
    public void ReadBgra32Pixels_samples_large_buffers_to_requested_max_dimension()
    {
        var bgra = new byte[4 * 4 * 4];
        for (var i = 0; i < 16; i++)
        {
            var offset = i * 4;
            bgra[offset] = 0;
            bgra[offset + 1] = 0;
            bgra[offset + 2] = (byte)i;
            bgra[offset + 3] = 255;
        }

        var pixels = PortableImageColorAnalyzer.ReadBgra32Pixels(bgra, width: 4, height: 4, stride: 16, maxDimension: 2);

        Assert.Equal(
            new[]
            {
                new PortableRgbColor(0, 0, 0),
                new PortableRgbColor(2, 0, 0),
                new PortableRgbColor(8, 0, 0),
                new PortableRgbColor(10, 0, 0)
            },
            pixels);
    }

    [Fact]
    public void ReadBgra32Pixels_rejects_incomplete_buffers()
    {
        Assert.Throws<ArgumentException>(() =>
            PortableImageColorAnalyzer.ReadBgra32Pixels(new byte[3], width: 1, height: 1, stride: 4));
    }

    [Fact]
    public void GenerateThemeColors_selects_farthest_palette_color_and_dark_text_from_image()
    {
        var pixels = Repeat(new PortableRgbColor(30, 30, 30), 20)
            .Concat(Repeat(new PortableRgbColor(220, 40, 40), 8))
            .Concat(Repeat(new PortableRgbColor(40, 40, 220), 6));

        var colors = PortableImageColorAnalyzer.GenerateThemeColors(
            new PortableRgbColor(210, 30, 30),
            pixels);

        Assert.Equal(new PortableRgbColor(210, 30, 30), colors.Primary);
        Assert.Equal(new PortableRgbColor(30, 30, 210), colors.Secondary);
        Assert.True(colors.IsDarkTheme);
        Assert.Equal(new PortableRgbColor(255, 255, 255), colors.TextColor);
    }

    [Fact]
    public void GenerateThemeColors_uses_primary_as_secondary_when_palette_has_no_other_color()
    {
        var colors = PortableImageColorAnalyzer.GenerateThemeColors(
            new PortableRgbColor(180, 180, 180),
            Repeat(new PortableRgbColor(180, 180, 180), 4));

        Assert.Equal(colors.Primary, colors.Secondary);
        Assert.False(colors.IsDarkTheme);
        Assert.Equal(new PortableRgbColor(33, 37, 41), colors.TextColor);
    }

    [Fact]
    public void CreateThemeSnapshot_maps_image_theme_to_portable_designer_snapshot()
    {
        var colors = new PortableImageThemeColors
        {
            Primary = new PortableRgbColor(60, 90, 180),
            Secondary = new PortableRgbColor(20, 160, 200),
            IsDarkTheme = false,
            TextColor = new PortableRgbColor(33, 37, 41)
        };

        var snapshot = PortableImageColorAnalyzer.CreateThemeSnapshot("Image Theme", colors);
        var xaml = PortableThemeDesigner.GenerateThemeXaml(snapshot);

        Assert.Equal("Image Theme", snapshot.ThemeName);
        Assert.Equal("#F8F9FA", snapshot.TopBarBackground);
        Assert.Equal("#FFFFFF", snapshot.CenterWorkspaceBackground);
        Assert.Equal("#212529", snapshot.CenterWorkspaceText);
        Assert.Equal("#3C5AB4", snapshot.PrimaryButtonColor);
        Assert.Equal("#14A0C8", snapshot.LeftBarIconColor);
        Assert.Contains("<SolidColorBrush x:Key=\"PrimaryColor\" Color=\"#3C5AB4\"/>", xaml);
        Assert.Contains("<SolidColorBrush x:Key=\"InfoColor\" Color=\"#3C5AB4\"/>", xaml);
    }

    private static IEnumerable<PortableRgbColor> Repeat(PortableRgbColor color, int count)
    {
        return Enumerable.Repeat(color, count);
    }
}
