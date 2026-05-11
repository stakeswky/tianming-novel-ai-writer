using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontResourceMapperTests
{
    [Fact]
    public void CreateUIResources_maps_original_global_font_resource_keys_and_scaled_sizes()
    {
        var settings = new PortableFontSettings
        {
            FontFamily = "PingFang SC",
            FontSize = 14,
            FontWeight = "SemiBold",
            LineHeight = 1.5,
            LetterSpacing = 0.2,
            TextRendering = PortableTextRenderingMode.Grayscale,
            TextFormatting = PortableTextFormattingMode.Display,
            TextHinting = PortableTextHintingMode.Fixed
        };

        var resources = PortableFontResourceMapper.CreateUIResources(settings);

        Assert.Equal("PingFang SC", resources["GlobalFontFamily"]);
        Assert.Equal(14d, resources["GlobalFontSize"]);
        Assert.Equal("SemiBold", resources["GlobalFontWeight"]);
        Assert.Equal(1.5d, resources["GlobalLineHeight"]);
        Assert.Equal(0.2d, resources["GlobalLetterSpacing"]);
        Assert.Equal(32.06d, resources["FontSizeXXL"]);
        Assert.Equal(23.94d, resources["FontSizeXL"]);
        Assert.Equal(18.06d, resources["FontSizeLarge"]);
        Assert.Equal(15.96d, resources["FontSizeMedium"]);
        Assert.Equal(14d, resources["FontSizeNormal"]);
        Assert.Equal(13.02d, resources["FontSizeSmall"]);
        Assert.Equal(12.04d, resources["FontSizeXS"]);
        Assert.Equal(11.06d, resources["FontSizeTiny"]);
        Assert.Equal("Grayscale", resources["GlobalTextRenderingMode"]);
        Assert.Equal("Display", resources["GlobalTextFormattingMode"]);
        Assert.Equal("Fixed", resources["GlobalTextHintingMode"]);
    }

    [Fact]
    public void CreateEditorResources_maps_original_editor_font_resource_keys_and_scaled_sizes()
    {
        var settings = new PortableFontSettings
        {
            FontFamily = "Menlo",
            FontSize = 12,
            FontWeight = "Medium",
            LineHeight = 1.7,
            LetterSpacing = 0.5
        };

        var resources = PortableFontResourceMapper.CreateEditorResources(settings);

        Assert.Equal("Menlo", resources["EditorFontFamily"]);
        Assert.Equal(12d, resources["EditorFontSize"]);
        Assert.Equal("Medium", resources["EditorFontWeight"]);
        Assert.Equal(1.7d, resources["EditorLineHeight"]);
        Assert.Equal(0.5d, resources["EditorLetterSpacing"]);
        Assert.Equal(13.8d, resources["EditorFontSizeLarge"]);
        Assert.Equal(10.2d, resources["EditorFontSizeSmall"]);
    }

    [Fact]
    public void CreateUIResources_uses_original_fallbacks_for_invalid_or_empty_values()
    {
        var settings = new PortableFontSettings
        {
            FontFamily = "   ",
            FontSize = -1,
            FontWeight = "TooHeavy",
            LineHeight = 0,
            LetterSpacing = -5
        };

        var resources = PortableFontResourceMapper.CreateUIResources(settings);

        Assert.Equal("Microsoft YaHei UI", resources["GlobalFontFamily"]);
        Assert.Equal(14d, resources["GlobalFontSize"]);
        Assert.Equal("Normal", resources["GlobalFontWeight"]);
        Assert.Equal(1.5d, resources["GlobalLineHeight"]);
        Assert.Equal(0d, resources["GlobalLetterSpacing"]);
        Assert.Equal("Auto", resources["GlobalTextRenderingMode"]);
        Assert.Equal("Ideal", resources["GlobalTextFormattingMode"]);
        Assert.Equal("Auto", resources["GlobalTextHintingMode"]);
    }
}
