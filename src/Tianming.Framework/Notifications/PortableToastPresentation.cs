using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Notifications;

public enum PortableToastNotificationPriority
{
    Low,
    Medium,
    High
}

public enum PortableToastAnimationType
{
    FadeInOut,
    SlideIn,
    Bounce,
    Scale
}

public enum PortableToastScreenPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
    Center
}

public enum PortableToastStackDirection
{
    Up,
    Down
}

public enum PortableToastEasingFunction
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

public sealed class PortableToastStyleData
{
    [JsonPropertyName("CornerRadius")]
    public double CornerRadius { get; set; } = 8;

    [JsonPropertyName("ShadowIntensity")]
    public double ShadowIntensity { get; set; } = 12;

    [JsonPropertyName("BorderThickness")]
    public double BorderThickness { get; set; } = 1;

    [JsonPropertyName("BackgroundOpacity")]
    public double BackgroundOpacity { get; set; } = 95;

    [JsonPropertyName("AnimationType")]
    public PortableToastAnimationType AnimationType { get; set; } = PortableToastAnimationType.FadeInOut;

    [JsonPropertyName("AnimationDuration")]
    public int AnimationDuration { get; set; } = 300;

    [JsonPropertyName("EasingFunction")]
    public PortableToastEasingFunction EasingFunction { get; set; } = PortableToastEasingFunction.Linear;

    [JsonPropertyName("ScreenPosition")]
    public PortableToastScreenPosition ScreenPosition { get; set; } = PortableToastScreenPosition.BottomRight;

    [JsonPropertyName("NotificationWidth")]
    public double NotificationWidth { get; set; } = 300;

    [JsonPropertyName("NotificationHeight")]
    public double NotificationHeight { get; set; } = 80;

    [JsonPropertyName("NotificationSpacing")]
    public double NotificationSpacing { get; set; } = 5;

    [JsonPropertyName("StackDirection")]
    public PortableToastStackDirection StackDirection { get; set; } = PortableToastStackDirection.Down;

    public static PortableToastStyleData CreateDefault()
    {
        return new PortableToastStyleData();
    }

    public PortableToastStyleData Clone()
    {
        return new PortableToastStyleData
        {
            CornerRadius = CornerRadius,
            ShadowIntensity = ShadowIntensity,
            BorderThickness = BorderThickness,
            BackgroundOpacity = BackgroundOpacity,
            AnimationType = AnimationType,
            AnimationDuration = AnimationDuration,
            EasingFunction = EasingFunction,
            ScreenPosition = ScreenPosition,
            NotificationWidth = NotificationWidth,
            NotificationHeight = NotificationHeight,
            NotificationSpacing = NotificationSpacing,
            StackDirection = StackDirection
        };
    }

    public void CopyFrom(PortableToastStyleData source)
    {
        ArgumentNullException.ThrowIfNull(source);

        CornerRadius = source.CornerRadius;
        ShadowIntensity = source.ShadowIntensity;
        BorderThickness = source.BorderThickness;
        BackgroundOpacity = source.BackgroundOpacity;
        AnimationType = source.AnimationType;
        AnimationDuration = source.AnimationDuration;
        EasingFunction = source.EasingFunction;
        ScreenPosition = source.ScreenPosition;
        NotificationWidth = source.NotificationWidth;
        NotificationHeight = source.NotificationHeight;
        NotificationSpacing = source.NotificationSpacing;
        StackDirection = source.StackDirection;
    }
}

public sealed class PortableToastTypeStyle
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("Color")]
    public string Color { get; set; } = "#3B82F6";

    [JsonPropertyName("Priority")]
    public PortableToastNotificationPriority Priority { get; set; } = PortableToastNotificationPriority.Medium;

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("GroupName")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("IsSelected")]
    public bool IsSelected { get; set; }

    public PortableToastTypeStyle Clone()
    {
        return new PortableToastTypeStyle
        {
            Id = Id,
            Name = Name,
            Icon = Icon,
            Color = Color,
            Priority = Priority,
            IsEnabled = IsEnabled,
            GroupName = GroupName,
            Description = Description,
            IsSelected = IsSelected
        };
    }
}

public sealed class PortableToastTypeSettingsData
{
    [JsonPropertyName("Types")]
    public List<PortableToastTypeStyle> Types { get; set; } = [];

    [JsonPropertyName("LastModified")]
    public DateTime LastModified { get; set; } = DateTime.Now;

    public static PortableToastTypeSettingsData CreateDefault(Func<DateTime>? clock = null)
    {
        return new PortableToastTypeSettingsData
        {
            Types = PortableToastPresenter.CreateDefaultTypeStyles().Select(type => type.Clone()).ToList(),
            LastModified = clock?.Invoke() ?? DateTime.Now
        };
    }

    public PortableToastTypeSettingsData Clone()
    {
        return new PortableToastTypeSettingsData
        {
            Types = Types.Select(type => type.Clone()).ToList(),
            LastModified = LastModified
        };
    }
}

