using TM.Framework.Preferences;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableDisplayPreferencesTests
{
    [Fact]
    public void Default_settings_match_original_display_defaults()
    {
        var settings = PortableDisplaySettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(settings.ShowFunctionBar);
        Assert.Equal(PortableListDensity.Standard, settings.ListDensity);
        Assert.Equal(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc), settings.LastModified);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Preferences", "Display", "display_settings.json");
        var now = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc);
        var store = new FileDisplaySettingsStore(path, () => now);
        var settings = PortableDisplaySettings.CreateDefault(now);
        settings.ShowFunctionBar = false;
        settings.ListDensity = PortableListDensity.Compact;

        await store.SaveAsync(settings);
        var reloaded = await new FileDisplaySettingsStore(path, () => now).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.False(reloaded.ShowFunctionBar);
        Assert.Equal(PortableListDensity.Compact, reloaded.ListDensity);
        Assert.Equal(now, reloaded.LastModified);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileDisplaySettingsStore(path, () => now).LoadAsync();

        Assert.True(recovered.ShowFunctionBar);
        Assert.Equal(PortableListDensity.Standard, recovered.ListDensity);
    }

    [Fact]
    public void Controller_updates_display_options_and_clamps_ui_scale()
    {
        var settings = PortableDisplaySettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        var controller = new PortableDisplayController(settings);

        controller.UpdateShowFunctionBar(false);
        controller.UpdateListDensity(PortableListDensity.Comfortable);
        controller.UpdateUiScale(2.4);

        Assert.False(settings.ShowFunctionBar);
        Assert.Equal(PortableListDensity.Comfortable, settings.ListDensity);
        Assert.Equal(200, controller.UiScalePercent);

        controller.UpdateUiScale(0.3);

        Assert.Equal(80, controller.UiScalePercent);

        controller.ResetToDefaults();

        Assert.True(settings.ShowFunctionBar);
        Assert.Equal(PortableListDensity.Standard, settings.ListDensity);
        Assert.Equal(100, controller.UiScalePercent);
    }

    [Fact]
    public void BuildApplyPlan_exposes_binding_friendly_display_state()
    {
        var settings = PortableDisplaySettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        settings.ListDensity = PortableListDensity.Compact;
        settings.ShowFunctionBar = false;

        var plan = PortableDisplayController.BuildApplyPlan(settings, uiScale: 1.256);

        Assert.False(plan.ShowFunctionBar);
        Assert.Equal(126, plan.UiScalePercent);
        Assert.Equal("126%", plan.UiScalePercentText);
        Assert.Equal("紧凑 - 显示更多内容", plan.DensityDescription);
    }
}
