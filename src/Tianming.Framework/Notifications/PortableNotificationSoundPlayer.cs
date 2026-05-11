using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Notifications;

public enum PortableSystemSound
{
    Beep,
    Asterisk,
    Exclamation,
    Hand
}

public sealed class PortableSoundSchemeData
{
    [JsonPropertyName("ActiveSchemeId")] public string ActiveSchemeId { get; set; } = "default";

    [JsonPropertyName("EventSoundMappings")] public Dictionary<string, string> EventSoundMappings { get; set; } = new();

    [JsonPropertyName("CustomSoundFiles")] public List<string> CustomSoundFiles { get; set; } = new();

    public static PortableSoundSchemeData CreateDefault()
    {
        var settings = new PortableSoundSchemeData();
        PortableSoundSchemeController.ApplyDefaultMappings(settings.EventSoundMappings);
        return settings;
    }

    public PortableSoundSchemeData Clone()
    {
        return new PortableSoundSchemeData
        {
            ActiveSchemeId = ActiveSchemeId,
            EventSoundMappings = new Dictionary<string, string>(EventSoundMappings, StringComparer.Ordinal),
            CustomSoundFiles = [.. CustomSoundFiles]
        };
    }
}

public sealed class FileSoundSchemeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileSoundSchemeSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Sound scheme settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableSoundSchemeData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableSoundSchemeData.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableSoundSchemeData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableSoundSchemeData.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableSoundSchemeData.CreateDefault();
        }
        catch (IOException)
        {
            return PortableSoundSchemeData.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableSoundSchemeData settings,
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

public sealed class PortableSoundSchemeController
{
    private static readonly HashSet<string> BuiltInSchemeIds = new(StringComparer.Ordinal)
    {
        "default",
        "silent",
        "minimal",
        "rich"
    };

    public PortableSoundSchemeController(PortableSoundSchemeData settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public PortableSoundSchemeData Settings { get; }

    public bool SelectScheme(string schemeId)
    {
        if (!BuiltInSchemeIds.Contains(schemeId))
        {
            return false;
        }

        Settings.ActiveSchemeId = schemeId;
        ApplySchemeMappings(schemeId, Settings.EventSoundMappings);
        return true;
    }

    public static void ApplyDefaultMappings(IDictionary<string, string> mappings)
    {
        mappings["通知到达"] = "默认提示音";
        mappings["警告"] = "警告音";
        mappings["错误"] = "错误音";
        mappings["成功"] = "成功音";
        mappings["信息提示"] = "信息音";
    }

    private static void ApplySchemeMappings(string schemeId, IDictionary<string, string> mappings)
    {
        mappings.Clear();
        switch (schemeId)
        {
            case "silent":
                mappings["通知到达"] = "无";
                mappings["警告"] = "无";
                mappings["错误"] = "无";
                mappings["成功"] = "无";
                mappings["信息提示"] = "无";
                break;
            case "minimal":
                mappings["通知到达"] = "默认提示音";
                mappings["警告"] = "默认提示音";
                mappings["错误"] = "警告音";
                mappings["成功"] = "默认提示音";
                mappings["信息提示"] = "默认提示音";
                break;
            default:
                ApplyDefaultMappings(mappings);
                break;
        }
    }
}

public sealed class PortableVolumeAndDeviceData
{
    [JsonPropertyName("SystemVolume")] public double SystemVolume { get; set; } = 80;

    [JsonPropertyName("NotificationVolume")] public double NotificationVolume { get; set; } = 100;

    [JsonPropertyName("EffectVolume")] public double EffectVolume { get; set; } = 80;

    [JsonPropertyName("IsMuted")] public bool IsMuted { get; set; }

    [JsonPropertyName("BassLevel")] public double BassLevel { get; set; }

    [JsonPropertyName("MidBassLevel")] public double MidBassLevel { get; set; }

    [JsonPropertyName("MidLevel")] public double MidLevel { get; set; }

    [JsonPropertyName("MidTrebleLevel")] public double MidTrebleLevel { get; set; }

    [JsonPropertyName("TrebleLevel")] public double TrebleLevel { get; set; }

    [JsonPropertyName("EqualizerPreset")] public string EqualizerPreset { get; set; } = "默认";

    [JsonPropertyName("OutputDeviceId")] public string? OutputDeviceId { get; set; }

    [JsonPropertyName("InputDeviceId")] public string? InputDeviceId { get; set; }

    public static PortableVolumeAndDeviceData CreateDefault()
    {
        return new PortableVolumeAndDeviceData
        {
            SystemVolume = 80,
            NotificationVolume = 100,
            EffectVolume = 80,
            IsMuted = false,
            BassLevel = 0,
            MidBassLevel = 0,
            MidLevel = 0,
            MidTrebleLevel = 0,
            TrebleLevel = 0,
            EqualizerPreset = "默认",
            OutputDeviceId = null,
            InputDeviceId = null
        };
    }

    public PortableVolumeAndDeviceData Clone()
    {
        return new PortableVolumeAndDeviceData
        {
            SystemVolume = SystemVolume,
            NotificationVolume = NotificationVolume,
            EffectVolume = EffectVolume,
            IsMuted = IsMuted,
            BassLevel = BassLevel,
            MidBassLevel = MidBassLevel,
            MidLevel = MidLevel,
            MidTrebleLevel = MidTrebleLevel,
            TrebleLevel = TrebleLevel,
            EqualizerPreset = EqualizerPreset,
            OutputDeviceId = OutputDeviceId,
            InputDeviceId = InputDeviceId
        };
    }
}

public enum PortableVolumePreset
{
    Quiet,
    Standard,
    Loud
}

public sealed class PortableVolumeAndDeviceController
{
    private double _volumeBeforeMute;

    public PortableVolumeAndDeviceController(PortableVolumeAndDeviceData settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _volumeBeforeMute = Settings.SystemVolume;
    }

    public PortableVolumeAndDeviceData Settings { get; }

    public void ApplyPreset(PortableVolumePreset preset)
    {
        var (systemVolume, notificationVolume, effectVolume) = preset switch
        {
            PortableVolumePreset.Quiet => (30d, 50d, 40d),
            PortableVolumePreset.Loud => (100d, 100d, 100d),
            _ => (80d, 100d, 80d)
        };

        Settings.SystemVolume = systemVolume;
        Settings.NotificationVolume = notificationVolume;
        Settings.EffectVolume = effectVolume;
        _volumeBeforeMute = systemVolume;
    }

    public void ToggleMute()
    {
        if (Settings.IsMuted)
        {
            Settings.SystemVolume = _volumeBeforeMute;
            Settings.IsMuted = false;
            return;
        }

        _volumeBeforeMute = Settings.SystemVolume;
        Settings.SystemVolume = 0;
        Settings.IsMuted = true;
    }
}

public sealed class PortableEqualizerController
{
    private readonly PortableVolumeAndDeviceData _settings;

    public PortableEqualizerController(PortableVolumeAndDeviceData settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool ApplyPreset(string presetName)
    {
        if (!GetPresets().TryGetValue(presetName, out var preset))
        {
            return false;
        }

        _settings.EqualizerPreset = presetName;
        _settings.BassLevel = preset.bass;
        _settings.MidBassLevel = preset.midBass;
        _settings.MidLevel = preset.mid;
        _settings.MidTrebleLevel = preset.midTreble;
        _settings.TrebleLevel = preset.treble;
        return true;
    }

    public void UpdateBands(
        double bass,
        double midBass,
        double mid,
        double midTreble,
        double treble)
    {
        _settings.EqualizerPreset = "自定义";
        _settings.BassLevel = ClampGain(bass);
        _settings.MidBassLevel = ClampGain(midBass);
        _settings.MidLevel = ClampGain(mid);
        _settings.MidTrebleLevel = ClampGain(midTreble);
        _settings.TrebleLevel = ClampGain(treble);
    }

    public static IReadOnlyDictionary<string, (double bass, double midBass, double mid, double midTreble, double treble)> GetPresets()
    {
        return new Dictionary<string, (double, double, double, double, double)>(StringComparer.Ordinal)
        {
            ["默认"] = (0, 0, 0, 0, 0),
            ["流行"] = (3, 1, 0, 1, 3),
            ["摇滚"] = (5, 2, -1, 2, 4),
            ["古典"] = (-2, -1, 0, 2, 3),
            ["爵士"] = (3, 1, 1, 1, 2),
            ["电子"] = (5, 3, 0, 1, 5),
            ["低音增强"] = (8, 5, 0, 0, 0),
            ["人声增强"] = (-2, 0, 4, 3, -1),
            ["柔和"] = (-3, -2, 0, 2, 3)
        };
    }

    private static double ClampGain(double gain)
    {
        return Math.Clamp(gain, -12, 12);
    }
}

public sealed class FileVolumeAndDeviceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileVolumeAndDeviceSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Volume and device settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableVolumeAndDeviceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableVolumeAndDeviceData.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableVolumeAndDeviceData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableVolumeAndDeviceData.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableVolumeAndDeviceData.CreateDefault();
        }
        catch (IOException)
        {
            return PortableVolumeAndDeviceData.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableVolumeAndDeviceData settings,
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

public enum PortableAudioDeviceSelectionStatus
{
    Selected,
    NotFound,
    WrongType
}

public sealed record PortableAudioDeviceSelectionResult(
    PortableAudioDeviceSelectionStatus Status,
    string DeviceId,
    string Message);

public sealed class PortableAudioDeviceSelectionController
{
    private readonly PortableVolumeAndDeviceData _settings;
    private readonly IReadOnlyList<PortableAudioDeviceInfo> _devices;

    public PortableAudioDeviceSelectionController(
        PortableVolumeAndDeviceData settings,
        IReadOnlyList<PortableAudioDeviceInfo> devices)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
    }

    public PortableAudioDeviceSelectionResult SelectOutputDevice(string deviceId)
    {
        return SelectDevice(deviceId, PortableAudioDeviceType.Output);
    }

    public PortableAudioDeviceSelectionResult SelectInputDevice(string deviceId)
    {
        return SelectDevice(deviceId, PortableAudioDeviceType.Input);
    }

    private PortableAudioDeviceSelectionResult SelectDevice(
        string deviceId,
        PortableAudioDeviceType expectedType)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.NotFound,
                string.Empty,
                "未找到音频设备");
        }

        var device = _devices.FirstOrDefault(device =>
            string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal));
        if (device is null)
        {
            return new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.NotFound,
                deviceId,
                "未找到音频设备");
        }

        if (device.DeviceType != expectedType)
        {
            return new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.WrongType,
                deviceId,
                expectedType == PortableAudioDeviceType.Output
                    ? "请选择输出设备"
                    : "请选择输入设备");
        }

        if (expectedType == PortableAudioDeviceType.Output)
        {
            _settings.OutputDeviceId = device.DeviceId;
        }
        else
        {
            _settings.InputDeviceId = device.DeviceId;
        }

        return new PortableAudioDeviceSelectionResult(
            PortableAudioDeviceSelectionStatus.Selected,
            device.DeviceId,
            $"已选择音频设备: {device.DeviceName}");
    }
}