public sealed class PortableToastRequest
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public PortableNotificationType Type { get; init; } = PortableNotificationType.Info;

    public int DurationMilliseconds { get; init; } = 3000;
}

public sealed class PortableToastPresentationModel
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool HasMessage { get; init; }

    public PortableNotificationType Type { get; init; } = PortableNotificationType.Info;

    public string Icon { get; init; } = string.Empty;

    public string BackgroundColor { get; init; } = "#3B82F6";

    public double BackgroundOpacity { get; init; }

    public string TitleColor { get; init; } = "#FFFFFF";

    public string MessageColor { get; init; } = "#FFFFFF";

    public double MessageOpacity { get; init; } = 0.9;

    public double CornerRadius { get; init; }

    public double ShadowIntensity { get; init; }

    public double BorderThickness { get; init; }

    public PortableToastAnimationType AnimationType { get; init; }

    public int AnimationDuration { get; init; }

    public PortableToastEasingFunction EasingFunction { get; init; }

    public double Width { get; init; }

    public double MinHeight { get; init; }

    public double MaxHeight { get; init; }

    public int DurationMilliseconds { get; init; }

    public bool AutoDismiss { get; init; }
}

public sealed class PortableToastPresenter
{
    private static readonly Dictionary<PortableNotificationType, (string Id, string Icon, string Color)> FallbackTypeStyles = new()
    {
        [PortableNotificationType.Success] = ("success", "✅", "#10B981"),
        [PortableNotificationType.Warning] = ("warning", "⚠️", "#F59E0B"),
        [PortableNotificationType.Error] = ("error", "❌", "#EF4444"),
        [PortableNotificationType.Info] = ("info", "ℹ️", "#3B82F6")
    };

    private readonly PortableToastStyleData _style;
    private readonly IReadOnlyList<PortableToastTypeStyle> _typeStyles;

    public PortableToastPresenter(
        PortableToastStyleData style,
        IEnumerable<PortableToastTypeStyle>? typeStyles = null)
    {
        _style = style;
        _typeStyles = typeStyles?.ToArray() ?? Array.Empty<PortableToastTypeStyle>();
    }

    public static PortableToastPresenter CreateDefault()
    {
        return new PortableToastPresenter(
            PortableToastStyleData.CreateDefault(),
            CreateDefaultTypeStyles());
    }

    public PortableToastPresentationModel Build(PortableToastRequest request)
    {
        var typeStyle = ResolveTypeStyle(request.Type);
        var message = string.IsNullOrWhiteSpace(request.Message) ? string.Empty : request.Message;

        return new PortableToastPresentationModel
        {
            Title = request.Title,
            Message = message,
            HasMessage = message.Length > 0,
            Type = request.Type,
            Icon = typeStyle.Icon,
            BackgroundColor = typeStyle.Color,
            BackgroundOpacity = _style.BackgroundOpacity / 100.0,
            CornerRadius = _style.CornerRadius,
            ShadowIntensity = _style.ShadowIntensity,
            BorderThickness = _style.BorderThickness,
            AnimationType = _style.AnimationType,
            AnimationDuration = _style.AnimationDuration,
            EasingFunction = _style.EasingFunction,
            Width = _style.NotificationWidth,
            MinHeight = _style.NotificationHeight,
            MaxHeight = Math.Max(_style.NotificationHeight * 3, 200),
            DurationMilliseconds = request.DurationMilliseconds,
            AutoDismiss = request.DurationMilliseconds > 0
        };
    }

    public static IReadOnlyList<PortableToastTypeStyle> CreateDefaultTypeStyles()
    {
        return
        [
            new PortableToastTypeStyle
            {
                Id = "system",
                Name = "系统通知",
                Icon = "⚙️",
                Color = "#2196F3",
                Priority = PortableToastNotificationPriority.Medium,
                IsEnabled = true,
                GroupName = "系统",
                Description = "系统级别的通知消息"
            },
            new PortableToastTypeStyle
            {
                Id = "application",
                Name = "应用通知",
                Icon = "📱",
                Color = "#16A34A",
                Priority = PortableToastNotificationPriority.Medium,
                IsEnabled = true,
                GroupName = "应用",
                Description = "应用程序的通知消息"
            },
            new PortableToastTypeStyle
            {
                Id = "warning",
                Name = "警告",
                Icon = "⚠️",
                Color = "#FF9800",
                Priority = PortableToastNotificationPriority.High,
                IsEnabled = true,
                GroupName = "状态",
                Description = "警告级别的通知"
            },
            new PortableToastTypeStyle
            {
                Id = "error",
                Name = "错误",
                Icon = "❌",
                Color = "#F44336",
                Priority = PortableToastNotificationPriority.High,
                IsEnabled = true,
                GroupName = "状态",
                Description = "错误级别的通知"
            },
            new PortableToastTypeStyle
            {
                Id = "success",
                Name = "成功",
                Icon = "✅",
                Color = "#16A34A",
                Priority = PortableToastNotificationPriority.Medium,
                IsEnabled = true,
                GroupName = "状态",
                Description = "成功提示通知"
            },
            new PortableToastTypeStyle
            {
                Id = "info",
                Name = "信息",
                Icon = "ℹ️",
                Color = "#9C27B0",
                Priority = PortableToastNotificationPriority.Low,
                IsEnabled = true,
                GroupName = "状态",
                Description = "普通信息通知"
            }
        ];
    }

