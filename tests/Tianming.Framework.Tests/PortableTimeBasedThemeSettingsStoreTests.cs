using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableTimeBasedThemeSettingsStoreTests
{
    [Fact]
    public void Default_settings_match_original_time_based_theme_defaults()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();

        Assert.False(settings.Enabled);
        Assert.Equal(PortableTimeScheduleMode.Simple, settings.Mode);
        Assert.Equal(new TimeSpan(7, 0, 0), settings.DayStartTime);
        Assert.Equal(PortableThemeType.Light, settings.DayTheme);
        Assert.Equal(new TimeSpan(19, 0, 0), settings.NightStartTime);
        Assert.Equal(PortableThemeType.Dark, settings.NightTheme);
        Assert.Empty(settings.Schedules);
        Assert.Equal(39.9, settings.Latitude);
        Assert.Equal(116.4, settings.Longitude);
        Assert.False(settings.AutoLocation);
        Assert.Equal(PortableThemeType.Light, settings.SunriseTheme);
        Assert.Equal(PortableThemeType.Dark, settings.SunsetTheme);
        Assert.False(settings.ExcludeHolidays);
        Assert.False(settings.UseBuiltInHolidays);
        Assert.Empty(settings.CustomHolidays);
        Assert.Equal(PortableHolidayThemeOverride.NoChange, settings.HolidayThemeOverride);
        Assert.Equal(PortableThemeType.Light, settings.HolidayTheme);
        Assert.False(settings.TemporaryDisabled);
        Assert.Null(settings.DisabledUntil);
        Assert.True(settings.RecordHistory);
        Assert.Empty(settings.History);
        Assert.Null(settings.LastSwitchTime);
        Assert.Equal(0, settings.TotalSwitchCount);
        Assert.Null(settings.NextSwitchTime);
        Assert.False(settings.EnableVerboseLog);
        Assert.Equal(5, settings.Priority);
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically_and_creates_parent_directory()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Appearance", "AutoTheme", "TimeBased", "settings.json");
        var store = new FileTimeBasedThemeSettingsStore(path);
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.Enabled = true;
        settings.Mode = PortableTimeScheduleMode.Flexible;
        settings.DayTheme = PortableThemeType.Forest;
        settings.NightTheme = PortableThemeType.MinimalBlack;
        settings.CustomHolidays = [new DateTime(2026, 5, 11)];
        settings.Schedules =
        [
            new PortableTimeSchedule
            {
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(18, 0, 0),
                TargetTheme = PortableThemeType.Business,
                EnabledWeekdays = PortableWeekday.Weekdays,
                Priority = 8,
                Description = "工作日"
            }
        ];

        await store.SaveAsync(settings);
        var reloaded = await new FileTimeBasedThemeSettingsStore(path).LoadAsync();

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.Enabled);
        Assert.Equal(PortableTimeScheduleMode.Flexible, reloaded.Mode);
        Assert.Equal(PortableThemeType.Forest, reloaded.DayTheme);
        Assert.Equal(PortableThemeType.MinimalBlack, reloaded.NightTheme);
        Assert.Equal(new DateTime(2026, 5, 11), Assert.Single(reloaded.CustomHolidays).Date);
        var schedule = Assert.Single(reloaded.Schedules);
        Assert.Equal(PortableThemeType.Business, schedule.TargetTheme);
        Assert.Equal(PortableWeekday.Weekdays, schedule.EnabledWeekdays);
        Assert.Equal("工作日", schedule.Description);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileTimeBasedThemeSettingsStore(path);

        Assert.Equal(PortableTimeScheduleMode.Simple, (await store.LoadAsync()).Mode);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.False((await store.LoadAsync()).Enabled);
        Assert.Equal(PortableThemeType.Light, (await store.LoadAsync()).DayTheme);
    }

    [Fact]
    public void RecordSwitch_success_updates_last_switch_count_and_caps_history_at_one_hundred()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        var start = new DateTime(2026, 5, 11, 9, 0, 0);

        for (var i = 0; i < 105; i++)
        {
            PortableThemeScheduleHistoryRecorder.RecordSwitch(
                settings,
                start.AddMinutes(i),
                $"schedule-{i}",
                PortableThemeType.Dark,
                success: true);
        }

        Assert.Equal(start.AddMinutes(104), settings.LastSwitchTime);
        Assert.Equal(105, settings.TotalSwitchCount);
        Assert.Equal(100, settings.History.Count);
        Assert.Equal("schedule-5", settings.History[0].ScheduleName);
        Assert.Equal("schedule-104", settings.History[^1].ScheduleName);
        Assert.All(settings.History, record => Assert.True(record.Success));
    }

    [Fact]
    public void RecordSwitch_failure_records_history_without_success_counters()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        var timestamp = new DateTime(2026, 5, 11, 9, 0, 0);

        PortableThemeScheduleHistoryRecorder.RecordSwitch(
            settings,
            timestamp,
            "定时调度",
            PortableThemeType.Dark,
            success: false);

        Assert.Null(settings.LastSwitchTime);
        Assert.Equal(0, settings.TotalSwitchCount);
        var record = Assert.Single(settings.History);
        Assert.Equal(timestamp, record.SwitchTime);
        Assert.Equal("定时调度", record.ScheduleName);
        Assert.Equal(PortableThemeType.Dark, record.TargetTheme);
        Assert.False(record.Success);
    }

    [Fact]
    public void RecordSwitch_respects_disabled_history()
    {
        var settings = PortableTimeBasedThemeSettings.CreateDefault();
        settings.RecordHistory = false;

        PortableThemeScheduleHistoryRecorder.RecordSwitch(
            settings,
            new DateTime(2026, 5, 11, 9, 0, 0),
            "定时调度",
            PortableThemeType.Dark,
            success: true);

        Assert.Equal(1, settings.TotalSwitchCount);
        Assert.Empty(settings.History);
    }
}
