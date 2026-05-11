using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeScheduleServiceTests
{
    [Theory]
    [InlineData(8, PortableThemeType.Light)]
    [InlineData(20, PortableThemeType.Dark)]
    public void CalculateTargetTheme_uses_simple_day_and_night_windows(int hour, PortableThemeType expected)
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.Mode = PortableTimeScheduleMode.Simple;
        settings.DayStartTime = new TimeSpan(7, 0, 0);
        settings.DayTheme = PortableThemeType.Light;
        settings.NightStartTime = new TimeSpan(19, 0, 0);
        settings.NightTheme = PortableThemeType.Dark;

        var result = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, hour, 0, 0));

        Assert.Equal(expected, result.TargetTheme);
        Assert.Equal(PortableThemeScheduleDecisionStatus.Switch, result.Status);
    }

    [Theory]
    [InlineData(23, PortableThemeType.Green)]
    [InlineData(2, PortableThemeType.Green)]
    [InlineData(12, PortableThemeType.Dark)]
    public void CalculateTargetTheme_handles_simple_window_that_crosses_midnight(
        int hour,
        PortableThemeType expected)
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.Mode = PortableTimeScheduleMode.Simple;
        settings.DayStartTime = new TimeSpan(22, 0, 0);
        settings.DayTheme = PortableThemeType.Green;
        settings.NightStartTime = new TimeSpan(6, 0, 0);
        settings.NightTheme = PortableThemeType.Dark;

        var result = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, hour, 0, 0));

        Assert.Equal(expected, result.TargetTheme);
    }

    [Fact]
    public void CalculateTargetTheme_selects_flexible_schedule_by_priority_then_start_time()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.Mode = PortableTimeScheduleMode.Flexible;
        settings.Schedules =
        [
            new PortableTimeSchedule
            {
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                TargetTheme = PortableThemeType.Light,
                EnabledWeekdays = PortableWeekday.All,
                Priority = 5
            },
            new PortableTimeSchedule
            {
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                TargetTheme = PortableThemeType.Business,
                EnabledWeekdays = PortableWeekday.All,
                Priority = 8
            },
            new PortableTimeSchedule
            {
                StartTime = new TimeSpan(10, 30, 0),
                EndTime = new TimeSpan(11, 30, 0),
                TargetTheme = PortableThemeType.ModernBlue,
                EnabledWeekdays = PortableWeekday.All,
                Priority = 8
            }
        ];

        var result = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 11, 0, 0));

        Assert.Equal(PortableThemeType.ModernBlue, result.TargetTheme);
        Assert.Equal("灵活时间段", result.Reason);
    }

    [Fact]
    public void CalculateTargetTheme_matches_flexible_overnight_schedule_on_enabled_weekday()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.Mode = PortableTimeScheduleMode.Flexible;
        settings.Schedules =
        [
            new PortableTimeSchedule
            {
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(7, 0, 0),
                TargetTheme = PortableThemeType.MinimalBlack,
                EnabledWeekdays = PortableWeekday.Monday,
                Priority = 3
            }
        ];

        var mondayNight = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 23, 0, 0));
        var tuesdayNight = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 12, 23, 0, 0));

        Assert.Equal(PortableThemeType.MinimalBlack, mondayNight.TargetTheme);
        Assert.Equal(PortableThemeScheduleDecisionStatus.NoMatchingSchedule, tuesdayNight.Status);
    }

    [Fact]
    public void CalculateTargetTheme_skips_when_custom_holiday_is_excluded()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.ExcludeHolidays = true;
        settings.CustomHolidays = [new DateTime(2026, 5, 11)];

        var result = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 12, 0, 0));

        Assert.Null(result.TargetTheme);
        Assert.Equal(PortableThemeScheduleDecisionStatus.SkippedHoliday, result.Status);
    }

    [Fact]
    public void CalculateTargetTheme_applies_holiday_theme_override_after_base_schedule()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.CustomHolidays = [new DateTime(2026, 5, 11)];
        settings.HolidayThemeOverride = PortableHolidayThemeOverride.Custom;
        settings.HolidayTheme = PortableThemeType.Sunset;

        var result = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 12, 0, 0));

        Assert.Equal(PortableThemeType.Sunset, result.TargetTheme);
        Assert.Equal("节假日主题覆盖", result.Reason);
    }

    [Fact]
    public void CalculateTargetTheme_keeps_temporary_disable_until_expiration()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.TemporaryDisabled = true;
        settings.DisabledUntil = new DateTime(2026, 5, 11, 14, 0, 0);

        var beforeExpiration = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 13, 59, 0));
        var afterExpiration = PortableThemeScheduleService.CalculateTargetTheme(
            settings,
            new DateTime(2026, 5, 11, 14, 0, 0));

        Assert.Equal(PortableThemeScheduleDecisionStatus.TemporaryDisabled, beforeExpiration.Status);
        Assert.Equal(PortableThemeType.Light, afterExpiration.TargetTheme);
    }
}