    private (string Icon, string Color) ResolveTypeStyle(PortableNotificationType type)
    {
        var fallback = FallbackTypeStyles[type];
        var configured = _typeStyles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, fallback.Id, StringComparison.OrdinalIgnoreCase));

        if (configured is { IsEnabled: true } &&
            !string.IsNullOrWhiteSpace(configured.Icon) &&
            !string.IsNullOrWhiteSpace(configured.Color))
        {
            return (configured.Icon, configured.Color);
        }

        return (fallback.Icon, fallback.Color);
    }
}

public sealed class PortableToastStyleController
{
    private readonly PortableToastStyleData _settings;

    public PortableToastStyleController(PortableToastStyleData settings)
    {
        _settings = settings;
    }

    public void ApplyPreset(string presetName)
    {
        switch (presetName.ToLowerInvariant())
        {
            case "simple":
                _settings.CornerRadius = 4;
                _settings.ShadowIntensity = 5;
                _settings.BorderThickness = 1;
                _settings.BackgroundOpacity = 100;
                _settings.AnimationType = PortableToastAnimationType.FadeInOut;
                _settings.AnimationDuration = 200;
                break;
            case "standard":
                _settings.CopyFrom(PortableToastStyleData.CreateDefault());
                break;
            case "fancy":
                _settings.CornerRadius = 16;
                _settings.ShadowIntensity = 25;
                _settings.BorderThickness = 0;
                _settings.BackgroundOpacity = 90;
                _settings.AnimationType = PortableToastAnimationType.Bounce;
                _settings.AnimationDuration = 500;
                break;
        }
    }
}

public sealed class FileToastStyleSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileToastStyleSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Toast style settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableToastStyleData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableToastStyleData.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableToastStyleData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableToastStyleData.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableToastStyleData.CreateDefault();
        }
        catch (IOException)
        {
            return PortableToastStyleData.CreateDefault();
        }
    }

    public async Task SaveAsync(PortableToastStyleData settings, CancellationToken cancellationToken = default)
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

public sealed class FileToastTypeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;

    public FileToastTypeSettingsStore(string filePath, Func<DateTime>? clock = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Toast type settings file path is required.", nameof(filePath))
            : filePath;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableToastTypeSettingsData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableToastTypeSettingsData.CreateDefault(_clock);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<PortableToastTypeSettingsData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            return settings is { Types.Count: > 0 }
                ? settings
                : PortableToastTypeSettingsData.CreateDefault(_clock);
        }
        catch (JsonException)
        {
            return PortableToastTypeSettingsData.CreateDefault(_clock);
        }
        catch (IOException)
        {
            return PortableToastTypeSettingsData.CreateDefault(_clock);
        }
    }

    public async Task SaveAsync(PortableToastTypeSettingsData settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = settings.Clone();
        data.LastModified = _clock();

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public readonly record struct PortableToastWorkArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

public readonly record struct PortableToastSize(double Width, double Height);

public readonly record struct PortableToastPlacement(double Left, double Top);

public static class PortableToastPlacementCalculator
{
    public static PortableToastPlacement Calculate(
        PortableToastStyleData style,
        PortableToastWorkArea workArea,
        PortableToastSize size,
        IReadOnlyCollection<double> previousToastHeights)
    {
        const double edgeMargin = 10;
        const double topMargin = 10;
        var offset = topMargin + previousToastHeights.Sum() + (previousToastHeights.Count * style.NotificationSpacing);

        return style.ScreenPosition switch
        {
            PortableToastScreenPosition.TopRight => new PortableToastPlacement(
                workArea.Right - size.Width - edgeMargin,
                workArea.Top + offset),
            PortableToastScreenPosition.TopLeft => new PortableToastPlacement(
                workArea.Left + edgeMargin,
                workArea.Top + offset),
            PortableToastScreenPosition.BottomRight => new PortableToastPlacement(
                workArea.Right - size.Width - edgeMargin,
                workArea.Bottom - offset - size.Height),
            PortableToastScreenPosition.BottomLeft => new PortableToastPlacement(
                workArea.Left + edgeMargin,
                workArea.Bottom - offset - size.Height),
            PortableToastScreenPosition.Center => new PortableToastPlacement(
                workArea.Left + ((workArea.Width - size.Width) / 2),
                workArea.Top + (workArea.Height / 2) + (previousToastHeights.Count * (size.Height + style.NotificationSpacing)) - (size.Height / 2)),
            _ => new PortableToastPlacement(workArea.Right - size.Width - edgeMargin, workArea.Top + offset)
        };
    }
}
