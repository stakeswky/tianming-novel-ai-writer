using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemFollowTests
{
    [Fact]
    public void Default_settings_match_original_system_follow_defaults()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();

        Assert.False(settings.Enabled);
        Assert.True(settings.AutoStart);
        Assert.Equal(PortableThemeType.Light, settings.LightThemeMapping);
        Assert.Equal(PortableThemeType.Dark, settings.DarkThemeMapping);
        Assert.Equal(PortableHighContrastBehavior.Ignore, settings.HighContrastMapping);
        Assert.Equal(PortableThemeType.Dark, settings.HighContrastCustomTheme);
        Assert.Equal(3, settings.DelaySeconds);
        Assert.True(settings.ShowNotification);
        Assert.False(settings.EnableAccentColor);
        Assert.False(settings.OnlyWhenNotManual);
        Assert.Empty(settings.ExclusionPeriods);
        Assert.Equal(30, settings.MinSwitchInterval);
        Assert.Equal(5, settings.DebounceDelay);
        Assert.True(settings.EnableSmartDelay);
        Assert.False(settings.EnableSceneDetection);
        Assert.Empty(settings.SceneRules);
        Assert.Equal(5, settings.Priority);
        Assert.Null(settings.LastSwitchTime);
        Assert.Equal(0, settings.TotalSwitchCount);
        Assert.Equal("未知", settings.LastDetectedTheme);
        Assert.False(settings.EnableVerboseLog);
    }

    [Theory]
    [InlineData(true, false, PortableThemeType.Green)]
    [InlineData(false, false, PortableThemeType.MinimalBlack)]
    [InlineData(true, true, PortableThemeType.Business)]
    public void DetermineTargetTheme_maps_light_dark_and_high_contrast(
        bool isLight,
        bool isHighContrast,
        PortableThemeType expected)
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.LightThemeMapping = PortableThemeType.Green;
        settings.DarkThemeMapping = PortableThemeType.MinimalBlack;
        settings.HighContrastMapping = isHighContrast
            ? PortableHighContrastBehavior.Custom
            : PortableHighContrastBehavior.Ignore;
        settings.HighContrastCustomTheme = PortableThemeType.Business;

        var snapshot = new PortableSystemThemeSnapshot(isLight, isHighContrast, "#ABCDEF");
        var target = PortableSystemFollowPolicy.DetermineTargetTheme(
            snapshot,
            settings,
            currentTheme: isLight ? PortableThemeType.Light : PortableThemeType.Dark);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void DetermineTargetTheme_ignores_high_contrast_when_configured()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.HighContrastMapping = PortableHighContrastBehavior.Ignore;

        var target = PortableSystemFollowPolicy.DetermineTargetTheme(
            new PortableSystemThemeSnapshot(IsLightTheme: false, IsHighContrast: true, AccentColor: null),
            settings,
            currentTheme: PortableThemeType.Sunset);

        Assert.Equal(PortableThemeType.Sunset, target);
    }

    [Fact]
    public void IsInExclusionPeriod_handles_same_day_and_cross_midnight_windows()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.ExclusionPeriods =
        [
            new PortableSystemFollowExclusionPeriod
            {
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(11, 0, 0),
                Days = PortableWeekday.Monday,
                Description = "writing"
            },
            new PortableSystemFollowExclusionPeriod
            {
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(2, 0, 0),
                Days = PortableWeekday.Monday | PortableWeekday.Tuesday,
                Description = "night"
            }
        ];

        Assert.True(PortableSystemFollowPolicy.IsInExclusionPeriod(
            settings,
            new DateTime(2026, 5, 11, 10, 0, 0)));
        Assert.True(PortableSystemFollowPolicy.IsInExclusionPeriod(
            settings,
            new DateTime(2026, 5, 12, 1, 0, 0)));
        Assert.False(PortableSystemFollowPolicy.IsInExclusionPeriod(
            settings,
            new DateTime(2026, 5, 12, 12, 0, 0)));
    }

    [Fact]
    public void Scene_detector_uses_enabled_rules_and_cross_midnight_windows()
    {
        var rules = new[]
        {
            new PortableSystemFollowSceneRule
            {
                SceneName = "presentation",
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(2, 0, 0),
                DisableSwitching = true,
                Enabled = true,
                Description = "do not flash themes"
            },
            new PortableSystemFollowSceneRule
            {
                SceneName = "disabled",
                StartTime = TimeSpan.Zero,
                EndTime = new TimeSpan(23, 59, 0),
                DisableSwitching = true,
                Enabled = false
            }
        };

        var scene = PortableSystemFollowSceneDetector.DetectCurrentScene(
            rules,
            new TimeSpan(1, 30, 0));

        Assert.True(scene.IsActive);
        Assert.True(scene.DisableSwitching);
        Assert.Equal("presentation", scene.SceneName);
        Assert.Equal("do not flash themes", scene.Description);
    }

    [Fact]
    public void ShouldSwitch_reports_suppression_reasons()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.EnableSceneDetection = true;
        settings.SceneRules =
        [
            new PortableSystemFollowSceneRule
            {
                SceneName = "meeting",
                StartTime = TimeSpan.Zero,
                EndTime = new TimeSpan(23, 59, 59),
                DisableSwitching = true
            }
        ];

        var decision = PortableSystemFollowPolicy.EvaluateSwitch(
            new PortableSystemThemeSnapshot(false, false, null),
            settings,
            currentTheme: PortableThemeType.Light,
            now: new DateTime(2026, 5, 11, 13, 0, 0));

        Assert.Equal(PortableSystemFollowDecisionStatus.SceneSuppressed, decision.Status);
        Assert.Equal(PortableThemeType.Dark, decision.TargetTheme);
        Assert.Equal("meeting", decision.ActiveSceneName);
    }

    [Fact]
    public void MacOS_appearance_parser_maps_defaults_output_to_theme_snapshot()
    {
        var dark = MacOSSystemAppearanceParser.ParseAppleInterfaceStyle("Dark\n");
        var light = MacOSSystemAppearanceParser.ParseAppleInterfaceStyle(string.Empty);
        var missing = MacOSSystemAppearanceParser.ParseAppleInterfaceStyle("The domain/default pair does not exist");

        Assert.False(dark.IsLightTheme);
        Assert.Equal("深色主题", dark.DisplayName);
        Assert.True(light.IsLightTheme);
        Assert.Equal("浅色主题", missing.DisplayName);
    }

    [Fact]
    public async Task MacOS_appearance_probe_invokes_defaults_command()
    {
        MacOSAppearanceCommandRequest? captured = null;
        var probe = new MacOSSystemAppearanceProbe((request, _) =>
        {
            captured = request;
            return Task.FromResult(new MacOSAppearanceCommandResult(0, "Dark\n", string.Empty));
        });

        var snapshot = await probe.DetectAsync();

        Assert.NotNull(captured);
        Assert.Equal("/usr/bin/defaults", captured.ExecutablePath);
        Assert.Equal(["read", "-g", "AppleInterfaceStyle"], captured.Arguments);
        Assert.False(snapshot.IsLightTheme);
        Assert.Equal("深色主题", snapshot.DisplayName);
    }

    [Fact]
    public async Task MacOS_appearance_probe_treats_missing_default_as_light_theme()
    {
        var probe = new MacOSSystemAppearanceProbe((_, _) =>
            Task.FromResult(new MacOSAppearanceCommandResult(1, string.Empty, "The domain/default pair does not exist")));

        var snapshot = await probe.DetectAsync();

        Assert.True(snapshot.IsLightTheme);
        Assert.Equal("浅色主题", snapshot.DisplayName);
    }

    [Fact]
    public async Task MacOS_appearance_probe_falls_back_to_light_on_runner_failure()
    {
        var probe = new MacOSSystemAppearanceProbe((_, _) =>
            throw new InvalidOperationException("defaults unavailable"));

        var snapshot = await probe.DetectAsync();

        Assert.True(snapshot.IsLightTheme);
        Assert.False(snapshot.IsHighContrast);
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically_and_recovers_from_bad_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Appearance", "AutoTheme", "SystemFollow", "settings.json");
        var store = new FileSystemFollowSettingsStore(path);
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.DarkThemeMapping = PortableThemeType.MinimalBlack;
        settings.ExclusionPeriods =
        [
            new PortableSystemFollowExclusionPeriod
            {
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(2, 0, 0),
                Days = PortableWeekday.All,
                Description = "quiet"
            }
        ];

        await store.SaveAsync(settings);
        var reloaded = await new FileSystemFollowSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.Enabled);
        Assert.Equal(PortableThemeType.MinimalBlack, reloaded.DarkThemeMapping);
        Assert.Single(reloaded.ExclusionPeriods);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.False((await store.LoadAsync()).Enabled);
    }

    [Fact]
    public void Theme_change_history_keeps_latest_records_first_and_caps_at_twenty()
    {
        var history = new PortableSystemThemeChangeHistory();
        var start = new DateTime(2026, 5, 10, 9, 0, 0);

        for (var i = 0; i < 25; i++)
        {
            history.AddChangeRecord(
                $"from-{i}",
                $"to-{i}",
                TimeSpan.FromMilliseconds(i),
                start.AddMinutes(i));
        }

        var records = history.GetChangeHistory();

        Assert.Equal(20, records.Count);
        Assert.Equal("to-24", records[0].ToTheme);
        Assert.Equal("to-5", records[^1].ToTheme);
        Assert.DoesNotContain(records, record => record.ToTheme == "to-4");
    }

    [Fact]
    public void Theme_change_record_display_text_matches_original_format()
    {
        var record = new PortableThemeChangeRecord
        {
            Timestamp = new DateTime(2026, 5, 10, 9, 8, 7),
            FromTheme = "浅色主题",
            ToTheme = "深色主题",
            Duration = TimeSpan.FromMilliseconds(12.3),
            Details = "浅色主题 -> 深色主题"
        };

        Assert.Equal("09:08:07 - 浅色主题 -> 深色主题 (耗时: 12ms)", record.DisplayText);
    }

    [Fact]
    public void Theme_change_history_returns_defensive_copies()
    {
        var history = new PortableSystemThemeChangeHistory();
        history.AddChangeRecord("浅色主题", "深色主题", TimeSpan.FromMilliseconds(30), DateTime.Now);

        var records = history.GetChangeHistory();
        records[0].ToTheme = "mutated";

        Assert.Equal("深色主题", history.GetChangeHistory()[0].ToTheme);
    }

    [Theory]
    [InlineData("Visual Studio Code", "工作中")]
    [InlineData("Microsoft Word", "工作中")]
    [InlineData("spotify", "娱乐中")]
    [InlineData("VLC", "娱乐中")]
    [InlineData("Zoom", "演示中")]
    [InlineData("Microsoft Teams", "演示中")]
    [InlineData("Safari", "默认")]
    public void Application_scene_classifier_maps_common_running_apps(string processName, string expected)
    {
        Assert.Equal(expected, PortableSystemFollowApplicationSceneClassifier.Classify([processName]));
    }

    [Fact]
    public void Application_scene_classifier_preserves_original_work_priority_over_presentation()
    {
        Assert.Equal("工作中", PortableSystemFollowApplicationSceneClassifier.Classify(["POWERPNT", "Zoom"]));
    }

    [Fact]
    public void Application_scene_classifier_accepts_custom_rules_before_builtin_fallback()
    {
        var scene = PortableSystemFollowApplicationSceneClassifier.Classify(
            ["Scrivener"],
            [
                new PortableApplicationSceneRule
                {
                    SceneName = "写作中",
                    ProcessKeywords = ["scrivener"],
                    IgnoreCase = true
                }
            ]);

        Assert.Equal("写作中", scene);
    }

    [Fact]
    public void MacOS_running_application_parser_extracts_distinct_app_names()
    {
        const string output = """
        Finder
        Safari
        Visual Studio Code
        Safari
        
        """;

        var apps = MacOSRunningApplicationParser.ParseApplicationNames(output);

        Assert.Equal(["Finder", "Safari", "Visual Studio Code"], apps);
    }

    [Fact]
    public async Task MacOS_running_application_probe_uses_osascript_boundary_and_falls_back_to_empty()
    {
        MacOSRunningApplicationCommandRequest? captured = null;
        var probe = new MacOSRunningApplicationProbe((request, _) =>
        {
            captured = request;
            return Task.FromResult(new MacOSRunningApplicationCommandResult(0, "Finder\nSafari\n", string.Empty));
        });

        var apps = await probe.GetRunningApplicationsAsync();

        Assert.NotNull(captured);
        Assert.Equal("/usr/bin/osascript", captured.ExecutablePath);
        Assert.Contains("System Events", captured.Script);
        Assert.Equal(["Finder", "Safari"], apps);

        var failingProbe = new MacOSRunningApplicationProbe((_, _) =>
            throw new InvalidOperationException("osascript unavailable"));

        Assert.Empty(await failingProbe.GetRunningApplicationsAsync());
    }
}
