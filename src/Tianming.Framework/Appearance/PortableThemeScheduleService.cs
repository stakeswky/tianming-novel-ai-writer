using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public enum PortableThemeType
{
    Light,
    Dark,
    Auto,
    Green,
    Business,
    ModernBlue,
    Violet,
    WarmOrange,
    Pink,
    TechCyan,
    MinimalBlack,
    Arctic,
    Forest,
    Sunset,
    Morandi,
    HighContrast,
    Custom
}

public enum PortableTimeScheduleMode
{
    Simple,
    Flexible,
    Sunrise
}

[Flags]
public enum PortableWeekday
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekends = Saturday | Sunday,
    All = Weekdays | Weekends
}

public enum PortableHolidayThemeOverride
{
    NoChange,
    ForceLight,
    ForceDark,
    Custom
}

public enum PortableThemeScheduleDecisionStatus
{
    Switch,
    Disabled,
    TemporaryDisabled,
    SkippedHoliday,
    NoMatchingSchedule
}

public sealed class PortableTimeSchedule
{
    [JsonPropertyName("StartTime")] public TimeSpan StartTime { get; set; }

    [JsonPropertyName("EndTime")] public TimeSpan EndTime { get; set; }

    [JsonPropertyName("TargetTheme")] public PortableThemeType TargetTheme { get; set; }

    [JsonPropertyName("EnabledWeekdays")] public PortableWeekday EnabledWeekdays { get; set; } = PortableWeekday.All;

    [JsonPropertyName("UseTransition")] public bool UseTransition { get; set; } = true;

    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Priority")] public int Priority { get; set; } = 5;
}

public sealed class PortableThemeSwitchHistoryRecord
{
    [JsonPropertyName("SwitchTime")] public DateTime SwitchTime { get; set; }

    [JsonPropertyName("ScheduleName")] public string ScheduleName { get; set; } = string.Empty;

    [JsonPropertyName("TargetTheme")] public PortableThemeType TargetTheme { get; set; }

    [JsonPropertyName("Success")] public bool Success { get; set; }
}

public sealed class PortableTimeBasedThemeSettings
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; }

    [JsonPropertyName("Mode")] public PortableTimeScheduleMode Mode { get; set; } = PortableTimeScheduleMode.Simple;

    [JsonPropertyName("DayStartTime")] public TimeSpan DayStartTime { get; set; } = new(7, 0, 0);

    [JsonPropertyName("DayTheme")] public PortableThemeType DayTheme { get; set; } = PortableThemeType.Light;

    [JsonPropertyName("NightStartTime")] public TimeSpan NightStartTime { get; set; } = new(19, 0, 0);

    [JsonPropertyName("NightTheme")] public PortableThemeType NightTheme { get; set; } = PortableThemeType.Dark;

    [JsonPropertyName("Schedules")] public List<PortableTimeSchedule> Schedules { get; set; } = new();

    [JsonPropertyName("Latitude")] public double Latitude { get; set; } = 39.9;

    [JsonPropertyName("Longitude")] public double Longitude { get; set; } = 116.4;

    [JsonPropertyName("AutoLocation")] public bool AutoLocation { get; set; }

    [JsonPropertyName("SunriseTheme")] public PortableThemeType SunriseTheme { get; set; } = PortableThemeType.Light;

    [JsonPropertyName("SunsetTheme")] public PortableThemeType SunsetTheme { get; set; } = PortableThemeType.Dark;

    [JsonPropertyName("ExcludeHolidays")] public bool ExcludeHolidays { get; set; }

    [JsonPropertyName("UseBuiltInHolidays")] public bool UseBuiltInHolidays { get; set; }

    [JsonPropertyName("CustomHolidays")] public List<DateTime> CustomHolidays { get; set; } = new();

    [JsonPropertyName("HolidayThemeOverride")] public PortableHolidayThemeOverride HolidayThemeOverride { get; set; }

    [JsonPropertyName("HolidayTheme")] public PortableThemeType HolidayTheme { get; set; } = PortableThemeType.Light;

    [JsonPropertyName("TemporaryDisabled")] public bool TemporaryDisabled { get; set; }

    [JsonPropertyName("DisabledUntil")] public DateTime? DisabledUntil { get; set; }

    [JsonPropertyName("RecordHistory")] public bool RecordHistory { get; set; } = true;

    [JsonPropertyName("History")] public List<PortableThemeSwitchHistoryRecord> History { get; set; } = new();

    [JsonPropertyName("LastSwitchTime")] public DateTime? LastSwitchTime { get; set; }

    [JsonPropertyName("TotalSwitchCount")] public int TotalSwitchCount { get; set; }

    [JsonPropertyName("NextSwitchTime")] public DateTime? NextSwitchTime { get; set; }

    [JsonPropertyName("EnableVerboseLog")] public bool EnableVerboseLog { get; set; }

    [JsonPropertyName("Priority")] public int Priority { get; set; } = 5;

    public static PortableTimeBasedThemeSettings CreateDefault()
    {
        return new PortableTimeBasedThemeSettings();
    }
}

