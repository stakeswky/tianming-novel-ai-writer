using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeSelectionStoreTests
{
    [Fact]
    public async Task ToggleFavoriteAsync_adds_and_removes_favorite_theme()
    {
        using var workspace = new TempDirectory();
        var store = new FileThemeSelectionStore(Path.Combine(workspace.Path, "theme_selection.json"));

        var added = await store.ToggleFavoriteAsync("Dark");
        var removed = await store.ToggleFavoriteAsync("Dark");
        var data = await store.LoadAsync();

        Assert.True(added);
        Assert.False(removed);
        Assert.Empty(data.FavoriteIds);
    }

    [Fact]
    public async Task RecordRecentThemeAsync_moves_existing_theme_to_front_and_trims_to_twenty()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 11, 12, 0, 0);
        var store = new FileThemeSelectionStore(
            Path.Combine(workspace.Path, "theme_selection.json"),
            () => now);

        for (var i = 0; i < 22; i++)
        {
            await store.RecordRecentThemeAsync($"theme-{i}", $"主题 {i}");
        }

        await store.RecordRecentThemeAsync("theme-5", "主题 5 updated");
        var data = await store.LoadAsync();

        Assert.Equal(20, data.RecentThemes.Count);
        Assert.Equal("theme-5", data.RecentThemes[0].ThemeId);
        Assert.Equal("主题 5 updated", data.RecentThemes[0].ThemeName);
        Assert.DoesNotContain(data.RecentThemes.Skip(1), item => item.ThemeId == "theme-5");
    }

    [Fact]
    public async Task AddSearchHistoryAsync_deduplicates_and_trims_to_fifty()
    {
        using var workspace = new TempDirectory();
        var store = new FileThemeSelectionStore(Path.Combine(workspace.Path, "theme_selection.json"));

        for (var i = 0; i < 52; i++)
        {
            await store.AddSearchHistoryAsync($"keyword-{i}");
        }

        await store.AddSearchHistoryAsync("keyword-10");
        await store.AddSearchHistoryAsync("   ");
        var data = await store.LoadAsync();

        Assert.Equal(50, data.SearchHistory.Count);
        Assert.Equal("keyword-10", data.SearchHistory[0]);
        Assert.DoesNotContain(data.SearchHistory.Skip(1), item => item == "keyword-10");
    }

    [Fact]
    public async Task UpdatePreferencesAsync_persists_latest_filters()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "theme_selection.json");
        var store = new FileThemeSelectionStore(path);

        await store.UpdatePreferencesAsync(new PortableThemeSelectionPreferences
        {
            LastSearchText = "blue",
            LastSelectedCategory = "深色",
            LastSortMode = "按名称",
            ShowOnlyFavorites = true
        });

        var reloaded = await new FileThemeSelectionStore(path).LoadAsync();

        Assert.Equal("blue", reloaded.Preferences.LastSearchText);
        Assert.Equal("深色", reloaded.Preferences.LastSelectedCategory);
        Assert.Equal("按名称", reloaded.Preferences.LastSortMode);
        Assert.True(reloaded.Preferences.ShowOnlyFavorites);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "theme_selection.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var store = new FileThemeSelectionStore(path);

        var data = await store.LoadAsync();

        Assert.Empty(data.FavoriteIds);
        Assert.Empty(data.RecentThemes);
        Assert.Equal("全部", data.Preferences.LastSelectedCategory);
    }
}
