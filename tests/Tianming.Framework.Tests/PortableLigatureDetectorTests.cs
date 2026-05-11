using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLigatureDetectorTests
{
    [Theory]
    [InlineData("Fira Code")]
    [InlineData("JetBrains Mono NL")]
    [InlineData("Cascadia Code")]
    [InlineData("Victor Mono")]
    public void SupportsLigatures_returns_true_for_original_known_ligature_fonts(string fontFamilyName)
    {
        var detector = new PortableLigatureDetector();

        Assert.True(detector.SupportsLigatures(fontFamilyName));
        Assert.Contains("->", detector.GetSupportedLigatures(fontFamilyName));
        Assert.Contains("===", detector.GetSupportedLigatures(fontFamilyName));
    }

    [Fact]
    public void GetSupportedLigatures_returns_empty_for_unknown_or_blank_font_names()
    {
        var detector = new PortableLigatureDetector();

        Assert.False(detector.SupportsLigatures("Helvetica Neue"));
        Assert.Empty(detector.GetSupportedLigatures("Helvetica Neue"));
        Assert.Empty(detector.GetSupportedLigatures("   "));
    }

    [Fact]
    public void GetSupportedLigatures_returns_copy_so_cache_cannot_be_mutated_by_callers()
    {
        var detector = new PortableLigatureDetector();

        var first = detector.GetSupportedLigatures("Fira Code");
        first.Clear();
        var second = detector.GetSupportedLigatures("Fira Code");

        Assert.NotEmpty(second);
        Assert.Contains("->", second);
    }

    [Fact]
    public void GenerateLigaturePreviewText_groups_arrows_comparisons_logical_and_other_tokens()
    {
        var preview = PortableLigatureDetector.GenerateLigaturePreviewText(
            ["->", "=>", "!=", "&&", "|||"]);

        Assert.Contains("// 编程连字预览", preview);
        Assert.Contains("箭头: ->  =>", preview);
        Assert.Contains("比较: =>  !=", preview);
        Assert.Contains("逻辑: &&  |||", preview);
    }

    [Fact]
    public void GenerateLigaturePreviewText_returns_original_empty_message()
    {
        var preview = PortableLigatureDetector.GenerateLigaturePreviewText([]);

        Assert.Equal("此字体不支持编程连字", preview);
    }
}
