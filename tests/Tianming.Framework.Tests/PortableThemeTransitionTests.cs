using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeTransitionTests
{
    [Fact]
    public void Default_settings_match_original_theme_transition_defaults()
    {
        var settings = PortableThemeTransitionSettings.CreateDefault();

        Assert.Equal(PortableTransitionEffect.Rotate, settings.Effect);
        Assert.Equal([PortableTransitionEffect.Rotate], settings.CombinedEffects);
        Assert.Equal(PortableEasingFunctionType.Linear, settings.EasingType);
        Assert.Equal(2000, settings.Duration);
        Assert.Equal(60, settings.TargetFPS);
        Assert.Equal(1.0, settings.IntensityMultiplier);
        Assert.Equal(PortableTransitionPreset.Fast, settings.Preset);
    }

    [Fact]
    public void Clone_returns_independent_copy_with_combined_effects()
    {
        var settings = PortableThemeTransitionSettings.CreateDefault();
        settings.CombinedEffects = [PortableTransitionEffect.Rotate, PortableTransitionEffect.Blur];
        settings.EasingType = PortableEasingFunctionType.EaseInOutCubic;
        settings.Duration = 900;

        var clone = settings.Clone();
        clone.CombinedEffects.Add(PortableTransitionEffect.SlideLeft);

        Assert.Equal([PortableTransitionEffect.Rotate, PortableTransitionEffect.Blur], settings.CombinedEffects);
        Assert.Equal([PortableTransitionEffect.Rotate, PortableTransitionEffect.Blur, PortableTransitionEffect.SlideLeft], clone.CombinedEffects);
        Assert.Equal(PortableEasingFunctionType.EaseInOutCubic, clone.EasingType);
        Assert.Equal(900, clone.Duration);
    }

    [Theory]
    [InlineData(PortableEasingFunctionType.Linear, -0.4, 0.0)]
    [InlineData(PortableEasingFunctionType.Linear, 1.4, 1.0)]
    [InlineData(PortableEasingFunctionType.EaseInQuad, 0.5, 0.25)]
    [InlineData(PortableEasingFunctionType.EaseOutQuad, 0.5, 0.75)]
    [InlineData(PortableEasingFunctionType.EaseInOutQuad, 0.25, 0.125)]
    [InlineData(PortableEasingFunctionType.EaseInCubic, 0.5, 0.125)]
    [InlineData(PortableEasingFunctionType.EaseOutCubic, 0.5, 0.875)]
    [InlineData(PortableEasingFunctionType.EaseInOutCubic, 0.75, 0.9375)]
    public void Easing_functions_match_original_math(
        PortableEasingFunctionType type,
        double t,
        double expected)
    {
        Assert.Equal(expected, PortableEasingFunctions.Apply(type, t), precision: 6);
    }

    [Fact]
    public void GetCurvePoints_returns_requested_curve_with_endpoints()
    {
        var points = PortableEasingFunctions.GetCurvePoints(PortableEasingFunctionType.EaseInQuad, 5);

        Assert.Equal(5, points.Length);
        Assert.Equal((0.0, 0.0), points[0]);
        Assert.Equal((0.5, 0.25), points[2]);
        Assert.Equal((1.0, 1.0), points[^1]);
    }

    [Fact]
    public void Presets_match_original_view_model_values_and_clamp_cool_to_detected_fps()
    {
        var fast = PortableThemeTransitionPresetService.ApplyPreset(
            PortableThemeTransitionSettings.CreateDefault(),
            PortableTransitionPreset.Fast,
            detectedMonitorFps: 60);
        var fancy = PortableThemeTransitionPresetService.ApplyPreset(
            PortableThemeTransitionSettings.CreateDefault(),
            PortableTransitionPreset.Fancy,
            detectedMonitorFps: 60);
        var coolOnSixtyHz = PortableThemeTransitionPresetService.ApplyPreset(
            PortableThemeTransitionSettings.CreateDefault(),
            PortableTransitionPreset.Cool,
            detectedMonitorFps: 60);
        var coolOnHighRefresh = PortableThemeTransitionPresetService.ApplyPreset(
            PortableThemeTransitionSettings.CreateDefault(),
            PortableTransitionPreset.Cool,
            detectedMonitorFps: 144);

        Assert.Equal(300, fast.Duration);
        Assert.Equal(60, fast.TargetFPS);
        Assert.Equal(PortableTransitionEffect.Blur, fast.Effect);
        Assert.Equal(PortableTransitionPreset.Fast, fast.Preset);

        Assert.Equal(1000, fancy.Duration);
        Assert.Equal(PortableTransitionEffect.Rotate, fancy.Effect);

        Assert.Equal(1200, coolOnSixtyHz.Duration);
        Assert.Equal(60, coolOnSixtyHz.TargetFPS);
        Assert.Equal(120, coolOnHighRefresh.TargetFPS);
    }

    [Fact]
    public void Settings_normalizer_clamps_values_and_filters_combined_effects()
    {
        var settings = new PortableThemeTransitionSettings
        {
            Effect = PortableTransitionEffect.SlideRight,
            CombinedEffects =
            [
                PortableTransitionEffect.None,
                PortableTransitionEffect.Blur,
                PortableTransitionEffect.Blur,
                PortableTransitionEffect.SlideDown
            ],
            Duration = 100,
            TargetFPS = 240,
            IntensityMultiplier = 4
        };

        var normalized = PortableThemeTransitionPresetService.Normalize(settings, detectedMonitorFps: 75);

        Assert.Equal(300, normalized.Duration);
        Assert.Equal(75, normalized.TargetFPS);
        Assert.Equal(2.0, normalized.IntensityMultiplier);
        Assert.Equal([PortableTransitionEffect.Blur, PortableTransitionEffect.SlideDown], normalized.CombinedEffects);
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically_and_recovers_from_bad_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Appearance", "Animation", "ThemeTransition", "settings.json");
        var store = new FileThemeTransitionSettingsStore(path);
        var settings = PortableThemeTransitionSettings.CreateDefault();
        settings.Effect = PortableTransitionEffect.FlipVertical;
        settings.CombinedEffects = [PortableTransitionEffect.Rotate, PortableTransitionEffect.FlipVertical];
        settings.EasingType = PortableEasingFunctionType.EaseOutBounce;
        settings.Duration = 1250;
        settings.TargetFPS = 90;

        await store.SaveAsync(settings);
        var reloaded = await new FileThemeTransitionSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(PortableTransitionEffect.FlipVertical, reloaded.Effect);
        Assert.Equal([PortableTransitionEffect.Rotate, PortableTransitionEffect.FlipVertical], reloaded.CombinedEffects);
        Assert.Equal(PortableEasingFunctionType.EaseOutBounce, reloaded.EasingType);
        Assert.Equal(1250, reloaded.Duration);
        Assert.Equal(90, reloaded.TargetFPS);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.Equal(PortableTransitionEffect.Rotate, (await store.LoadAsync()).Effect);
    }
}
