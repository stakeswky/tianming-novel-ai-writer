using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableScenePresetServiceTests
{
    [Fact]
    public void GetAllPresets_returns_original_scene_order_and_names()
    {
        var service = new PortableScenePresetService();

        var presets = service.GetAllPresets();

        Assert.Equal(
            [PortableUsageScene.Coding, PortableUsageScene.Reading, PortableUsageScene.Presentation, PortableUsageScene.Terminal, PortableUsageScene.Documentation],
            presets.Select(preset => preset.Scene));
        Assert.Equal(["编码开发", "文档阅读", "屏幕演示", "终端控制台", "技术文档"], presets.Select(preset => preset.Name));
    }

    [Fact]
    public void GetPreset_returns_original_coding_settings_and_guidance()
    {
        var service = new PortableScenePresetService();

        var preset = service.GetPreset(PortableUsageScene.Coding);

        Assert.NotNull(preset);
        Assert.Equal("💻", preset.Icon);
        Assert.Equal("Consolas", preset.Settings.FontFamily);
        Assert.Equal(12, preset.Settings.FontSize);
        Assert.Equal(1.5, preset.Settings.LineHeight);
        Assert.True(preset.Settings.EnableLigatures);
        Assert.True(preset.Settings.VisualizeWhitespace);
        Assert.Equal("→", preset.Settings.TabSymbol);
        Assert.Equal("·", preset.Settings.SpaceSymbol);
        Assert.Contains("Fira Code", preset.RecommendedFonts);
        Assert.Contains("建议启用连字以改善运算符显示", preset.Tips);
    }

    [Theory]
    [InlineData(PortableUsageScene.Presentation, 18, "SemiBold", 2.0, false)]
    [InlineData(PortableUsageScene.Terminal, 11, "Normal", 1.3, false)]
    [InlineData(PortableUsageScene.Documentation, 12, "Normal", 1.6, true)]
    public void GetPreset_preserves_original_scene_font_settings(
        PortableUsageScene scene,
        double expectedFontSize,
        string expectedWeight,
        double expectedLineHeight,
        bool expectedLigatures)
    {
        var service = new PortableScenePresetService();

        var preset = service.GetPreset(scene);

        Assert.NotNull(preset);
        Assert.Equal(expectedFontSize, preset.Settings.FontSize);
        Assert.Equal(expectedWeight, preset.Settings.FontWeight);
        Assert.Equal(expectedLineHeight, preset.Settings.LineHeight);
        Assert.Equal(expectedLigatures, preset.Settings.EnableLigatures);
    }

    [Fact]
    public void GetAllPresets_returns_copy_so_callers_cannot_remove_internal_presets()
    {
        var service = new PortableScenePresetService();

        var first = service.GetAllPresets();
        first.Clear();
        var second = service.GetAllPresets();

        Assert.Equal(5, second.Count);
    }
}
