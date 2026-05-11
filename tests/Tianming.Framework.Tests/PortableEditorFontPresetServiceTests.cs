using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableEditorFontPresetServiceTests
{
    [Fact]
    public void GetBuiltInPresets_returns_original_editor_font_preset_order()
    {
        var service = new PortableEditorFontPresetService([]);

        var presets = service.GetBuiltInPresets();

        Assert.Equal(
            [
                "Consolas（Windows经典）",
                "Fira Code（连字推荐）",
                "JetBrains Mono（专业）",
                "Cascadia Code（现代）",
                "Source Code Pro（Adobe）",
                "Inconsolata（紧凑）",
                "Monaco（macOS风格）",
                "Courier New（经典）",
                "Lucida Console（内置）"
            ],
            presets.Select(preset => preset.Name));
    }

    [Fact]
    public void GetBuiltInPresets_marks_installation_status_from_injected_font_catalog()
    {
        var service = new PortableEditorFontPresetService(["fira code", "MONACO"]);

        var presets = service.GetBuiltInPresets();

        Assert.True(presets.Single(preset => preset.Settings.FontFamily == "Fira Code").IsInstalled);
        Assert.True(presets.Single(preset => preset.Settings.FontFamily == "Monaco").IsInstalled);
        Assert.False(presets.Single(preset => preset.Settings.FontFamily == "Consolas").IsInstalled);
    }

    [Fact]
    public void IsFontInstalled_is_case_insensitive_and_rejects_blank_names()
    {
        var service = new PortableEditorFontPresetService(["JetBrains Mono"]);

        Assert.True(service.IsFontInstalled("jetbrains mono"));
        Assert.False(service.IsFontInstalled("   "));
        Assert.False(service.IsFontInstalled("Unknown Font"));
    }

    [Fact]
    public void BuiltInPresets_preserve_original_settings_urls_and_features()
    {
        var service = new PortableEditorFontPresetService(["Cascadia Code"]);

        var presets = service.GetBuiltInPresets();
        var cascadia = presets.Single(preset => preset.Settings.FontFamily == "Cascadia Code");
        var monaco = presets.Single(preset => preset.Settings.FontFamily == "Monaco");

        Assert.Equal(11, cascadia.Settings.FontSize);
        Assert.Equal(1.5, cascadia.Settings.LineHeight);
        Assert.True(cascadia.Settings.EnableLigatures);
        Assert.Equal("https://github.com/microsoft/cascadia-code", cascadia.DownloadUrl);
        Assert.Equal(["等宽", "连字", "现代", "OpenType"], cascadia.Features);

        Assert.Equal("Monaco（macOS风格）", monaco.Name);
        Assert.Equal("https://en.wikipedia.org/wiki/Monaco_(typeface)", monaco.DownloadUrl);
        Assert.Equal(["等宽", "macOS"], monaco.Features);
    }
}
