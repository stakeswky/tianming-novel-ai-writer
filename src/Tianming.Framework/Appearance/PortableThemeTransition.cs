using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public enum PortableEasingFunctionType
{
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInElastic,
    EaseOutElastic,
    EaseInBounce,
    EaseOutBounce,
    EaseInOutBounce
}

public enum PortableTransitionEffect
{
    None,
    Rotate,
    Blur,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    FlipHorizontal,
    FlipVertical
}

public enum PortableTransitionPreset
{
    Fast,
    Smooth,
    Fancy,
    Simple,
    Dynamic,
    Cool,
    Custom
}

public sealed class PortableThemeTransitionSettings
{
    [JsonPropertyName("Effect")] public PortableTransitionEffect Effect { get; set; } = PortableTransitionEffect.Rotate;

    [JsonPropertyName("CombinedEffects")] public List<PortableTransitionEffect> CombinedEffects { get; set; } = [];

    [JsonPropertyName("EasingType")] public PortableEasingFunctionType EasingType { get; set; } = PortableEasingFunctionType.Linear;

    [JsonPropertyName("Duration")] public int Duration { get; set; } = 2000;

    [JsonPropertyName("TargetFPS")] public int TargetFPS { get; set; } = 60;

    [JsonPropertyName("IntensityMultiplier")] public double IntensityMultiplier { get; set; } = 1.0;

    [JsonPropertyName("Preset")] public PortableTransitionPreset Preset { get; set; } = PortableTransitionPreset.Fast;

    public static PortableThemeTransitionSettings CreateDefault()
    {
        return new PortableThemeTransitionSettings
        {
            Effect = PortableTransitionEffect.Rotate,
            CombinedEffects = [PortableTransitionEffect.Rotate],
            EasingType = PortableEasingFunctionType.Linear,
            Duration = 2000,
            TargetFPS = 60,
            IntensityMultiplier = 1.0,
            Preset = PortableTransitionPreset.Fast
        };
    }

    public PortableThemeTransitionSettings Clone()
    {
        return new PortableThemeTransitionSettings
        {
            Effect = Effect,
            CombinedEffects = [.. CombinedEffects],
            EasingType = EasingType,
            Duration = Duration,
            TargetFPS = TargetFPS,
            IntensityMultiplier = IntensityMultiplier,
            Preset = Preset
        };
    }
}

public static class PortableEasingFunctions
{
    public static double Apply(PortableEasingFunctionType type, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        return type switch
        {
            PortableEasingFunctionType.Linear => t,
            PortableEasingFunctionType.EaseInQuad => t * t,
            PortableEasingFunctionType.EaseOutQuad => t * (2 - t),
            PortableEasingFunctionType.EaseInOutQuad => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t,
            PortableEasingFunctionType.EaseInCubic => t * t * t,
            PortableEasingFunctionType.EaseOutCubic => EaseOutCubic(t),
            PortableEasingFunctionType.EaseInOutCubic => EaseInOutCubic(t),
            PortableEasingFunctionType.EaseInElastic => EaseInElastic(t),
            PortableEasingFunctionType.EaseOutElastic => EaseOutElastic(t),
            PortableEasingFunctionType.EaseInBounce => 1 - EaseOutBounce(1 - t),
            PortableEasingFunctionType.EaseOutBounce => EaseOutBounce(t),
            PortableEasingFunctionType.EaseInOutBounce => EaseInOutBounce(t),
            _ => t
        };
    }

    public static (double x, double y)[] GetCurvePoints(
        PortableEasingFunctionType type,
        int pointCount = 50)
    {
        if (pointCount <= 0)
        {
            return [];
        }

        if (pointCount == 1)
        {
            return [(0.0, Apply(type, 0.0))];
        }

        var points = new (double x, double y)[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var t = (double)i / (pointCount - 1);
            points[i] = (t, Apply(type, t));
        }

        return points;
    }

    private static double EaseOutCubic(double t)
    {
        var t1 = t - 1;
        return t1 * t1 * t1 + 1;
    }

    private static double EaseInOutCubic(double t)
    {
        return t < 0.5
            ? 4 * t * t * t
            : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
    }

