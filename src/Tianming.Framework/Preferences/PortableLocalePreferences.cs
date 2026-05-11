using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Preferences;

public enum PortableLocalePlatform
{
    Windows,
    MacOS,
    Linux
}

public sealed class PortableLocaleSettings
{
    [JsonPropertyName("Language")]
    public string Language { get; set; } = "zh-CN";

    [JsonPropertyName("LanguageName")]
    public string LanguageName { get; set; } = "简体中文";

    [JsonPropertyName("TimeZoneId")]
    public string TimeZoneId { get; set; } = "China Standard Time";

    [JsonPropertyName("DateFormat")]
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    [JsonPropertyName("TimeFormat")]
    public string TimeFormat { get; set; } = "HH:mm:ss";

    [JsonPropertyName("NumberFormat")]
    public string NumberFormat { get; set; } = "1,234.56";

    [JsonPropertyName("CurrencySymbol")]
    public string CurrencySymbol { get; set; } = "¥";

    [JsonPropertyName("Use24HourFormat")]
    public bool Use24HourFormat { get; set; } = true;

    [JsonPropertyName("WeekStartDay")]
    public int WeekStartDay { get; set; } = 1;

    [JsonPropertyName("LastModified")]
    public DateTime LastModified { get; set; } = DateTime.Now;

    public static PortableLocaleSettings CreateDefault(DateTime? lastModified = null)
    {
        return new PortableLocaleSettings
        {
            Language = "zh-CN",
            LanguageName = "简体中文",
            TimeZoneId = "China Standard Time",
            DateFormat = "yyyy-MM-dd",
            TimeFormat = "HH:mm:ss",
            NumberFormat = "1,234.56",
            CurrencySymbol = "¥",
            Use24HourFormat = true,
            WeekStartDay = 1,
            LastModified = lastModified ?? DateTime.Now
        };
    }

    public PortableLocaleSettings Clone()
    {
        return new PortableLocaleSettings
        {
            Language = Language,
            LanguageName = LanguageName,
            TimeZoneId = TimeZoneId,
            DateFormat = DateFormat,
            TimeFormat = TimeFormat,
            NumberFormat = NumberFormat,
            CurrencySymbol = CurrencySymbol,
            Use24HourFormat = Use24HourFormat,
            WeekStartDay = WeekStartDay,
            LastModified = LastModified
        };
    }

    public void CopyFrom(PortableLocaleSettings other)
    {
        Language = other.Language;
        LanguageName = other.LanguageName;
        TimeZoneId = other.TimeZoneId;
        DateFormat = other.DateFormat;
        TimeFormat = other.TimeFormat;
        NumberFormat = other.NumberFormat;
        CurrencySymbol = other.CurrencySymbol;
        Use24HourFormat = other.Use24HourFormat;
        WeekStartDay = other.WeekStartDay;
        LastModified = other.LastModified;
    }
}

public sealed class FileLocaleSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly Func<DateTime> _clock;

    public FileLocaleSettingsStore(string path, Func<DateTime>? clock = null)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableLocaleSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
                return PortableLocaleSettings.CreateDefault(_clock());

            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<PortableLocaleSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return settings ?? PortableLocaleSettings.CreateDefault(_clock());
        }
        catch (JsonException)
        {
            return PortableLocaleSettings.CreateDefault(_clock());
        }
        catch (IOException)
        {
            return PortableLocaleSettings.CreateDefault(_clock());
        }
        catch (UnauthorizedAccessException)
        {
            return PortableLocaleSettings.CreateDefault(_clock());
        }
    }

    public async Task SaveAsync(PortableLocaleSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        settings.LastModified = _clock();
        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _path, overwrite: true);
    }
}

