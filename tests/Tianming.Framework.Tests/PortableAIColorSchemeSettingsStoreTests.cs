using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAIColorSchemeSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_default_user_config_when_file_is_missing()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIColorSchemeSettingsStore(Path.Combine(workspace.Path, "ai_color_scheme_data.json"));

        var config = await store.LoadUserConfigAsync();

        Assert.Equal("互补色", config.LastColorHarmony);
        Assert.Equal("浅色主题", config.LastThemeType);
        Assert.Equal("无", config.LastEmotion);
        Assert.Equal("通用", config.LastScene);
        Assert.Equal(string.Empty, config.LastKeywords);
    }

    [Fact]
    public async Task SaveUserConfigAsync_persists_config_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "ai_color_scheme_data.json");
        var store = new FileAIColorSchemeSettingsStore(path);

        await store.SaveUserConfigAsync(new PortableAIColorSchemeUserConfig
        {
            LastColorHarmony = "三角色",
            LastThemeType = "暗色主题",
            LastEmotion = "神秘",
            LastScene = "科技感",
            LastKeywords = "星港"
        });
        var reloaded = await new FileAIColorSchemeSettingsStore(path).LoadUserConfigAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("三角色", reloaded.LastColorHarmony);
        Assert.Equal("暗色主题", reloaded.LastThemeType);
        Assert.Equal("神秘", reloaded.LastEmotion);
        Assert.Equal("科技感", reloaded.LastScene);
        Assert.Equal("星港", reloaded.LastKeywords);
    }

    [Fact]
    public async Task UpdateUserConfigAsync_overwrites_existing_values()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIColorSchemeSettingsStore(Path.Combine(workspace.Path, "ai_color_scheme_data.json"));
        await store.SaveUserConfigAsync(new PortableAIColorSchemeUserConfig
        {
            LastKeywords = "old",
            LastScene = "通用"
        });

        await store.UpdateUserConfigAsync(new PortableAIColorSchemeUserConfig
        {
            LastKeywords = "new",
            LastScene = "写作创作",
            LastColorHarmony = "类似色",
            LastThemeType = "浅色主题",
            LastEmotion = "平静"
        });
        var config = await store.LoadUserConfigAsync();

        Assert.Equal("new", config.LastKeywords);
        Assert.Equal("写作创作", config.LastScene);
        Assert.Equal("类似色", config.LastColorHarmony);
    }

    [Fact]
    public async Task LoadUserConfigAsync_recovers_from_invalid_json_or_missing_config()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "ai_color_scheme_data.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var invalidStore = new FileAIColorSchemeSettingsStore(path);

        Assert.Equal("互补色", (await invalidStore.LoadUserConfigAsync()).LastColorHarmony);

        await File.WriteAllTextAsync(path, "{\"GenerationHistory\":[]}");
        var missingConfigStore = new FileAIColorSchemeSettingsStore(path);

        Assert.Equal("浅色主题", (await missingConfigStore.LoadUserConfigAsync()).LastThemeType);
    }

    [Fact]
    public async Task LoadUserConfigAsync_normalizes_invalid_option_values_but_keeps_keywords()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "ai_color_scheme_data.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "UserConfig": {
                "LastColorHarmony": "不存在",
                "LastThemeType": "坏主题",
                "LastEmotion": "坏情绪",
                "LastScene": "坏场景",
                "LastKeywords": "  保留关键词  "
              }
            }
            """);
        var store = new FileAIColorSchemeSettingsStore(path);

        var config = await store.LoadUserConfigAsync();

        Assert.Equal("互补色", config.LastColorHarmony);
        Assert.Equal("浅色主题", config.LastThemeType);
        Assert.Equal("无", config.LastEmotion);
        Assert.Equal("通用", config.LastScene);
        Assert.Equal("  保留关键词  ", config.LastKeywords);
    }
}
