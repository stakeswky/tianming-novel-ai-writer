using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public enum PortableLoadingAnimationType
{
    Spinner,
    Dots,
    Bars,
    Pulse,
    Ring,
    Wave,
    Progress,
    Skeleton,
    Custom1,
    Custom2
}

public enum PortableLoadingPosition
{
    Center,
    Top,
    Bottom,
    TopRight,
    BottomRight
}

public enum PortableOverlayMode
{
    None,
    Transparent,
    Blur,
    Full
}

public sealed class PortableLoadingAnimationSettings
{
    [JsonPropertyName("AnimationType")] public PortableLoadingAnimationType AnimationType { get; set; } = PortableLoadingAnimationType.Spinner;

    [JsonPropertyName("Position")] public PortableLoadingPosition Position { get; set; } = PortableLoadingPosition.Center;

    [JsonPropertyName("Overlay")] public PortableOverlayMode Overlay { get; set; } = PortableOverlayMode.Transparent;

    [JsonPropertyName("AnimationSpeed")] public int AnimationSpeed { get; set; } = 100;

    [JsonPropertyName("Size")] public int Size { get; set; } = 48;

    [JsonPropertyName("PrimaryColor")] public string PrimaryColor { get; set; } = "#3B82F6";

    [JsonPropertyName("SecondaryColor")] public string SecondaryColor { get; set; } = "#60A5FA";

    [JsonPropertyName("Opacity")] public double Opacity { get; set; } = 0.9;

    [JsonPropertyName("ShowText")] public bool ShowText { get; set; } = true;

    [JsonPropertyName("LoadingText")] public string LoadingText { get; set; } = "加载中...";

    [JsonPropertyName("TextSize")] public int TextSize { get; set; } = 14;

    [JsonPropertyName("TextColor")] public string TextColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("ShowPercentage")] public bool ShowPercentage { get; set; }

    [JsonPropertyName("OverlayOpacity")] public double OverlayOpacity { get; set; } = 0.5;

    [JsonPropertyName("OverlayColor")] public string OverlayColor { get; set; } = "#000000";

    [JsonPropertyName("BlurRadius")] public int BlurRadius { get; set; } = 8;

    [JsonPropertyName("MinDisplayTime")] public int MinDisplayTime { get; set; } = 300;

    [JsonPropertyName("DelayTime")] public int DelayTime { get; set; } = 200;

    [JsonPropertyName("CancelOnClick")] public bool CancelOnClick { get; set; }

    [JsonPropertyName("EnableSound")] public bool EnableSound { get; set; }

    [JsonPropertyName("SoundPath")] public string SoundPath { get; set; } = string.Empty;

    public static PortableLoadingAnimationSettings CreateDefault()
    {
        return new PortableLoadingAnimationSettings
        {
            AnimationType = PortableLoadingAnimationType.Spinner,
            Position = PortableLoadingPosition.Center,
            Overlay = PortableOverlayMode.Transparent,
            AnimationSpeed = 100,
            Size = 48,
            PrimaryColor = "#3B82F6",
            SecondaryColor = "#60A5FA",
            Opacity = 0.9,
            ShowText = true,
            LoadingText = "加载中...",
            TextSize = 14,
            TextColor = "#FFFFFF",
            ShowPercentage = false,
            OverlayOpacity = 0.5,
            OverlayColor = "#000000",
            BlurRadius = 8,
            MinDisplayTime = 300,
            DelayTime = 200,
            CancelOnClick = false,
            EnableSound = false,
            SoundPath = string.Empty
        };
    }

    public PortableLoadingAnimationSettings Clone()
    {
        return new PortableLoadingAnimationSettings
        {
            AnimationType = AnimationType,
            Position = Position,
            Overlay = Overlay,
            AnimationSpeed = AnimationSpeed,
            Size = Size,
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            Opacity = Opacity,
            ShowText = ShowText,
            LoadingText = LoadingText,
            TextSize = TextSize,
            TextColor = TextColor,
            ShowPercentage = ShowPercentage,
            OverlayOpacity = OverlayOpacity,
            OverlayColor = OverlayColor,
            BlurRadius = BlurRadius,
            MinDisplayTime = MinDisplayTime,
            DelayTime = DelayTime,
            CancelOnClick = CancelOnClick,
            EnableSound = EnableSound,
            SoundPath = SoundPath
        };
    }
}

public sealed class FileLoadingAnimationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileLoadingAnimationSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Loading animation settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableLoadingAnimationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableLoadingAnimationSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableLoadingAnimationSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableLoadingAnimationSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableLoadingAnimationSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableLoadingAnimationSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableLoadingAnimationSettings settings,
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
