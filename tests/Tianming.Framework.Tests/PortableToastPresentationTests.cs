using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableToastPresentationTests
{
    [Fact]
    public void Build_uses_original_static_entry_defaults_and_hides_blank_message()
    {
        var presenter = PortableToastPresenter.CreateDefault();

        var model = presenter.Build(new PortableToastRequest
        {
            Title = "测试通知",
            Message = "   ",
            Type = PortableNotificationType.Info
        });

        Assert.Equal("测试通知", model.Title);
        Assert.Equal(string.Empty, model.Message);
        Assert.False(model.HasMessage);
        Assert.Equal(3000, model.DurationMilliseconds);
        Assert.True(model.AutoDismiss);
        Assert.Equal("ℹ️", model.Icon);
        Assert.Equal("#9C27B0", model.BackgroundColor);
        Assert.Equal(0.95, model.BackgroundOpacity, 3);
        Assert.Equal(300, model.Width);
        Assert.Equal(80, model.MinHeight);
    }

    [Fact]
    public void Build_prefers_enabled_type_configuration_and_falls_back_when_disabled()
    {
        var presenter = new PortableToastPresenter(
            PortableToastStyleData.CreateDefault(),
            new[]
            {
                new PortableToastTypeStyle
                {
                    Id = "warning",
                    Icon = "!",
                    Color = "#ABCDEF",
                    IsEnabled = true
                },
                new PortableToastTypeStyle
                {
                    Id = "error",
                    Icon = "x",
                    Color = "#111111",
                    IsEnabled = false
                }
            });

        var warning = presenter.Build(new PortableToastRequest
        {
            Title = "警告",
            Type = PortableNotificationType.Warning,
            DurationMilliseconds = 0
        });
        var error = presenter.Build(new PortableToastRequest
        {
            Title = "错误",
            Type = PortableNotificationType.Error
        });

        Assert.Equal("!", warning.Icon);
        Assert.Equal("#ABCDEF", warning.BackgroundColor);
        Assert.False(warning.AutoDismiss);
        Assert.Equal("❌", error.Icon);
        Assert.Equal("#EF4444", error.BackgroundColor);
    }

    [Fact]
    public void Placement_matches_original_bottom_right_stacking_formula()
    {
        var style = PortableToastStyleData.CreateDefault();
        var workArea = new PortableToastWorkArea(0, 0, 1440, 900);
        var size = new PortableToastSize(300, 80);

        var first = PortableToastPlacementCalculator.Calculate(
            style,
            workArea,
            size,
            previousToastHeights: []);
        var second = PortableToastPlacementCalculator.Calculate(
            style,
            workArea,
            size,
            previousToastHeights: [80]);

        Assert.Equal(1130, first.Left);
        Assert.Equal(810, first.Top);
        Assert.Equal(1130, second.Left);
        Assert.Equal(725, second.Top);
    }

    [Fact]
    public void Style_controller_applies_original_style_presets()
    {
        var settings = PortableToastStyleData.CreateDefault();
        var controller = new PortableToastStyleController(settings);

        controller.ApplyPreset("fancy");

        Assert.Equal(16, settings.CornerRadius);
        Assert.Equal(25, settings.ShadowIntensity);
        Assert.Equal(0, settings.BorderThickness);
        Assert.Equal(90, settings.BackgroundOpacity);
        Assert.Equal(PortableToastAnimationType.Bounce, settings.AnimationType);
        Assert.Equal(500, settings.AnimationDuration);

        controller.ApplyPreset("simple");

        Assert.Equal(4, settings.CornerRadius);
        Assert.Equal(5, settings.ShadowIntensity);
        Assert.Equal(1, settings.BorderThickness);
        Assert.Equal(100, settings.BackgroundOpacity);
        Assert.Equal(PortableToastAnimationType.FadeInOut, settings.AnimationType);
        Assert.Equal(200, settings.AnimationDuration);

        controller.ApplyPreset("standard");

        Assert.Equal(PortableToastStyleData.CreateDefault().CornerRadius, settings.CornerRadius);
        Assert.Equal(PortableToastStyleData.CreateDefault().AnimationDuration, settings.AnimationDuration);
    }

    [Fact]
    public async Task Style_store_round_trips_settings_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "SystemNotifications", "NotificationStyle", "notification_style.json");
        var store = new FileToastStyleSettingsStore(path);
        var settings = PortableToastStyleData.CreateDefault();
        settings.ScreenPosition = PortableToastScreenPosition.TopLeft;
        settings.NotificationWidth = 420;
        settings.BackgroundOpacity = 88;

        await store.SaveAsync(settings);
        var reloaded = await new FileToastStyleSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(PortableToastScreenPosition.TopLeft, reloaded.ScreenPosition);
        Assert.Equal(420, reloaded.NotificationWidth);
        Assert.Equal(88, reloaded.BackgroundOpacity);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileToastStyleSettingsStore(path).LoadAsync();

        Assert.Equal(PortableToastScreenPosition.BottomRight, recovered.ScreenPosition);
        Assert.Equal(300, recovered.NotificationWidth);
    }

    [Fact]
    public async Task Type_store_uses_original_default_types_and_round_trips_metadata()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_types.json");
        var store = new FileToastTypeSettingsStore(path);

        var defaults = await store.LoadAsync();

        Assert.Equal(6, defaults.Types.Count);
        var warning = defaults.Types.Single(type => type.Id == "warning");
        Assert.Equal("警告", warning.Name);
        Assert.Equal("⚠️", warning.Icon);
        Assert.Equal("#FF9800", warning.Color);
        Assert.Equal(PortableToastNotificationPriority.High, warning.Priority);
        Assert.Equal("状态", warning.GroupName);

        warning.Color = "#ABCDEF";
        warning.IsEnabled = false;
        await store.SaveAsync(defaults);
        var reloaded = await new FileToastTypeSettingsStore(path).LoadAsync();

        var updatedWarning = reloaded.Types.Single(type => type.Id == "warning");
        Assert.Equal("#ABCDEF", updatedWarning.Color);
        Assert.False(updatedWarning.IsEnabled);
        Assert.NotEqual(default, reloaded.LastModified);
    }

    [Fact]
    public async Task Type_store_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_types.json");
        await File.WriteAllTextAsync(path, "{ invalid json");

        var settings = await new FileToastTypeSettingsStore(path).LoadAsync();

        Assert.Equal(6, settings.Types.Count);
        Assert.Contains(settings.Types, type => type.Id == "success" && type.Icon == "✅");
    }
}