public sealed class PortableAudioDeviceTestController
{
    private readonly PortableVolumeAndDeviceData _settings;
    private readonly IReadOnlyList<PortableAudioDeviceInfo> _devices;
    private readonly IPortableSoundOutput _soundOutput;

    public PortableAudioDeviceTestController(
        PortableVolumeAndDeviceData settings,
        IReadOnlyList<PortableAudioDeviceInfo> devices,
        IPortableSoundOutput soundOutput)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _soundOutput = soundOutput ?? throw new ArgumentNullException(nameof(soundOutput));
    }

    public async Task<PortableAudioDeviceSelectionResult> TestOutputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var deviceResult = FindDevice(deviceId, PortableAudioDeviceType.Output);
        if (deviceResult.Device is null)
        {
            return deviceResult.Result;
        }

        await _soundOutput.PlaySystemSoundAsync(
            PortableSystemSound.Asterisk,
            NormalizeVolume(_settings.EffectVolume),
            cancellationToken).ConfigureAwait(false);

        return new PortableAudioDeviceSelectionResult(
            PortableAudioDeviceSelectionStatus.Selected,
            deviceResult.Device.DeviceId,
            $"已播放测试音: {deviceResult.Device.DeviceName}");
    }

    public Task<PortableAudioDeviceSelectionResult> TestInputDeviceAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deviceResult = FindDevice(deviceId, PortableAudioDeviceType.Input);
        if (deviceResult.Device is null)
        {
            return Task.FromResult(deviceResult.Result);
        }

        return Task.FromResult(new PortableAudioDeviceSelectionResult(
            PortableAudioDeviceSelectionStatus.Selected,
            deviceResult.Device.DeviceId,
            $"输入设备已就绪: {deviceResult.Device.DeviceName}"));
    }

    private (PortableAudioDeviceInfo? Device, PortableAudioDeviceSelectionResult Result) FindDevice(
        string deviceId,
        PortableAudioDeviceType expectedType)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return (null, new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.NotFound,
                string.Empty,
                "未找到音频设备"));
        }

        var device = _devices.FirstOrDefault(device =>
            string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal));
        if (device is null)
        {
            return (null, new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.NotFound,
                deviceId,
                "未找到音频设备"));
        }

        if (device.DeviceType != expectedType)
        {
            return (null, new PortableAudioDeviceSelectionResult(
                PortableAudioDeviceSelectionStatus.WrongType,
                deviceId,
                expectedType == PortableAudioDeviceType.Output
                    ? "请选择输出设备"
                    : "请选择输入设备"));
        }

        return (device, new PortableAudioDeviceSelectionResult(
            PortableAudioDeviceSelectionStatus.Selected,
            device.DeviceId,
            string.Empty));
    }

    private static double NormalizeVolume(double volume)
    {
        return Math.Clamp(volume, 0, 100) / 100.0;
    }
}

