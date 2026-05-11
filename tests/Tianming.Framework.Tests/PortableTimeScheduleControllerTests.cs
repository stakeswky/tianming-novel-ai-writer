using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableTimeScheduleControllerTests
{
    [Fact]
    public async Task StartAsync_marks_running_and_switches_immediately_when_target_differs()
    {
        var now = new DateTime(2026, 5, 11, 20, 0, 0);
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.NightTheme = PortableThemeType.Dark;
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Light);
        var saveCount = 0;
        var controller = new PortableTimeScheduleController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            () => now);

        await controller.StartAsync();

        Assert.True(controller.IsRunning);
        Assert.Equal([PortableThemeType.Dark], switcher.Switches);
        Assert.Equal(1, settings.TotalSwitchCount);
        Assert.Equal(now, settings.LastSwitchTime);
        var record = Assert.Single(settings.History);
        Assert.Equal("定时调度", record.ScheduleName);
        Assert.True(record.Success);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task CheckAndSwitchAsync_does_not_switch_or_save_when_theme_is_already_current()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Light);
        var saveCount = 0;
        var controller = new PortableTimeScheduleController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            () => new DateTime(2026, 5, 11, 12, 0, 0));

        await controller.CheckAndSwitchAsync();

        Assert.Empty(switcher.Switches);
        Assert.Empty(settings.History);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public async Task CheckAndSwitchAsync_clears_expired_temporary_disable_before_switching()
    {
        var now = new DateTime(2026, 5, 11, 14, 0, 0);
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.TemporaryDisabled = true;
        settings.DisabledUntil = now.AddMinutes(-1);
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Dark);
        var saveCount = 0;
        var controller = new PortableTimeScheduleController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            () => now);

        await controller.CheckAndSwitchAsync();

        Assert.False(settings.TemporaryDisabled);
        Assert.Null(settings.DisabledUntil);
        Assert.Equal([PortableThemeType.Light], switcher.Switches);
        Assert.Equal(2, saveCount);
    }

    [Fact]
    public async Task CheckAndSwitchAsync_keeps_active_temporary_disable_without_switching()
    {
        var now = new DateTime(2026, 5, 11, 14, 0, 0);
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.TemporaryDisabled = true;
        settings.DisabledUntil = now.AddMinutes(1);
        var switcher = new RecordingThemeSwitcher(PortableThemeType.Dark);
        var controller = new PortableTimeScheduleController(
            settings,
            switcher.GetCurrentTheme,
            switcher.SwitchThemeAsync,
            (_, _) => Task.CompletedTask,
            () => now);

        await controller.CheckAndSwitchAsync();

        Assert.True(settings.TemporaryDisabled);
        Assert.Empty(switcher.Switches);
        Assert.Empty(settings.History);
    }

    [Fact]
    public async Task CheckAndSwitchAsync_records_failed_switch_without_throwing()
    {
        var now = new DateTime(2026, 5, 11, 20, 0, 0);
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        var saveCount = 0;
        var controller = new PortableTimeScheduleController(
            settings,
            () => PortableThemeType.Light,
            (_, _) => throw new InvalidOperationException("switch failed"),
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            () => now);

        await controller.CheckAndSwitchAsync();

        Assert.Equal(0, settings.TotalSwitchCount);
        Assert.Null(settings.LastSwitchTime);
        var record = Assert.Single(settings.History);
        Assert.Equal(PortableThemeType.Dark, record.TargetTheme);
        Assert.False(record.Success);
        Assert.Equal(1, saveCount);
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
