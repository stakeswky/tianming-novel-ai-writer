using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSoundSchemeTests
{
    [Fact]
    public void SoundScheme_default_settings_match_original_events()
    {
        var settings = PortableSoundSchemeData.CreateDefault();

        Assert.Equal("default", settings.ActiveSchemeId);
        Assert.Empty(settings.CustomSoundFiles);
        Assert.Equal("默认提示音", settings.EventSoundMappings["通知到达"]);
        Assert.Equal("警告音", settings.EventSoundMappings["警告"]);
        Assert.Equal("错误音", settings.EventSoundMappings["错误"]);
        Assert.Equal("成功音", settings.EventSoundMappings["成功"]);
        Assert.Equal("信息音", settings.EventSoundMappings["信息提示"]);
    }

    [Fact]
    public async Task SoundScheme_store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "Sound", "SoundScheme", "settings.json");
        var store = new FileSoundSchemeSettingsStore(path);
        var settings = PortableSoundSchemeData.CreateDefault();
        settings.ActiveSchemeId = "custom";
        settings.EventSoundMappings["成功"] = "ding";
        settings.CustomSoundFiles.Add("ding.wav");

        await store.SaveAsync(settings);
        var reloaded = await new FileSoundSchemeSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("custom", reloaded.ActiveSchemeId);
        Assert.Equal("ding", reloaded.EventSoundMappings["成功"]);
        Assert.Equal(["ding.wav"], reloaded.CustomSoundFiles);
    }

    [Fact]
    public async Task SoundScheme_store_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileSoundSchemeSettingsStore(path);

        Assert.Equal("信息音", (await store.LoadAsync()).EventSoundMappings["信息提示"]);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.Equal("默认提示音", (await store.LoadAsync()).EventSoundMappings["通知到达"]);
    }

    [Theory]
    [InlineData("silent", "无", "无", "无")]
    [InlineData("minimal", "默认提示音", "警告音", "默认提示音")]
    [InlineData("rich", "信息音", "错误音", "成功音")]
    [InlineData("default", "信息音", "错误音", "成功音")]
    public void SoundScheme_controller_applies_builtin_schemes(
        string schemeId,
        string expectedInfo,
        string expectedError,
        string expectedSuccess)
    {
        var controller = new PortableSoundSchemeController(PortableSoundSchemeData.CreateDefault());

        Assert.True(controller.SelectScheme(schemeId));

        Assert.Equal(schemeId, controller.Settings.ActiveSchemeId);
        Assert.Equal(expectedInfo, controller.Settings.EventSoundMappings["信息提示"]);
        Assert.Equal(expectedError, controller.Settings.EventSoundMappings["错误"]);
        Assert.Equal(expectedSuccess, controller.Settings.EventSoundMappings["成功"]);
    }

    [Fact]
    public void SoundScheme_controller_rejects_unknown_scheme_without_changing_current_settings()
    {
        var controller = new PortableSoundSchemeController(PortableSoundSchemeData.CreateDefault());

        Assert.False(controller.SelectScheme("missing"));

        Assert.Equal("default", controller.Settings.ActiveSchemeId);
        Assert.Equal("警告音", controller.Settings.EventSoundMappings["警告"]);
    }
}