public sealed class PortableThemeScheduleDecision
{
    public PortableThemeScheduleDecisionStatus Status { get; init; }

    public PortableThemeType? TargetTheme { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public static class PortableThemeScheduleService
{
    public static PortableThemeScheduleDecision CalculateTargetTheme(
        PortableTimeBasedThemeSettings settings,
        DateTime now,
        Func<DateTime, bool>? builtInHolidayChecker = null,
        Func<DateTime, double, double, (TimeSpan Sunrise, TimeSpan Sunset)>? sunTimesProvider = null)
    {
        if (!settings.Enabled)
        {
            return new PortableThemeScheduleDecision
            {
                Status = PortableThemeScheduleDecisionStatus.Disabled,
                Reason = "定时主题未启用"
            };
        }

        if (settings.TemporaryDisabled && (!settings.DisabledUntil.HasValue || now < settings.DisabledUntil.Value))
        {
            return new PortableThemeScheduleDecision
            {
                Status = PortableThemeScheduleDecisionStatus.TemporaryDisabled,
                Reason = "定时主题临时禁用"
            };
        }

        var isHoliday = IsHoliday(settings, now, builtInHolidayChecker);
        if (settings.ExcludeHolidays && isHoliday)
        {
            return new PortableThemeScheduleDecision
            {
                Status = PortableThemeScheduleDecisionStatus.SkippedHoliday,
                Reason = "节假日跳过"
            };
        }

        var targetTheme = CalculateBaseTheme(settings, now, sunTimesProvider);
        if (!targetTheme.HasValue)
        {
            return new PortableThemeScheduleDecision
            {
                Status = PortableThemeScheduleDecisionStatus.NoMatchingSchedule,
                Reason = "没有匹配的时间段"
            };
        }

        if (isHoliday && settings.HolidayThemeOverride != PortableHolidayThemeOverride.NoChange)
        {
            return new PortableThemeScheduleDecision
            {
                Status = PortableThemeScheduleDecisionStatus.Switch,
                TargetTheme = settings.HolidayThemeOverride switch
                {
                    PortableHolidayThemeOverride.ForceLight => PortableThemeType.Light,
                    PortableHolidayThemeOverride.ForceDark => PortableThemeType.Dark,
                    PortableHolidayThemeOverride.Custom => settings.HolidayTheme,
                    _ => targetTheme
                },
                Reason = "节假日主题覆盖"
            };
        }

        return new PortableThemeScheduleDecision
        {
            Status = PortableThemeScheduleDecisionStatus.Switch,
            TargetTheme = targetTheme,
            Reason = settings.Mode == PortableTimeScheduleMode.Flexible ? "灵活时间段" : "定时主题"
        };
    }

    public static PortableWeekday ConvertDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => PortableWeekday.Monday,
            DayOfWeek.Tuesday => PortableWeekday.Tuesday,
            DayOfWeek.Wednesday => PortableWeekday.Wednesday,
            DayOfWeek.Thursday => PortableWeekday.Thursday,
            DayOfWeek.Friday => PortableWeekday.Friday,
            DayOfWeek.Saturday => PortableWeekday.Saturday,
            DayOfWeek.Sunday => PortableWeekday.Sunday,
            _ => PortableWeekday.None
        };
    }

