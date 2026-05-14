using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.Notifications;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class NotificationsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _historyPath;

    public NotificationsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tm-lane-b-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _historyPath = Path.Combine(_tempDir, "history.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Ctor_loads_initial_state_from_lib()
    {
        var vm = BuildVm();

        Assert.False(string.IsNullOrEmpty(vm.SinkName));            // sink type name
        Assert.False(string.IsNullOrEmpty(vm.SoundSchemeName));     // "default"
        Assert.False(string.IsNullOrEmpty(vm.DndStatusText));       // "免打扰已关闭"
        Assert.True(vm.ToastCornerRadius > 0);                       // default 8
    }

    [Fact]
    public async Task RefreshHistoryAsync_populates_collection_from_store()
    {
        var store = new FileNotificationHistoryStore(_historyPath);
        await store.AddRecordAsync("T1", "M1", "Info", wasBlocked: false);
        await store.AddRecordAsync("T2", "M2", "Warning", wasBlocked: false);

        var vm = BuildVm(store: store);

        await vm.RefreshHistoryAsync();

        Assert.Equal(2, vm.History.Count);
        Assert.False(vm.HasNoHistory);
    }

    [Fact]
    public async Task SendTestNotificationAsync_dispatches_and_records_in_history()
    {
        var store = new FileNotificationHistoryStore(_historyPath);
        var sink = new StubSink();
        var vm = BuildVm(store: store, sink: sink);

        await vm.SendTestNotificationCommand.ExecuteAsync(null);

        Assert.True(sink.DispatchedCount >= 1);  // sink 真接到调用
        Assert.True(vm.History.Count >= 1);      // 历史新增一条
    }

    [Fact]
    public void ToggleDoNotDisturb_flips_status_text()
    {
        var vm = BuildVm();
        var before = vm.DndStatusText;

        vm.ToggleDoNotDisturbCommand.Execute(null);

        Assert.NotEqual(before, vm.DndStatusText);
    }

    [Fact]
    public async Task ClearHistoryAsync_empties_collection()
    {
        var store = new FileNotificationHistoryStore(_historyPath);
        await store.AddRecordAsync("T", "M", "Info", wasBlocked: false);
        var vm = BuildVm(store: store);
        await vm.RefreshHistoryAsync();
        Assert.NotEmpty(vm.History);

        await vm.ClearHistoryCommand.ExecuteAsync(null);

        Assert.Empty(vm.History);
        Assert.True(vm.HasNoHistory);
    }

    [Fact]
    public void Setting_ShowTrayIcon_writes_back_to_settings_data()
    {
        var sysIntegration = new PortableSystemIntegrationSettings { ShowTrayIcon = false };
        var vm = BuildVm(sysIntegration: sysIntegration);

        vm.ShowTrayIcon = true;

        Assert.True(sysIntegration.ShowTrayIcon);
    }

    private NotificationsViewModel BuildVm(
        FileNotificationHistoryStore? store = null,
        PortableSystemIntegrationSettings? sysIntegration = null,
        IPortableNotificationSink? sink = null)
    {
        store ??= new FileNotificationHistoryStore(_historyPath);
        var dndSettings = DoNotDisturbSettingsData.CreateDefault();
        var dnd = new PortableDoNotDisturbController(dndSettings);
        var soundOptions = new PortableNotificationSoundOptions
        {
            SoundScheme = PortableSoundSchemeData.CreateDefault(),
            VolumeAndDevice = PortableVolumeAndDeviceData.CreateDefault(),
            VoiceBroadcast = PortableVoiceBroadcastData.CreateDefault(),
        };
        var toastStyle = new PortableToastStyleData();
        sysIntegration ??= new PortableSystemIntegrationSettings();
        sink ??= new StubSink();
        var soundPlayer = new StubSoundPlayer();
        var dispatcher = new PortableNotificationDispatcher(
            new PortableNotificationDispatcherOptions { EnableSystemNotification = true, NotificationSound = true },
            store,
            sink,
            soundPlayer);
        return new NotificationsViewModel(store, dispatcher, dnd, dndSettings, soundOptions, toastStyle, sysIntegration, sink);
    }

    private sealed class StubSink : IPortableNotificationSink
    {
        public int DispatchedCount { get; private set; }
        public Task DeliverAsync(PortableNotificationRequest request, CancellationToken cancellationToken = default)
        {
            DispatchedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSoundPlayer : IPortableNotificationSoundPlayer
    {
        public Task PlayAsync(PortableNotificationType type, bool isHighPriority, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