public sealed class PortableLocaleController
{
    private static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = "简体中文",
        ["en-US"] = "English"
    };

    private readonly PortableLocaleSettings _settings;
    private readonly Func<DateTime> _clock;

    public PortableLocaleController(PortableLocaleSettings settings, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _clock = clock ?? (() => DateTime.Now);
    }

    public void UpdateLanguage(string language)
    {
        _settings.Language = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language;
        _settings.LanguageName = GetLanguageName(_settings.Language);
        _settings.LastModified = _clock();
    }

    public void UpdateTimeZone(string timeZoneId)
    {
        _settings.TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "China Standard Time" : timeZoneId;
        _settings.LastModified = _clock();
    }

    public void UpdateDateFormat(string format)
    {
        _settings.DateFormat = string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd" : format;
        _settings.LastModified = _clock();
    }

    public void UpdateNumberFormat(string format)
    {
        _settings.NumberFormat = string.IsNullOrWhiteSpace(format) ? "1,234.56" : format;
        _settings.LastModified = _clock();
    }

    public void ResetToDefaults()
    {
        _settings.CopyFrom(PortableLocaleSettings.CreateDefault(_clock()));
    }

    public static PortableLocaleApplyPlan BuildApplyPlan(PortableLocaleSettings settings, PortableLocalePlatform platform)
    {
        var cultureName = ResolveCultureName(settings.Language);
        var resolvedTimeZoneId = ResolveTimeZoneId(settings.TimeZoneId, platform);
        var offset = ResolveUtcOffset(resolvedTimeZoneId);

        return new PortableLocaleApplyPlan(
            cultureName,
            GetLanguageName(cultureName),
            resolvedTimeZoneId,
            FormatTimeZoneDisplay(offset),
            settings.DateFormat,
            BuildNumberFormatPreview(settings.NumberFormat),
            requiresRestart: true);
    }

    private static string GetLanguageName(string language)
    {
        return LanguageNames.TryGetValue(language, out var name) ? name : language;
    }

    private static string ResolveCultureName(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language).Name;
        }
        catch (CultureNotFoundException)
        {
            return "zh-CN";
        }
    }

    private static string ResolveTimeZoneId(string timeZoneId, PortableLocalePlatform platform)
    {
        if (platform is PortableLocalePlatform.MacOS or PortableLocalePlatform.Linux &&
            string.Equals(timeZoneId, "China Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return "Asia/Shanghai";
        }

        return string.IsNullOrWhiteSpace(timeZoneId) ? "China Standard Time" : timeZoneId;
    }

    private static TimeSpan ResolveUtcOffset(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).BaseUtcOffset;
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeSpan.FromHours(8);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeSpan.FromHours(8);
        }
    }

    private static string FormatTimeZoneDisplay(TimeSpan offset)
    {
        return $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.TotalHours:F1}";
    }

    private static string BuildNumberFormatPreview(string format)
    {
        const double sample = 1234.56;
        return format switch
        {
            "1,234.56" => sample.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
            "1 234,56" => sample.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")),
            "1.234,56" => sample.ToString("N2", CultureInfo.GetCultureInfo("de-DE")),
            _ => sample.ToString("N2", CultureInfo.InvariantCulture)
        };
    }
}

public sealed class PortableLocaleApplyPlan
{
    private readonly string _dateFormat;

    public PortableLocaleApplyPlan(
        string cultureName,
        string languageName,
        string resolvedTimeZoneId,
        string timeZoneDisplay,
        string dateFormat,
        string numberFormatPreview,
        bool requiresRestart)
    {
        CultureName = cultureName;
        LanguageName = languageName;
        ResolvedTimeZoneId = resolvedTimeZoneId;
        TimeZoneDisplay = timeZoneDisplay;
        NumberFormatPreview = numberFormatPreview;
        RequiresRestart = requiresRestart;
        _dateFormat = string.IsNullOrWhiteSpace(dateFormat) ? "yyyy-MM-dd" : dateFormat;
    }

    public string CultureName { get; }

    public string LanguageName { get; }

    public string ResolvedTimeZoneId { get; }

    public string TimeZoneDisplay { get; }

    public string NumberFormatPreview { get; }

    public bool RequiresRestart { get; }

    public string FormatDate(DateTime value)
    {
        try
        {
            return value.ToString(_dateFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
