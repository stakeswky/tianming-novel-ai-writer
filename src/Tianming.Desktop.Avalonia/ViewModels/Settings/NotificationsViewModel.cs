using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Notifications;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

/// <summary>
/// M7 Lane B "通知" 设置 page VM。一个 page 含 7 个 SectionCard 覆盖矩阵 D #12-#18：
/// #12 Toast 样式 / #13 系统通知 (sink 状态 + 测试发通知) / #14 系统集成 /
/// #15 通知历史 / #16 勿扰 / #17 声音方案 / #18 语音播报。
///
/// 多数 lib 类是 data class（无 controller），VM 直接通过 DI 共享的 Singleton 实例
/// 读 / 写字段；PortableDoNotDisturbController.Toggle 是有 controller API 的，
/// SoundScheme 用 PortableSoundSchemeController.SelectScheme 切换。
/// </summary>
public partial class NotificationsViewModel : ObservableObject
{
    private readonly FileNotificationHistoryStore _historyStore;
    private readonly PortableNotificationDispatcher _dispatcher;
    private readonly PortableDoNotDisturbController _dnd;
    private readonly DoNotDisturbSettingsData _dndSettings;
    private readonly PortableNotificationSoundOptions _soundOptions;
    private readonly PortableToastStyleData _toastStyle;
    private readonly PortableSystemIntegrationSettings _sysIntegration;
    private readonly IPortableNotificationSink _sink;

    // #15 通知历史
    public ObservableCollection<NotificationRecordData> History { get; } = new();
    [ObservableProperty] private bool _hasNoHistory = true;

    // #13 系统通知
    [ObservableProperty] private string _sinkName = string.Empty;

    // #16 勿扰
    [ObservableProperty] private string _dndStatusText = string.Empty;
    [ObservableProperty] private bool _dndEnabled;

    // #17 声音方案
    [ObservableProperty] private string _soundSchemeName = string.Empty;

    // #18 语音播报
    [ObservableProperty] private bool _voiceBroadcastEnabled;
    [ObservableProperty] private int _voiceSpeed;
    [ObservableProperty] private int _voiceVolume;

    // #12 Toast 样式（暴露最常见字段，其他 deferred）
    [ObservableProperty] private double _toastCornerRadius;
    [ObservableProperty] private string _toastScreenPosition = string.Empty;

    // #14 系统集成（暴露最常见字段）
    [ObservableProperty] private bool _showTrayIcon;
    [ObservableProperty] private bool _autoStartup;
    [ObservableProperty] private bool _enableSystemNotification;

    public NotificationsViewModel(
        FileNotificationHistoryStore historyStore,
        PortableNotificationDispatcher dispatcher,
        PortableDoNotDisturbController dnd,
        DoNotDisturbSettingsData dndSettings,
        PortableNotificationSoundOptions soundOptions,
        PortableToastStyleData toastStyle,
        PortableSystemIntegrationSettings sysIntegration,
        IPortableNotificationSink sink)
    {
        _historyStore = historyStore;
        _dispatcher = dispatcher;
        _dnd = dnd;
        _dndSettings = dndSettings;
        _soundOptions = soundOptions;
        _toastStyle = toastStyle;
        _sysIntegration = sysIntegration;
        _sink = sink;

        LoadInitialState();
    }

    private void LoadInitialState()
    {
        SinkName = _sink.GetType().Name;
        DndStatusText = _dnd.StatusText;
        DndEnabled = _dndSettings.IsEnabled;
        SoundSchemeName = _soundOptions.SoundScheme.ActiveSchemeId;
        VoiceBroadcastEnabled = _soundOptions.VoiceBroadcast.IsEnabled;
        VoiceSpeed = _soundOptions.VoiceBroadcast.Speed;
        VoiceVolume = _soundOptions.VoiceBroadcast.Volume;
        ToastCornerRadius = _toastStyle.CornerRadius;
        ToastScreenPosition = _toastStyle.ScreenPosition.ToString();
        ShowTrayIcon = _sysIntegration.ShowTrayIcon;
        AutoStartup = _sysIntegration.AutoStartup;
        EnableSystemNotification = _sysIntegration.EnableSystemNotification;
    }

    [RelayCommand]
    public async Task RefreshHistoryAsync()
    {
        var records = await _historyStore.GetRecordsAsync();
        History.Clear();
        foreach (var rec in records.OrderByDescending(r => r.Time))
            History.Add(rec);
        HasNoHistory = History.Count == 0;
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        await _historyStore.ClearAllAsync();
        History.Clear();
        HasNoHistory = true;
    }

    [RelayCommand]
    public async Task SendTestNotificationAsync()
    {
        var request = new PortableNotificationRequest
        {
            Title = "Tianming Test",
            Message = $"测试通知 @ {DateTime.Now:HH:mm:ss}",
            Type = PortableNotificationType.Info,
        };
        await _dispatcher.DispatchAsync(request);
        // 触发刷新历史，让用户立刻看到新 record
        await RefreshHistoryAsync();
    }

    [RelayCommand]
    public void ToggleDoNotDisturb()
    {
        _dnd.Toggle();
        DndStatusText = _dnd.StatusText;
        DndEnabled = _dndSettings.IsEnabled;
    }

    // 数据 toggle 同步：UI binding 改 ObservableProperty 时把 data class 字段也写回
    partial void OnShowTrayIconChanged(bool value) => _sysIntegration.ShowTrayIcon = value;
    partial void OnAutoStartupChanged(bool value) => _sysIntegration.AutoStartup = value;
    partial void OnEnableSystemNotificationChanged(bool value) => _sysIntegration.EnableSystemNotification = value;
    partial void OnVoiceBroadcastEnabledChanged(bool value) => _soundOptions.VoiceBroadcast.IsEnabled = value;
    partial void OnVoiceSpeedChanged(int value) => _soundOptions.VoiceBroadcast.Speed = value;
    partial void OnVoiceVolumeChanged(int value) => _soundOptions.VoiceBroadcast.Volume = value;
    partial void OnToastCornerRadiusChanged(double value) => _toastStyle.CornerRadius = value;
}
