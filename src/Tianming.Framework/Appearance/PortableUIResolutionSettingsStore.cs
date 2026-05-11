using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public enum PortablePresetResolution
{
    HD,
    FullHD,
    QHD,
    Custom
}

public enum PortableUIScaleLevel
{
    Scale100 = 100,
    Scale125 = 125,
    Scale150 = 150,
    Scale200 = 200
}

public sealed class PortableUIResolutionSettings
{
    [JsonPropertyName("WindowWidth")] public int WindowWidth { get; set; } = 1920;

    [JsonPropertyName("WindowHeight")] public int WindowHeight { get; set; } = 1080;

    [JsonPropertyName("UsePreset")] public bool UsePreset { get; set; } = true;

    [JsonPropertyName("Preset")] public PortablePresetResolution Preset { get; set; } = PortablePresetResolution.FullHD;

    [JsonPropertyName("ScalePercent")] public int ScalePercent { get; set; } = 100;

    [JsonPropertyName("MinWidth")] public int MinWidth { get; set; } = 800;

    [JsonPropertyName("MinHeight")] public int MinHeight { get; set; } = 600;

    [JsonPropertyName("MaxWidth")] public int MaxWidth { get; set; }

    [JsonPropertyName("MaxHeight")] public int MaxHeight { get; set; }

    public static PortableUIResolutionSettings CreateDefault()
    {
        return new PortableUIResolutionSettings
        {
            WindowWidth = 1920,
            WindowHeight = 1080,
            UsePreset = true,
            Preset = PortablePresetResolution.FullHD,
            ScalePercent = 100,
            MinWidth = 800,
            MinHeight = 600,
            MaxWidth = 0,
            MaxHeight = 0
        };
    }

    public static (int width, int height) GetPresetResolution(PortablePresetResolution preset)
    {
        return preset switch
        {
            PortablePresetResolution.HD => (1280, 720),
            PortablePresetResolution.FullHD => (1920, 1080),
            PortablePresetResolution.QHD => (2560, 1440),
            PortablePresetResolution.Custom => (1920, 1080),
            _ => (1920, 1080)
        };
    }

    public static string GetPresetDisplayName(PortablePresetResolution preset)
    {
        return preset switch
        {
            PortablePresetResolution.HD => "720p (1280×720)",
            PortablePresetResolution.FullHD => "1080p (1920×1080)",
            PortablePresetResolution.QHD => "1440p (2560×1440)",
            PortablePresetResolution.Custom => "自定义",
            _ => "未知"
        };
    }

    public static string GetScaleDisplayName(PortableUIScaleLevel scale)
    {
        return scale switch
        {
            PortableUIScaleLevel.Scale100 => "100% (标准)",
            PortableUIScaleLevel.Scale125 => "125% (稍大)",
            PortableUIScaleLevel.Scale150 => "150% (较大)",
            PortableUIScaleLevel.Scale200 => "200% (很大)",
            _ => "未知"
        };
    }

    public bool ValidateSize(int width, int height)
    {
        if (width < MinWidth || height < MinHeight)
        {
            return false;
        }

        if (MaxWidth > 0 && width > MaxWidth)
        {
            return false;
        }

        if (MaxHeight > 0 && height > MaxHeight)
        {
            return false;
        }

        return true;
    }

    public PortableUIResolutionSettings Clone()
    {
        return new PortableUIResolutionSettings
        {
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            UsePreset = UsePreset,
            Preset = Preset,
            ScalePercent = ScalePercent,
            MinWidth = MinWidth,
            MinHeight = MinHeight,
            MaxWidth = MaxWidth,
            MaxHeight = MaxHeight
        };
    }
}

public sealed class FileUIResolutionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileUIResolutionSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("UI resolution settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableUIResolutionSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableUIResolutionSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableUIResolutionSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableUIResolutionSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableUIResolutionSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableUIResolutionSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableUIResolutionSettings settings,
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
