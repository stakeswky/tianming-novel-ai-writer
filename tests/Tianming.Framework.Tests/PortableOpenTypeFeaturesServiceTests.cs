using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOpenTypeFeaturesServiceTests
{
    [Fact]
    public void GetSupportedFeatures_returns_original_known_features_for_fira_code()
    {
        var service = new PortableOpenTypeFeaturesService();

        var features = service.GetSupportedFeatures("Fira Code Retina");

        Assert.Equal(
            ["liga", "calt", "ss01", "ss02", "ss03", "ss04", "ss05", "ss06", "ss07", "ss08", "ss09", "ss10", "zero", "onum"],
            features.Select(feature => feature.Tag));
        Assert.All(features, feature => Assert.False(string.IsNullOrWhiteSpace(feature.Name)));
        Assert.True(features.Single(feature => feature.Tag == "liga").IsEnabled);
        Assert.True(features.Single(feature => feature.Tag == "calt").IsEnabled);
        Assert.False(features.Single(feature => feature.Tag == "ss01").IsEnabled);
    }

    [Fact]
    public void GetSupportedFeatures_uses_original_ligature_fallback_for_unknown_fonts()
    {
        var service = new PortableOpenTypeFeaturesService();

        var features = service.GetSupportedFeatures("Unknown Mono");

        Assert.Equal(["liga", "calt"], features.Select(feature => feature.Tag));
        Assert.Equal("标准连字", features[0].Name);
        Assert.Equal("-> => >= != == ===", features[0].PreviewText);
    }

    [Fact]
    public void GetSupportedFeatures_returns_empty_for_blank_fonts()
    {
        var service = new PortableOpenTypeFeaturesService();

        Assert.Empty(service.GetSupportedFeatures(""));
        Assert.Empty(service.GetSupportedFeatures("   "));
    }

    [Theory]
    [InlineData("JetBrains Mono", "CV05")]
    [InlineData("Cascadia Code", "ss20")]
    [InlineData("Consolas", "CALT")]
    public void SupportsFeature_is_case_insensitive_for_original_known_font_features(string fontName, string tag)
    {
        var service = new PortableOpenTypeFeaturesService();

        Assert.True(service.SupportsFeature(fontName, tag));
    }

    [Fact]
    public void GetFeatureInfo_returns_original_definition_or_unknown_fallback()
    {
        var service = new PortableOpenTypeFeaturesService();

        var known = service.GetFeatureInfo("zero");
        var unknown = service.GetFeatureInfo("zzzz");

        Assert.Equal("带斜线的零", known.Name);
        Assert.Equal("区分数字0和字母O", known.Description);
        Assert.Equal("0 O 00 10", known.Preview);
        Assert.Equal(("zzzz", "未知特性", ""), unknown);
    }
}
