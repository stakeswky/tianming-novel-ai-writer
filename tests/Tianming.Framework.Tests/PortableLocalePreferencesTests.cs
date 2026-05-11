using TM.Framework.Preferences;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLocalePreferencesTests
{
    [Fact]
    public void Default_settings_match_original_locale_defaults()
    {
        var settings = PortableLocaleSettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal("简体中文", settings.LanguageName);
        Assert.Equal("China Standard Time", settings.TimeZoneId);
        Assert.Equal("yyyy-MM-dd", settings.DateFormat);
        Assert.Equal("HH:mm:ss", settings.TimeFormat);
        Assert.Equal("1,234.56", settings.NumberFormat);
        Assert.Equal("¥", settings.CurrencySymbol);
        Assert.True(settings.Use24HourFormat);
        Assert.Equal(1, settings.WeekStartDay);
        Assert.Equal(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc), settings.LastModified);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Preferences", "Locale", "locale_settings.json");
        var now = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc);
        var store = new FileLocaleSettingsStore(path, () => now);
        var settings = PortableLocaleSettings.CreateDefault(now);
        settings.Language = "en-US";
        settings.LanguageName = "English";
        settings.TimeZoneId = "America/Los_Angeles";
        settings.DateFormat = "MM/dd/yyyy";
        settings.NumberFormat = "1.234,56";

        await store.SaveAsync(settings);
        var reloaded = await new FileLocaleSettingsStore(path, () => now).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("en-US", reloaded.Language);
        Assert.Equal("English", reloaded.LanguageName);
        Assert.Equal("America/Los_Angeles", reloaded.TimeZoneId);
        Assert.Equal("MM/dd/yyyy", reloaded.DateFormat);
        Assert.Equal("1.234,56", reloaded.NumberFormat);
        Assert.Equal(now, reloaded.LastModified);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileLocaleSettingsStore(path, () => now).LoadAsync();

        Assert.Equal("zh-CN", recovered.Language);
        Assert.Equal("China Standard Time", recovered.TimeZoneId);
        Assert.Equal("1,234.56", recovered.NumberFormat);
    }

    [Fact]
    public void Controller_updates_settings_and_preserves_language_display_names()
    {
        var settings = PortableLocaleSettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        var controller = new PortableLocaleController(settings);

        controller.UpdateLanguage("en-US");
        controller.UpdateTimeZone("Asia/Tokyo");
        controller.UpdateDateFormat("dd/MM/yyyy");
        controller.UpdateNumberFormat("1 234,56");

        Assert.Equal("en-US", settings.Language);
        Assert.Equal("English", settings.LanguageName);
        Assert.Equal("Asia/Tokyo", settings.TimeZoneId);
        Assert.Equal("dd/MM/yyyy", settings.DateFormat);
        Assert.Equal("1 234,56", settings.NumberFormat);

        controller.ResetToDefaults();

        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal("简体中文", settings.LanguageName);
        Assert.Equal("China Standard Time", settings.TimeZoneId);
    }

    [Fact]
    public void BuildApplyPlan_resolves_windows_timezone_aliases_for_macos()
    {
        var settings = PortableLocaleSettings.CreateDefault(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        var plan = PortableLocaleController.BuildApplyPlan(settings, PortableLocalePlatform.MacOS);

        Assert.Equal("zh-CN", plan.CultureName);
        Assert.Equal("Asia/Shanghai", plan.ResolvedTimeZoneId);
        Assert.Equal("UTC+8.0", plan.TimeZoneDisplay);
        Assert.Equal("2026-05-10", plan.FormatDate(new DateTime(2026, 5, 10)));
        Assert.Equal("1,234.56", plan.NumberFormatPreview);
        Assert.True(plan.RequiresRestart);
    }
}
