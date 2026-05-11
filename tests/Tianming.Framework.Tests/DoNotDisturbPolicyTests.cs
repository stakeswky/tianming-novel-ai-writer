using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class DoNotDisturbPolicyTests
{
    [Fact]
    public void ShouldBlock_returns_false_when_disabled_or_urgent_is_allowed()
    {
        var policy = new DoNotDisturbPolicy(new DoNotDisturbSettingsData { IsEnabled = false });

        Assert.False(policy.ShouldBlock());

        policy = new DoNotDisturbPolicy(new DoNotDisturbSettingsData
        {
            IsEnabled = true,
            AllowUrgentNotifications = true
        });

        Assert.False(policy.ShouldBlock(isHighPriority: true));
        Assert.True(policy.ShouldBlock(isHighPriority: false, now: new DateTime(2026, 5, 10, 23, 0, 0)));
    }

    [Fact]
    public void ShouldBlock_respects_time_window_when_enabled()
    {
        var settings = new DoNotDisturbSettingsData
        {
            IsEnabled = true,
            StartTime = new TimeSpan(22, 0, 0),
            EndTime = new TimeSpan(8, 0, 0)
        };
        var policy = new DoNotDisturbPolicy(settings);

        Assert.True(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 23, 0, 0)));
        Assert.True(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 7, 30, 0)));
        Assert.False(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 12, 0, 0)));
    }

    [Fact]
    public void ShouldBlock_supports_same_day_time_window_and_exception_apps()
    {
        var settings = new DoNotDisturbSettingsData
        {
            IsEnabled = true,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            ExceptionApps = ["Tianming"]
        };
        var policy = new DoNotDisturbPolicy(settings);

        Assert.True(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 10, 0, 0)));
        Assert.False(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 18, 0, 0)));
        Assert.False(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 10, 0, 0), sourceApp: "tianming"));
    }

    [Fact]
    public void ShouldBlock_auto_enables_when_fullscreen_is_detected()
    {
        var policy = new DoNotDisturbPolicy(new DoNotDisturbSettingsData
        {
            IsEnabled = false,
            AutoEnableInFullscreen = true,
            AllowUrgentNotifications = true,
            ExceptionApps = ["Tianming"]
        });

        Assert.True(policy.ShouldBlock(now: new DateTime(2026, 5, 10, 12, 0, 0), isSystemFullscreen: true));
        Assert.False(policy.ShouldBlock(
            isHighPriority: true,
            now: new DateTime(2026, 5, 10, 12, 0, 0),
            isSystemFullscreen: true));
        Assert.False(policy.ShouldBlock(
            now: new DateTime(2026, 5, 10, 12, 0, 0),
            sourceApp: "tianming",
            isSystemFullscreen: true));
    }

    [Fact]
    public void MacOSFullScreenApplicationDetector_parses_frontmost_application_state()
    {
        var state = MacOSFullScreenApplicationDetector.ParseState("Safari|true");

        Assert.Equal("Safari", state.ApplicationName);
        Assert.True(state.IsFullScreen);

        state = MacOSFullScreenApplicationDetector.ParseState("Finder|false");

        Assert.Equal("Finder", state.ApplicationName);
        Assert.False(state.IsFullScreen);
    }

    [Fact]
    public async Task MacOSFullScreenApplicationDetector_uses_osascript_command_boundary()
    {
        MacOSFullScreenCommandRequest? captured = null;
        var detector = new MacOSFullScreenApplicationDetector((request, _) =>
        {
            captured = request;
            return Task.FromResult(new MacOSFullScreenCommandResult(0, "Keynote|true", string.Empty));
        });

        var state = await detector.DetectAsync();

        Assert.Equal("/usr/bin/osascript", captured?.ExecutablePath);
        Assert.Contains("-e", captured!.Arguments);
        Assert.Equal("Keynote", state.ApplicationName);
        Assert.True(state.IsFullScreen);
    }

    [Fact]
    public void Default_settings_match_original_do_not_disturb_defaults()
    {
        var settings = DoNotDisturbSettingsData.CreateDefault();

        Assert.False(settings.IsEnabled);
        Assert.Equal(new TimeSpan(22, 0, 0), settings.StartTime);
        Assert.Equal(new TimeSpan(8, 0, 0), settings.EndTime);
        Assert.True(settings.AllowUrgentNotifications);
        Assert.False(settings.AutoEnableInFullscreen);
        Assert.Empty(settings.ExceptionApps);
    }

    [Fact]
    public async Task Settings_store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "NotificationManagement", "DoNotDisturb", "dnd_settings.json");
        var store = new FileDoNotDisturbSettingsStore(path);
        var settings = DoNotDisturbSettingsData.CreateDefault();
        settings.IsEnabled = true;
        settings.StartTime = new TimeSpan(9, 0, 0);
        settings.EndTime = new TimeSpan(18, 0, 0);
        settings.AutoEnableInFullscreen = true;
        settings.ExceptionApps.Add("Tianming");

        await store.SaveAsync(settings);
        var reloaded = await new FileDoNotDisturbSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.IsEnabled);
        Assert.Equal(new TimeSpan(9, 0, 0), reloaded.StartTime);
        Assert.Equal(new TimeSpan(18, 0, 0), reloaded.EndTime);
        Assert.True(reloaded.AutoEnableInFullscreen);
        Assert.Equal(["Tianming"], reloaded.ExceptionApps);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileDoNotDisturbSettingsStore(path).LoadAsync();

        Assert.False(recovered.IsEnabled);
        Assert.Equal(new TimeSpan(22, 0, 0), recovered.StartTime);
    }

    [Fact]
    public void Controller_matches_original_toggle_quick_enable_and_status_text()
    {
        var settings = DoNotDisturbSettingsData.CreateDefault();
        var controller = new PortableDoNotDisturbController(settings);

        Assert.Equal("免打扰已关闭", controller.StatusText);
        Assert.Equal("#9E9E9E", controller.StatusColor);

        controller.Toggle();

        Assert.True(settings.IsEnabled);
        Assert.Equal("免打扰已启用", controller.StatusText);
        Assert.Equal("#4CAF50", controller.StatusColor);

        settings.IsEnabled = false;
        var result = controller.QuickEnable("1小时");

        Assert.True(settings.IsEnabled);
        Assert.Equal("已启用免打扰模式：1小时", result.Message);
    }
}