public sealed class PortableVoiceBroadcastData
{
    [JsonPropertyName("IsEnabled")] public bool IsEnabled { get; set; }

    [JsonPropertyName("Speed")] public int Speed { get; set; }

    [JsonPropertyName("Volume")] public int Volume { get; set; } = 100;

    [JsonPropertyName("Pitch")] public int Pitch { get; set; }

    [JsonPropertyName("TestText")] public string TestText { get; set; } = "这是一条测试语音播报";

    [JsonPropertyName("BroadcastOnNotification")] public bool BroadcastOnNotification { get; set; } = true;

    [JsonPropertyName("BroadcastOnError")] public bool BroadcastOnError { get; set; } = true;

    [JsonPropertyName("BroadcastOnSuccess")] public bool BroadcastOnSuccess { get; set; } = true;

    public static PortableVoiceBroadcastData CreateDefault()
    {
        return new PortableVoiceBroadcastData
        {
            IsEnabled = false,
            Speed = 0,
            Volume = 100,
            Pitch = 0,
            TestText = PortableVoiceBroadcastController.DefaultTestText,
            BroadcastOnNotification = true,
            BroadcastOnError = true,
            BroadcastOnSuccess = true
        };
    }

    public PortableVoiceBroadcastData Clone()
    {
        return new PortableVoiceBroadcastData
        {
            IsEnabled = IsEnabled,
            Speed = Speed,
            Volume = Volume,
            Pitch = Pitch,
            TestText = TestText,
            BroadcastOnNotification = BroadcastOnNotification,
            BroadcastOnError = BroadcastOnError,
            BroadcastOnSuccess = BroadcastOnSuccess
        };
    }
}

