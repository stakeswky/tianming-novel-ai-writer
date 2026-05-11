using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontInfoServiceTests
{
    [Fact]
    public void GetFontInfo_returns_original_blank_font_placeholder()
    {
        var service = new PortableFontInfoService(_ => null);

        var info = service.GetFontInfo("   ");

        Assert.Equal("未选择字体", info.FontName);
        Assert.Equal("未知", info.Designer);
        Assert.Equal(400, info.MinWeight);
        Assert.Equal(400, info.MaxWeight);
        Assert.Empty(info.SupportedScripts);
    }

    [Fact]
    public void GetFontInfo_maps_probe_metadata_and_detects_supported_scripts()
    {
        var service = new PortableFontInfoService(_ => new PortableFontProbeResult
        {
            Designer = "Apple",
            Version = "1.2",
            License = "System",
            Copyright = "Copyright",
            SupportsItalic = true,
            CharacterMap = ['A', 'z', '\u0410', '\u4E2D', '\u0627', '©']
        });

        var info = service.GetFontInfo("PingFang SC");

        Assert.Equal("PingFang SC", info.FontName);
        Assert.Equal("Apple", info.Designer);
        Assert.Equal("1.2", info.Version);
        Assert.Equal("System", info.License);
        Assert.Equal("Copyright", info.Copyright);
        Assert.Equal(6, info.GlyphCount);
        Assert.Equal(400, info.MinWeight);
        Assert.Equal(700, info.MaxWeight);
        Assert.True(info.SupportsItalic);
        Assert.False(info.IsMonospace);
        Assert.True(info.SupportsLatin);
        Assert.True(info.SupportsCyrillic);
        Assert.True(info.SupportsCJK);
        Assert.True(info.SupportsArabic);
        Assert.True(info.SupportsSymbols);
        Assert.Equal(["拉丁", "西里尔", "中日韩", "阿拉伯", "符号"], info.SupportedScripts);
    }

    [Fact]
    public void GetFontInfo_uses_cache_until_clear_cache()
    {
        var calls = 0;
        var service = new PortableFontInfoService(_ =>
        {
            calls++;
            return new PortableFontProbeResult
            {
                Version = calls.ToString(),
                CharacterMap = ['A', 'z']
            };
        });

        Assert.Equal("1", service.GetFontInfo("Menlo").Version);
        Assert.Equal("1", service.GetFontInfo("Menlo").Version);
        service.ClearCache();
        Assert.Equal("2", service.GetFontInfo("Menlo").Version);
    }

    [Fact]
    public void GetFontInfo_returns_default_model_when_probe_fails()
    {
        var service = new PortableFontInfoService(_ => throw new InvalidOperationException("font unavailable"));

        var info = service.GetFontInfo("Broken Font");

        Assert.Equal("Broken Font", info.FontName);
        Assert.Equal("未知", info.Designer);
        Assert.Equal("未知", info.Version);
        Assert.Equal(0, info.GlyphCount);
        Assert.Empty(info.SupportedScripts);
    }

    [Fact]
    public void GetFontVariants_uses_probe_and_recovers_from_errors()
    {
        var service = new PortableFontInfoService(
            _ => null,
            fontName => fontName == "Menlo"
                ? ["Normal Normal Normal", "Bold Italic Normal"]
                : throw new InvalidOperationException("variant unavailable"));

        Assert.Equal(["Normal Normal Normal", "Bold Italic Normal"], service.GetFontVariants("Menlo"));
        Assert.Empty(service.GetFontVariants("Broken Font"));
    }
}