    private static double EaseInElastic(double t)
    {
        if (t == 0)
        {
            return 0;
        }

        if (t == 1)
        {
            return 1;
        }

        const double p = 0.3;
        const double s = p / 4;
        var t1 = t - 1;

        return -(Math.Pow(2, 10 * t1) * Math.Sin((t1 - s) * (2 * Math.PI) / p));
    }

    private static double EaseOutElastic(double t)
    {
        if (t == 0)
        {
            return 0;
        }

        if (t == 1)
        {
            return 1;
        }

        const double p = 0.3;
        const double s = p / 4;

        return Math.Pow(2, -10 * t) * Math.Sin((t - s) * (2 * Math.PI) / p) + 1;
    }

    private static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (t < 1 / d1)
        {
            return n1 * t * t;
        }

        if (t < 2 / d1)
        {
            t -= 1.5 / d1;
            return n1 * t * t + 0.75;
        }

        if (t < 2.5 / d1)
        {
            t -= 2.25 / d1;
            return n1 * t * t + 0.9375;
        }

        t -= 2.625 / d1;
        return n1 * t * t + 0.984375;
    }

    private static double EaseInOutBounce(double t)
    {
        return t < 0.5
            ? (1 - EaseOutBounce(1 - t * 2)) * 0.5
            : EaseOutBounce(t * 2 - 1) * 0.5 + 0.5;
    }
}

public static class PortableThemeTransitionPresetService
{
    public static PortableThemeTransitionSettings ApplyPreset(
        PortableThemeTransitionSettings settings,
        PortableTransitionPreset preset,
        int detectedMonitorFps = 60)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var next = settings.Clone();

        switch (preset)
        {
            case PortableTransitionPreset.Fast:
                next.Duration = 300;
                next.TargetFPS = 60;
                next.Effect = PortableTransitionEffect.Blur;
                break;
            case PortableTransitionPreset.Smooth:
                next.Duration = 600;
                next.TargetFPS = 60;
                next.Effect = PortableTransitionEffect.Blur;
                break;
            case PortableTransitionPreset.Fancy:
                next.Duration = 1000;
                next.TargetFPS = 60;
                next.Effect = PortableTransitionEffect.Rotate;
                break;
            case PortableTransitionPreset.Simple:
                next.Duration = 400;
                next.TargetFPS = 60;
                next.Effect = PortableTransitionEffect.Blur;
                break;
            case PortableTransitionPreset.Dynamic:
                next.Duration = 800;
                next.TargetFPS = 60;
                next.Effect = PortableTransitionEffect.SlideLeft;
                break;
            case PortableTransitionPreset.Cool:
                next.Duration = 1200;
                next.TargetFPS = Math.Min(120, Math.Max(0, detectedMonitorFps));
                next.Effect = PortableTransitionEffect.Rotate;
                break;
            case PortableTransitionPreset.Custom:
            default:
                break;
        }

        next.Preset = preset;
        return Normalize(next, detectedMonitorFps);
    }

    public static PortableThemeTransitionSettings Normalize(
        PortableThemeTransitionSettings settings,
        int detectedMonitorFps = 60)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var maxFps = Math.Max(60, detectedMonitorFps);
        var normalized = settings.Clone();

        normalized.Duration = Math.Clamp(normalized.Duration, 300, 3000);
        normalized.TargetFPS = Math.Clamp(normalized.TargetFPS, 30, maxFps);
        normalized.IntensityMultiplier = Math.Clamp(normalized.IntensityMultiplier, 0.5, 2.0);
        normalized.CombinedEffects = normalized.CombinedEffects
            .Where(effect => effect != PortableTransitionEffect.None)
            .Distinct()
            .ToList();

        return normalized;
    }
}

public sealed class FileThemeTransitionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileThemeTransitionSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Theme transition settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableThemeTransitionSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableThemeTransitionSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableThemeTransitionSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableThemeTransitionSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableThemeTransitionSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableThemeTransitionSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableThemeTransitionSettings settings,
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
            await JsonSerializer.SerializeAsync(stream, settings.Clone(), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
