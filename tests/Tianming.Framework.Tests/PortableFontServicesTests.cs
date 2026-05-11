using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontServicesTests
{
    [Theory]
    [InlineData("JetBrains Mono", PortableFontCategory.Monospace)]
    [InlineData("PingFang SC", PortableFontCategory.CJK)]
    [InlineData("Georgia", PortableFontCategory.Serif)]
    [InlineData("Helvetica Neue", PortableFontCategory.SansSerif)]
    [InlineData("", PortableFontCategory.System)]
    public void ClassifyFont_matches_original_category_rules(string fontName, PortableFontCategory expected)
    {
        Assert.Equal(expected, PortableFontCatalog.ClassifyFont(fontName));
    }

    [Fact]
    public void GetThemeRecommendations_prefers_available_fonts_in_theme_order_and_scores_top_three()
    {
        var recommendations = PortableFontCatalog.GetThemeRecommendations(
            PortableThemeType.Dark,
            ["Arial", "PingFang SC", "SF Pro Display", "Roboto", "Courier New"]);

        Assert.Equal(3, recommendations.Count);
        Assert.Equal("SF Pro Display", recommendations[0].FontName);
        Assert.Equal("PingFang SC", recommendations[1].FontName);
        Assert.Equal("Roboto", recommendations[2].FontName);
        Assert.All(recommendations, item => Assert.InRange(item.Score, 0, 100));
    }

    [Fact]
    public void GenerateTags_marks_monospace_and_category_labels()
    {
        var monoTags = PortableFontCatalog.GenerateTags("Fira Code", PortableFontCategory.Monospace);
        var cjkTags = PortableFontCatalog.GenerateTags("PingFang SC", PortableFontCategory.CJK);

        Assert.Contains("等宽", monoTags);
        Assert.Contains("CJK", cjkTags);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_adds_and_removes_font()
    {
        using var workspace = new TempDirectory();
        var store = new FileFontUsageStore(Path.Combine(workspace.Path, "fonts.json"));

        var added = await store.ToggleFavoriteAsync("PingFang SC");
        var removed = await store.ToggleFavoriteAsync("PingFang SC");
        var data = await store.LoadAsync();

        Assert.True(added);
        Assert.False(removed);
        Assert.Empty(data.FavoriteFonts);
    }

    [Fact]
    public async Task RecordUsageAsync_updates_count_moves_recent_and_trims_to_twenty()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 11, 12, 0, 0);
        var store = new FileFontUsageStore(Path.Combine(workspace.Path, "fonts.json"), () => now);

        for (var i = 0; i < 22; i++)
        {
            now = new DateTime(2026, 5, 11, 12, i, 0);
            await store.RecordUsageAsync($"Font-{i:00}");
        }

        now = new DateTime(2026, 5, 11, 13, 0, 0);
        await store.RecordUsageAsync("Font-05");
        var data = await store.LoadAsync();

        Assert.Equal(20, data.RecentFonts.Count);
        Assert.Equal("Font-05", data.RecentFonts[0].FontName);
        Assert.Equal(2, data.RecentFonts[0].UsageCount);
        Assert.DoesNotContain(data.RecentFonts.Skip(1), item => item.FontName == "Font-05");
    }

    [Fact]
    public async Task LoadAsync_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "fonts.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var store = new FileFontUsageStore(path);

        var data = await store.LoadAsync();

        Assert.Empty(data.FavoriteFonts);
        Assert.Empty(data.RecentFonts);
    }
}