public sealed class FileVoiceBroadcastSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileVoiceBroadcastSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Voice broadcast settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableVoiceBroadcastData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableVoiceBroadcastData.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableVoiceBroadcastData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableVoiceBroadcastData.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableVoiceBroadcastData.CreateDefault();
        }
        catch (IOException)
        {
            return PortableVoiceBroadcastData.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableVoiceBroadcastData settings,
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

public sealed class PortableVoiceBroadcastController
{
    public const string DefaultTestText = "这是一条测试语音播报";

    public PortableVoiceBroadcastController(PortableVoiceBroadcastData settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public PortableVoiceBroadcastData Settings { get; }

    public void UpdateParameters(int speed, int volume, int pitch, string? testText)
    {
        Settings.Speed = Math.Clamp(speed, -10, 10);
        Settings.Volume = Math.Clamp(volume, 0, 100);
        Settings.Pitch = Math.Clamp(pitch, -10, 10);
        Settings.TestText = string.IsNullOrWhiteSpace(testText) ? DefaultTestText : testText;
    }

    public bool ShouldBroadcast(PortableNotificationType type)
    {
        if (!Settings.IsEnabled)
        {
            return false;
        }

        return type switch
        {
            PortableNotificationType.Success => Settings.BroadcastOnSuccess,
            PortableNotificationType.Error => Settings.BroadcastOnError,
            _ => Settings.BroadcastOnNotification
        };
    }
}

public sealed class PortableNotificationSoundOptions
{
    public bool NotificationSound { get; init; } = true;

    public string? CustomSoundDirectory { get; init; }

    public PortableSoundSchemeData SoundScheme { get; init; } = new();

    public PortableVolumeAndDeviceData VolumeAndDevice { get; init; } = new();

    public PortableVoiceBroadcastData VoiceBroadcast { get; init; } = new();
}

public interface IPortableSoundOutput
{
    Task PlayFileAsync(string filePath, double volume, CancellationToken cancellationToken = default);

    Task PlaySystemSoundAsync(
        PortableSystemSound systemSound,
        double volume,
        CancellationToken cancellationToken = default);
}

public interface IPortableSpeechOutput
{
    Task SpeakAsync(
        string text,
        int speed,
        int volume,
        int pitch,
        CancellationToken cancellationToken = default);
}

public sealed class PortableNotificationSoundPlayer : IPortableNotificationSoundPlayer
{
    private readonly PortableNotificationSoundOptions _options;
    private readonly IPortableSoundOutput _soundOutput;
    private readonly IPortableSpeechOutput? _speechOutput;

    public PortableNotificationSoundPlayer(
        PortableNotificationSoundOptions options,
        IPortableSoundOutput soundOutput,
        IPortableSpeechOutput? speechOutput = null)
    {
        _options = options;
        _soundOutput = soundOutput;
        _speechOutput = speechOutput;
    }

    public Task PlayAsync(
        PortableNotificationType type,
        bool isHighPriority,
        CancellationToken cancellationToken = default)
    {
        return PlayAsync(
            new PortableNotificationRequest
            {
                Type = type,
                IsHighPriority = isHighPriority
            },
            cancellationToken);
    }

    public async Task PlayAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.NotificationSound || _options.VolumeAndDevice.IsMuted)
        {
            return;
        }

        var eventName = GetEventName(request.Type);
        var mappedSound = GetMappedSound(eventName);
        if (IsNone(mappedSound))
        {
            return;
        }

        var volume = NormalizeVolume(_options.VolumeAndDevice.NotificationVolume);
        var filePath = ResolveCustomSoundPath(mappedSound);
        if (filePath is not null)
        {
            await _soundOutput.PlayFileAsync(filePath, volume, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _soundOutput.PlaySystemSoundAsync(
            GetSystemSound(request.Type),
            volume,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task BroadcastNotificationAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_speechOutput is null || !ShouldBroadcast(request.Type))
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(request.Message)
            ? request.Title
            : $"{request.Title}。{request.Message}";
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _speechOutput.SpeakAsync(
            text,
            _options.VoiceBroadcast.Speed,
            _options.VoiceBroadcast.Volume,
            _options.VoiceBroadcast.Pitch,
            cancellationToken).ConfigureAwait(false);
    }

    public static string GetEventName(PortableNotificationType type)
    {
        return type switch
        {
            PortableNotificationType.Success => "成功",
            PortableNotificationType.Warning => "警告",
            PortableNotificationType.Error => "错误",
            _ => "信息提示"
        };
    }

    public static PortableSystemSound GetSystemSound(PortableNotificationType type)
    {
        return type switch
        {
            PortableNotificationType.Success => PortableSystemSound.Asterisk,
            PortableNotificationType.Warning => PortableSystemSound.Exclamation,
            PortableNotificationType.Error => PortableSystemSound.Hand,
            _ => PortableSystemSound.Beep
        };
    }

    private static double NormalizeVolume(double volume)
    {
        return Math.Clamp(volume, 0, 100) / 100.0;
    }

    private string? GetMappedSound(string eventName)
    {
        return _options.SoundScheme.EventSoundMappings.TryGetValue(eventName, out var soundName)
            ? soundName
            : null;
    }

    private string? ResolveCustomSoundPath(string? mappedSound)
    {
        if (string.IsNullOrWhiteSpace(mappedSound) || string.IsNullOrWhiteSpace(_options.CustomSoundDirectory))
        {
            return null;
        }

        var wavPath = Path.Combine(_options.CustomSoundDirectory, mappedSound + ".wav");
        if (File.Exists(wavPath))
        {
            return wavPath;
        }

        var exactPath = Path.Combine(_options.CustomSoundDirectory, mappedSound);
        return File.Exists(exactPath) ? exactPath : null;
    }

    private bool ShouldBroadcast(PortableNotificationType type)
    {
        var settings = _options.VoiceBroadcast;
        if (!settings.IsEnabled)
        {
            return false;
        }

        return type switch
        {
            PortableNotificationType.Success => settings.BroadcastOnSuccess,
            PortableNotificationType.Error => settings.BroadcastOnError,
            _ => settings.BroadcastOnNotification
        };
    }

    private static bool IsNone(string? mappedSound)
    {
        return string.Equals(mappedSound?.Trim(), "无", StringComparison.Ordinal);
    }
}
