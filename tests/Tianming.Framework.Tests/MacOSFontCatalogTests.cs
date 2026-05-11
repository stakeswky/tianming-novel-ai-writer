using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSFontCatalogTests
{
    [Fact]
    public void GetInstalledFontFamilies_reads_supported_font_files_from_configured_directories()
    {
        using var workspace = new TempDirectory();
        var systemFonts = Directory.CreateDirectory(Path.Combine(workspace.Path, "SystemFonts")).FullName;
        var userFonts = Directory.CreateDirectory(Path.Combine(workspace.Path, "UserFonts")).FullName;
        File.WriteAllText(Path.Combine(systemFonts, "PingFang-SC-Regular.ttc"), "font");
        File.WriteAllText(Path.Combine(systemFonts, "SF-Pro-Display.otf"), "font");
        File.WriteAllText(Path.Combine(userFonts, "JetBrains Mono.ttf"), "font");
        File.WriteAllText(Path.Combine(userFonts, "notes.txt"), "not a font");
        var catalog = new MacOSFontCatalog([systemFonts, userFonts]);

        var fonts = catalog.GetInstalledFontFamilies();

        Assert.Equal(["JetBrains Mono", "PingFang SC", "SF Pro Display"], fonts);
    }

    [Fact]
    public void GetInstalledFontFamilies_deduplicates_names_case_insensitively()
    {
        using var workspace = new TempDirectory();
        File.WriteAllText(Path.Combine(workspace.Path, "Menlo-Regular.ttf"), "font");
        File.WriteAllText(Path.Combine(workspace.Path, "menlo.otf"), "font");
        var catalog = new MacOSFontCatalog([workspace.Path]);

        var fonts = catalog.GetInstalledFontFamilies();

        Assert.Equal(["Menlo"], fonts);
    }

    [Fact]
    public void GetInstalledFontFamilies_ignores_missing_directories()
    {
        using var workspace = new TempDirectory();
        var catalog = new MacOSFontCatalog([Path.Combine(workspace.Path, "missing")]);

        var fonts = catalog.GetInstalledFontFamilies();

        Assert.Empty(fonts);
    }

    [Theory]
    [InlineData("PingFang-SC-Regular.ttc", "PingFang SC")]
    [InlineData("SF-Pro-Display-Bold.otf", "SF Pro Display")]
    [InlineData("JetBrains Mono Italic.ttf", "JetBrains Mono")]
    [InlineData("NotoSansCJK-Regular.ttc", "NotoSansCJK")]
    public void NormalizeFontFamilyName_removes_common_style_suffixes(string fileName, string expected)
    {
        Assert.Equal(expected, MacOSFontCatalog.NormalizeFontFamilyName(fileName));
    }
}
