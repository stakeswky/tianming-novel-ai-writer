using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableMonospaceFontDetectorTests
{
    [Theory]
    [InlineData("Menlo")]
    [InlineData("SF Mono")]
    [InlineData("Hack")]
    [InlineData("Input Mono")]
    [InlineData("Meslo LG M")]
    public void IsMonospace_returns_true_for_original_known_monospace_fonts(string fontFamilyName)
    {
        var detector = new PortableMonospaceFontDetector();

        Assert.True(detector.IsMonospace(fontFamilyName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Helvetica Neue")]
    public void IsMonospace_returns_false_for_blank_or_unknown_fonts_without_measurement(string fontFamilyName)
    {
        var detector = new PortableMonospaceFontDetector();

        Assert.False(detector.IsMonospace(fontFamilyName));
    }

    [Fact]
    public void IsMonospace_uses_injected_width_measurement_for_unknown_fonts()
    {
        var detector = new PortableMonospaceFontDetector((fontName, fontSize) =>
            fontName == "Measured Mono"
                ? PortableMonospaceFontDetector.TestChars.ToDictionary(ch => ch, _ => 12d)
                : new Dictionary<char, double>
                {
                    ['i'] = 4,
                    ['l'] = 4,
                    ['I'] = 5,
                    ['1'] = 7,
                    ['m'] = 14,
                    ['W'] = 16,
                    ['M'] = 15,
                    ['0'] = 9,
                    ['O'] = 10
                });

        Assert.True(detector.IsMonospace("Measured Mono"));
        Assert.False(detector.IsMonospace("Measured Sans"));
    }

    [Fact]
    public void IsMonospace_caches_measurement_until_cache_is_cleared()
    {
        var calls = 0;
        var detector = new PortableMonospaceFontDetector((_, _) =>
        {
            calls++;
            return PortableMonospaceFontDetector.TestChars.ToDictionary(ch => ch, _ => 10d);
        });

        Assert.True(detector.IsMonospace("Custom Unknown"));
        Assert.True(detector.IsMonospace("Custom Unknown"));
        detector.ClearCache();
        Assert.True(detector.IsMonospace("Custom Unknown"));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void GetCharacterWidths_returns_copy_of_injected_widths()
    {
        var detector = new PortableMonospaceFontDetector((_, _) =>
            new Dictionary<char, double> { ['i'] = 8, ['m'] = 8 });

        var first = detector.GetCharacterWidths("Any Font", 13);
        first['i'] = 99;
        var second = detector.GetCharacterWidths("Any Font", 13);

        Assert.Equal(8, second['i']);
        Assert.Equal(8, second['m']);
    }
}
