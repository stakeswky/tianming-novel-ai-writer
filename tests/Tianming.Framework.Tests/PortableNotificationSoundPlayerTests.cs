using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableNotificationSoundPlayerTests
{
    [Fact]
    public async Task PlayAsync_uses_custom_file_mapping_before_system_fallback()
    {
        using var workspace = new TempDirectory();
        var soundFile = Path.Combine(workspace.Path, "ding.wav");
        await File.WriteAllTextAsync(soundFile, "fake wav");
        var output = new RecordingSoundOutput();
        var player = new PortableNotificationSoundPlayer(
            new PortableNotificationSoundOptions
            {
                CustomSoundDirectory = workspace.Path,
                VolumeAndDevice = new PortableVolumeAndDeviceData
                {
                    NotificationVolume = 125
                },
                SoundScheme = new PortableSoundSchemeData
                {
                    EventSoundMappings =
                    {
                        ["成功"] = "ding"
                    }
                }
            },
            output);

        await player.PlayAsync(new PortableNotificationRequest
        {
            Title = "保存成功",
            Message = "章节已保存",
            Type = PortableNotificationType.Success
        });

        var played = Assert.Single(output.FileSounds);
        Assert.Equal(soundFile, played.Path);
        Assert.Equal(1.0, played.Volume);
        Assert.Empty(output.SystemSounds);
    }

    [Fact]
    public async Task PlayAsync_falls_back_to_system_sound_when_custom_file_is_missing()
    {
        using var workspace = new TempDirectory();
        var output = new RecordingSoundOutput();
        var player = new PortableNotificationSoundPlayer(
            new PortableNotificationSoundOptions
            {
                CustomSoundDirectory = workspace.Path,
                VolumeAndDevice = new PortableVolumeAndDeviceData
                {
                    NotificationVolume = 35
                },
                SoundScheme = new PortableSoundSchemeData
                {
                    EventSoundMappings =
                    {
                        ["警告"] = "missing.wav"
                    }
                }
            },
            output);

        await player.PlayAsync(new PortableNotificationRequest
        {
            Title = "规则风险",
            Message = "需要检查",
            Type = PortableNotificationType.Warning
        });

        var played = Assert.Single(output.SystemSounds);
        Assert.Equal(PortableSystemSound.Exclamation, played.Sound);
        Assert.Equal(0.35, played.Volume);
        Assert.Empty(output.FileSounds);
    }

    [Theory]
    [InlineData(false, false, "ding")]
    [InlineData(true, true, "ding")]
    [InlineData(true, false, "无")]
    public async Task PlayAsync_skips_when_disabled_muted_or_mapping_is_none(
        bool notificationSound,
        bool isMuted,
        string mappedSound)
    {
        using var workspace = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "ding.wav"), "fake wav");
        var output = new RecordingSoundOutput();
        var player = new PortableNotificationSoundPlayer(
            new PortableNotificationSoundOptions
            {
                NotificationSound = notificationSound,
                CustomSoundDirectory = workspace.Path,
                VolumeAndDevice = new PortableVolumeAndDeviceData
                {
                    IsMuted = isMuted,
                    NotificationVolume = 80
                },
                SoundScheme = new PortableSoundSchemeData
                {
                    EventSoundMappings =
                    {
                        ["信息提示"] = mappedSound
                    }
                }
            },
            output);

        await player.PlayAsync(new PortableNotificationRequest
        {
            Title = "普通通知",
            Message = "不应播放",
            Type = PortableNotificationType.Info
        });

        Assert.Empty(output.FileSounds);
        Assert.Empty(output.SystemSounds);
    }

    [Fact]
    public async Task BroadcastNotificationAsync_respects_voice_toggles_and_formats_text()
    {
        var speech = new RecordingSpeechOutput();
        var player = new PortableNotificationSoundPlayer(
            new PortableNotificationSoundOptions
            {
                VoiceBroadcast = new PortableVoiceBroadcastData
                {
                    IsEnabled = true,
                    BroadcastOnSuccess = false,
                    BroadcastOnError = true,
                    Speed = -1,
                    Volume = 75,
                    Pitch = 2
                }
            },
            new RecordingSoundOutput(),
            speech);

        await player.BroadcastNotificationAsync(new PortableNotificationRequest
        {
            Title = "保存成功",
            Message = "不会播报",
            Type = PortableNotificationType.Success
        });
        await player.BroadcastNotificationAsync(new PortableNotificationRequest
        {
            Title = "生成失败",
            Message = "请检查 API Key",
            Type = PortableNotificationType.Error
        });

        var spoken = Assert.Single(speech.Spoken);
        Assert.Equal("生成失败。请检查 API Key", spoken.Text);
        Assert.Equal(-1, spoken.Speed);
        Assert.Equal(75, spoken.Volume);
        Assert.Equal(2, spoken.Pitch);
    }

    [Fact]
    public void VoiceBroadcast_default_settings_match_original_values()
    {
        var settings = PortableVoiceBroadcastData.CreateDefault();

        Assert.False(settings.IsEnabled);
        Assert.Equal(0, settings.Speed);
        Assert.Equal(100, settings.Volume);
        Assert.Equal(0, settings.Pitch);
        Assert.Equal("这是一条测试语音播报", settings.TestText);
        Assert.True(settings.BroadcastOnNotification);
        Assert.True(settings.BroadcastOnError);
        Assert.True(settings.BroadcastOnSuccess);
    }

    [Fact]
    public async Task VoiceBroadcast_store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "Sound", "VoiceBroadcast", "settings.json");
        var store = new FileVoiceBroadcastSettingsStore(path);
        var settings = PortableVoiceBroadcastData.CreateDefault();
        settings.IsEnabled = true;
        settings.Speed = 4;
        settings.Volume = 65;
        settings.Pitch = -2;
        settings.TestText = "测试播报";
        settings.BroadcastOnSuccess = false;

        await store.SaveAsync(settings);
        var reloaded = await new FileVoiceBroadcastSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.IsEnabled);
        Assert.Equal(4, reloaded.Speed);
        Assert.Equal(65, reloaded.Volume);
        Assert.Equal(-2, reloaded.Pitch);
        Assert.Equal("测试播报", reloaded.TestText);
        Assert.False(reloaded.BroadcastOnSuccess);
    }

    [Fact]
    public async Task VoiceBroadcast_store_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileVoiceBroadcastSettingsStore(path);

        Assert.Equal(100, (await store.LoadAsync()).Volume);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.Equal("这是一条测试语音播报", (await store.LoadAsync()).TestText);
    }

    [Fact]
    public void VoiceBroadcast_controller_normalizes_parameters_and_filters_notification_types()
    {
        var controller = new PortableVoiceBroadcastController(PortableVoiceBroadcastData.CreateDefault());

        controller.UpdateParameters(speed: 20, volume: -5, pitch: -20, testText: "   ");
        controller.Settings.IsEnabled = true;
        controller.Settings.BroadcastOnNotification = false;
        controller.Settings.BroadcastOnError = true;
        controller.Settings.BroadcastOnSuccess = false;

        Assert.Equal(10, controller.Settings.Speed);
        Assert.Equal(0, controller.Settings.Volume);
        Assert.Equal(-10, controller.Settings.Pitch);
        Assert.Equal("这是一条测试语音播报", controller.Settings.TestText);
        Assert.False(controller.ShouldBroadcast(PortableNotificationType.Info));
        Assert.True(controller.ShouldBroadcast(PortableNotificationType.Error));
        Assert.False(controller.ShouldBroadcast(PortableNotificationType.Success));
    }

    [Fact]
    public void VolumeAndDevice_default_settings_match_original_values()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();

        Assert.Equal(80, settings.SystemVolume);
        Assert.Equal(100, settings.NotificationVolume);
        Assert.Equal(80, settings.EffectVolume);
        Assert.False(settings.IsMuted);
        Assert.Equal("默认", settings.EqualizerPreset);
        Assert.Equal(0, settings.BassLevel);
        Assert.Null(settings.OutputDeviceId);
        Assert.Null(settings.InputDeviceId);
    }

    [Fact]
    public async Task VolumeAndDevice_store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "Sound", "VolumeAndDevice", "settings.json");
        var store = new FileVolumeAndDeviceSettingsStore(path);
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        settings.SystemVolume = 64;
        settings.NotificationVolume = 42;
        settings.EffectVolume = 55;
        settings.IsMuted = true;
        settings.BassLevel = 3;
        settings.MidBassLevel = 2;
        settings.MidLevel = 1;
        settings.MidTrebleLevel = -2;
        settings.TrebleLevel = -3;
        settings.EqualizerPreset = "摇滚";
        settings.OutputDeviceId = "macos-output-display";
        settings.InputDeviceId = "macos-input-mic";

        await store.SaveAsync(settings);
        var reloaded = await new FileVolumeAndDeviceSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(64, reloaded.SystemVolume);
        Assert.Equal(42, reloaded.NotificationVolume);
        Assert.Equal(55, reloaded.EffectVolume);
        Assert.True(reloaded.IsMuted);
        Assert.Equal(3, reloaded.BassLevel);
        Assert.Equal("摇滚", reloaded.EqualizerPreset);
        Assert.Equal("macos-output-display", reloaded.OutputDeviceId);
        Assert.Equal("macos-input-mic", reloaded.InputDeviceId);
    }

    [Fact]
    public async Task VolumeAndDevice_store_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileVolumeAndDeviceSettingsStore(path);

        Assert.Equal(80, (await store.LoadAsync()).SystemVolume);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.Equal("默认", (await store.LoadAsync()).EqualizerPreset);
    }

    [Fact]
    public void VolumeAndDevice_controller_applies_presets_and_mute_toggle()
    {
        var controller = new PortableVolumeAndDeviceController(PortableVolumeAndDeviceData.CreateDefault());

        controller.ApplyPreset(PortableVolumePreset.Quiet);
        Assert.Equal(30, controller.Settings.SystemVolume);
        Assert.Equal(50, controller.Settings.NotificationVolume);
        Assert.Equal(40, controller.Settings.EffectVolume);

        controller.ToggleMute();
        Assert.True(controller.Settings.IsMuted);
        Assert.Equal(0, controller.Settings.SystemVolume);

        controller.ToggleMute();
        Assert.False(controller.Settings.IsMuted);
        Assert.Equal(30, controller.Settings.SystemVolume);
    }

    [Fact]
    public void Equalizer_controller_exposes_original_preset_table()
    {
        var presets = PortableEqualizerController.GetPresets();

        Assert.Equal(9, presets.Count);
        Assert.Equal((5, 2, -1, 2, 4), presets["摇滚"]);
        Assert.Equal((8, 5, 0, 0, 0), presets["低音增强"]);
        Assert.Equal((-2, 0, 4, 3, -1), presets["人声增强"]);
    }

    [Fact]
    public void Equalizer_controller_applies_known_preset_to_settings()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        var controller = new PortableEqualizerController(settings);

        Assert.True(controller.ApplyPreset("摇滚"));

        Assert.Equal("摇滚", settings.EqualizerPreset);
        Assert.Equal(5, settings.BassLevel);
        Assert.Equal(2, settings.MidBassLevel);
        Assert.Equal(-1, settings.MidLevel);
        Assert.Equal(2, settings.MidTrebleLevel);
        Assert.Equal(4, settings.TrebleLevel);
    }

    [Fact]
    public void Equalizer_controller_rejects_unknown_preset_without_changing_settings()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        settings.EqualizerPreset = "默认";
        settings.BassLevel = 3;
        var controller = new PortableEqualizerController(settings);

        Assert.False(controller.ApplyPreset("不存在"));

        Assert.Equal("默认", settings.EqualizerPreset);
        Assert.Equal(3, settings.BassLevel);
    }

    [Fact]
    public void Equalizer_controller_clamps_manual_band_updates()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        var controller = new PortableEqualizerController(settings);

        controller.UpdateBands(bass: 20, midBass: -20, mid: 4, midTreble: 13, treble: -13);

        Assert.Equal("自定义", settings.EqualizerPreset);
        Assert.Equal(12, settings.BassLevel);
        Assert.Equal(-12, settings.MidBassLevel);
        Assert.Equal(4, settings.MidLevel);
        Assert.Equal(12, settings.MidTrebleLevel);
        Assert.Equal(-12, settings.TrebleLevel);
    }

    [Fact]
    public void AudioDeviceSelection_selects_matching_device_ids_into_settings()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        var controller = new PortableAudioDeviceSelectionController(settings, Devices());

        var outputResult = controller.SelectOutputDevice("macos-output-speakers");
        var inputResult = controller.SelectInputDevice("macos-input-mic");

        Assert.Equal(PortableAudioDeviceSelectionStatus.Selected, outputResult.Status);
        Assert.Equal(PortableAudioDeviceSelectionStatus.Selected, inputResult.Status);
        Assert.Equal("macos-output-speakers", settings.OutputDeviceId);
        Assert.Equal("macos-input-mic", settings.InputDeviceId);
    }

    [Fact]
    public void AudioDeviceSelection_rejects_missing_or_wrong_type_without_overwriting_existing_selection()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        settings.OutputDeviceId = "existing-output";
        settings.InputDeviceId = "existing-input";
        var controller = new PortableAudioDeviceSelectionController(settings, Devices());

        var wrongType = controller.SelectOutputDevice("macos-input-mic");
        var missing = controller.SelectInputDevice("missing-device");

        Assert.Equal(PortableAudioDeviceSelectionStatus.WrongType, wrongType.Status);
        Assert.Equal(PortableAudioDeviceSelectionStatus.NotFound, missing.Status);
        Assert.Equal("existing-output", settings.OutputDeviceId);
        Assert.Equal("existing-input", settings.InputDeviceId);
    }

    [Fact]
    public async Task AudioDeviceTest_plays_output_test_sound_with_effect_volume()
    {
        var settings = PortableVolumeAndDeviceData.CreateDefault();
        settings.EffectVolume = 35;
        var output = new RecordingSoundOutput();
        var controller = new PortableAudioDeviceTestController(settings, Devices(), output);

        var result = await controller.TestOutputDeviceAsync("macos-output-speakers");

        Assert.Equal(PortableAudioDeviceSelectionStatus.Selected, result.Status);
        Assert.Equal("macos-output-speakers", result.DeviceId);
        Assert.Equal("已播放测试音: Speakers", result.Message);
        var sound = Assert.Single(output.SystemSounds);
        Assert.Equal(PortableSystemSound.Asterisk, sound.Sound);
        Assert.Equal(0.35, sound.Volume);
    }

    [Fact]
    public async Task AudioDeviceTest_validates_input_devices_without_playback()
    {
        var output = new RecordingSoundOutput();
        var controller = new PortableAudioDeviceTestController(
            PortableVolumeAndDeviceData.CreateDefault(),
            Devices(),
            output);

        var result = await controller.TestInputDeviceAsync("macos-input-mic");

        Assert.Equal(PortableAudioDeviceSelectionStatus.Selected, result.Status);
        Assert.Equal("macos-input-mic", result.DeviceId);
        Assert.Equal("输入设备已就绪: Microphone", result.Message);
        Assert.Empty(output.SystemSounds);
        Assert.Empty(output.FileSounds);
    }

    [Fact]
    public async Task AudioDeviceTest_rejects_missing_or_wrong_type_devices()
    {
        var output = new RecordingSoundOutput();
        var controller = new PortableAudioDeviceTestController(
            PortableVolumeAndDeviceData.CreateDefault(),
            Devices(),
            output);

        var wrongType = await controller.TestOutputDeviceAsync("macos-input-mic");
        var missing = await controller.TestInputDeviceAsync("missing-device");

        Assert.Equal(PortableAudioDeviceSelectionStatus.WrongType, wrongType.Status);
        Assert.Equal(PortableAudioDeviceSelectionStatus.NotFound, missing.Status);
        Assert.Empty(output.SystemSounds);
    }

    private static IReadOnlyList<PortableAudioDeviceInfo> Devices()
    {
        return
        [
            new PortableAudioDeviceInfo
            {
                DeviceId = "macos-output-speakers",
                DeviceName = "Speakers",
                DeviceType = PortableAudioDeviceType.Output,
                IsDefault = true
            },
            new PortableAudioDeviceInfo
            {
                DeviceId = "macos-input-mic",
                DeviceName = "Microphone",
                DeviceType = PortableAudioDeviceType.Input,
                IsDefault = true
            }
        ];
    }

    private sealed class RecordingSoundOutput : IPortableSoundOutput
    {
        public List<(string Path, double Volume)> FileSounds { get; } = [];

        public List<(PortableSystemSound Sound, double Volume)> SystemSounds { get; } = [];

        public Task PlayFileAsync(string filePath, double volume, CancellationToken cancellationToken = default)
        {
            FileSounds.Add((filePath, volume));
            return Task.CompletedTask;
        }

        public Task PlaySystemSoundAsync(
            PortableSystemSound systemSound,
            double volume,
            CancellationToken cancellationToken = default)
        {
            SystemSounds.Add((systemSound, volume));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSpeechOutput : IPortableSpeechOutput
    {
        public List<(string Text, int Speed, int Volume, int Pitch)> Spoken { get; } = [];

        public Task SpeakAsync(
            string text,
            int speed,
            int volume,
            int pitch,
            CancellationToken cancellationToken = default)
        {
            Spoken.Add((text, speed, volume, pitch));
            return Task.CompletedTask;
        }
    }
}
