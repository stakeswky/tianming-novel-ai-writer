using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemFollowControllerTests
{
    [Fact]
    public async Task HandleAppearanceChangedAsync_switches_theme_and_saves_when_policy_allows()
    {
        var now = new DateTime(2026, 5, 11, 20, 0, 0);
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.DelaySeconds = 0;
        settings.DarkThemeMapping = PortableThemeType.MinimalBlack;
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Light);
        var saved = 0;
        var notifications = new List<PortableSystemFollowSwitchNotification>();
        var controller = new PortableSystemFollowController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saved++;
                return Task.CompletedTask;
            },
            (notification, _) =>
            {
                notifications.Add(notification);
                return Task.CompletedTask;
            },
            () => now);

        var result = await controller.HandleAppearanceChangedAsync(
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal(PortableSystemFollowDecisionStatus.Switch, result.Status);
        Assert.Equal([PortableThemeType.MinimalBlack], switcher.Switches);
        Assert.Equal(now, settings.LastSwitchTime);
        Assert.Equal(1, settings.TotalSwitchCount);
        Assert.Equal("深色主题", settings.LastDetectedTheme);
        Assert.Equal(1, saved);
        var notification = Assert.Single(notifications);
        Assert.Equal(PortableThemeType.Light, notification.FromTheme);
        Assert.Equal(PortableThemeType.MinimalBlack, notification.ToTheme);
        Assert.Equal("已切换到 MinimalBlack 主题", notification.Message);
    }

    [Fact]
    public async Task HandleAppearanceChangedAsync_skips_switch_when_policy_suppresses()
    {
        var now = new DateTime(2026, 5, 11, 22, 30, 0);
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.ExclusionPeriods =
        [
            new PortableSystemFollowExclusionPeriod
            {
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(23, 0, 0),
                Days = PortableWeekday.Monday
            }
        ];
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Light);
        var saved = 0;
        var controller = new PortableSystemFollowController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saved++;
                return Task.CompletedTask;
            },
            clock: () => now);

        var result = await controller.HandleAppearanceChangedAsync(
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal(PortableSystemFollowDecisionStatus.ExclusionSuppressed, result.Status);
        Assert.Empty(switcher.Switches);
        Assert.Equal(0, settings.TotalSwitchCount);
        Assert.Equal(0, saved);
    }

    [Fact]
    public async Task HandleAppearanceChangedAsync_does_not_switch_or_save_when_already_current()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.DarkThemeMapping = PortableThemeType.Dark;
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Dark);
        var saved = 0;
        var controller = new PortableSystemFollowController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saved++;
                return Task.CompletedTask;
            },
            clock: () => new DateTime(2026, 5, 11, 20, 0, 0));

        var result = await controller.HandleAppearanceChangedAsync(
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal(PortableSystemFollowDecisionStatus.AlreadyCurrent, result.Status);
        Assert.Empty(switcher.Switches);
        Assert.Equal(0, saved);
    }

    [Fact]
    public async Task HandleAppearanceChangedAsync_records_last_detected_theme_even_when_disabled()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = false;
        var controller = new PortableSystemFollowController(
            settings,
            () => PortableThemeType.Light,
            (_, _) => Task.CompletedTask);

        var result = await controller.HandleAppearanceChangedAsync(
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal(PortableSystemFollowDecisionStatus.Disabled, result.Status);
        Assert.Equal("深色主题", settings.LastDetectedTheme);
    }

    [Fact]
    public async Task HandleAppearanceChangedAsync_saves_failure_context_without_incrementing_success_count()
    {
        var now = new DateTime(2026, 5, 11, 20, 0, 0);
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.DelaySeconds = 0;
        var saved = 0;
        var controller = new PortableSystemFollowController(
            settings,
            () => PortableThemeType.Light,
            (_, _) => throw new InvalidOperationException("switch failed"),
            (_, _) =>
            {
                saved++;
                return Task.CompletedTask;
            },
            clock: () => now);

        var result = await controller.HandleAppearanceChangedAsync(
            new PortableSystemThemeSnapshot(false, false, null));

        Assert.Equal(PortableSystemFollowDecisionStatus.Switch, result.Status);
        Assert.Equal(0, settings.TotalSwitchCount);
        Assert.Null(settings.LastSwitchTime);
        Assert.Equal("深色主题", settings.LastDetectedTheme);
        Assert.Equal(1, saved);
    }

    private sealed class RecordingThemeSwitcher(PortableThemeType currentTheme)
    {
        private PortableThemeType _currentTheme = currentTheme;

        public List<PortableThemeType> Switches { get; } = [];

        public PortableThemeType GetCurrentTheme()
        {
            return _currentTheme;
        }

        public Task SwitchThemeAsync(PortableThemeType targetTheme, CancellationToken cancellationToken)
        {
            _currentTheme = targetTheme;
            Switches.Add(targetTheme);
            return Task.CompletedTask;
        }
    }
}