    public static IReadOnlyList<string> DetectConflicts(IReadOnlyList<PortableTimeSchedule> schedules)
    {
        var conflicts = new List<string>();
        for (var i = 0; i < schedules.Count; i++)
        {
            for (var j = i + 1; j < schedules.Count; j++)
            {
                var first = schedules[i];
                var second = schedules[j];
                if ((first.EnabledWeekdays & second.EnabledWeekdays) == PortableWeekday.None)
                {
                    continue;
                }

                if (IsTimeOverlap(first.StartTime, first.EndTime, second.StartTime, second.EndTime))
                {
                    conflicts.Add($"时间段 {i + 1} 与时间段 {j + 1} 冲突");
                }
            }
        }

        return conflicts;
    }

    private static PortableThemeType? CalculateBaseTheme(
        PortableTimeBasedThemeSettings settings,
        DateTime now,
        Func<DateTime, double, double, (TimeSpan Sunrise, TimeSpan Sunset)>? sunTimesProvider)
    {
        return settings.Mode switch
        {
            PortableTimeScheduleMode.Simple => IsInsideWindow(
                    now.TimeOfDay,
                    settings.DayStartTime,
                    settings.NightStartTime)
                ? settings.DayTheme
                : settings.NightTheme,
            PortableTimeScheduleMode.Flexible => CalculateFlexibleTheme(settings, now),
            PortableTimeScheduleMode.Sunrise => CalculateSunriseTheme(settings, now, sunTimesProvider),
            _ => null
        };
    }

    private static PortableThemeType? CalculateFlexibleTheme(PortableTimeBasedThemeSettings settings, DateTime now)
    {
        var currentWeekday = ConvertDayOfWeek(now.DayOfWeek);
        return settings.Schedules
            .OrderByDescending(schedule => schedule.Priority)
            .ThenByDescending(schedule => schedule.StartTime)
            .Where(schedule => (schedule.EnabledWeekdays & currentWeekday) == currentWeekday)
            .FirstOrDefault(schedule => IsInsideWindow(now.TimeOfDay, schedule.StartTime, schedule.EndTime))
            ?.TargetTheme;
    }

    private static PortableThemeType CalculateSunriseTheme(
        PortableTimeBasedThemeSettings settings,
        DateTime now,
        Func<DateTime, double, double, (TimeSpan Sunrise, TimeSpan Sunset)>? sunTimesProvider)
    {
        var (sunrise, sunset) = sunTimesProvider?.Invoke(now, settings.Latitude, settings.Longitude)
            ?? PortableSunCalculator.CalculateSunTimes(now, settings.Latitude, settings.Longitude);

        return IsInsideWindow(now.TimeOfDay, sunrise, sunset)
            ? settings.SunriseTheme
            : settings.SunsetTheme;
    }

    private static bool IsHoliday(
        PortableTimeBasedThemeSettings settings,
        DateTime now,
        Func<DateTime, bool>? builtInHolidayChecker)
    {
        if (settings.CustomHolidays.Any(holiday => holiday.Date == now.Date))
        {
            return true;
        }

        return settings.UseBuiltInHolidays && builtInHolidayChecker?.Invoke(now) == true;
    }

    private static bool IsInsideWindow(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        if (start <= end)
        {
            return current >= start && current < end;
        }

        return current >= start || current < end;
    }

    private static bool IsTimeOverlap(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
    {
        if (start1 <= end1 && start2 <= end2)
        {
            return start1 < end2 && end1 > start2;
        }

        return true;
    }

}

public static class PortableThemeScheduleHistoryRecorder
{
    public static void RecordSwitch(
        PortableTimeBasedThemeSettings settings,
        DateTime switchTime,
        string scheduleName,
        PortableThemeType targetTheme,
        bool success)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (success)
        {
            settings.LastSwitchTime = switchTime;
            settings.TotalSwitchCount++;
        }

        if (!settings.RecordHistory)
        {
            return;
        }

        settings.History.Add(new PortableThemeSwitchHistoryRecord
        {
            SwitchTime = switchTime,
            ScheduleName = scheduleName,
            TargetTheme = targetTheme,
            Success = success
        });

        if (settings.History.Count > 100)
        {
            settings.History.RemoveRange(0, settings.History.Count - 100);
        }
    }
}

public sealed class FileTimeBasedThemeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileTimeBasedThemeSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Time-based theme settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableTimeBasedThemeSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableTimeBasedThemeSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableTimeBasedThemeSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableTimeBasedThemeSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableTimeBasedThemeSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableTimeBasedThemeSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableTimeBasedThemeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
