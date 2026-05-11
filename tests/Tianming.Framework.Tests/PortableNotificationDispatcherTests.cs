using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableNotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_records_blocked_notification_without_delivering_or_playing_sound()
    {
        using var workspace = new TempDirectory();
        var history = new FileNotificationHistoryStore(
            Path.Combine(workspace.Path, "notification_history.json"),
            () => new DateTime(2026, 5, 10, 23, 0, 0),
            () => "N001");
        var sink = new RecordingNotificationSink();
        var sound = new RecordingNotificationSoundPlayer();
        var dispatcher = new PortableNotificationDispatcher(
            new PortableNotificationDispatcherOptions
            {
                EnableSystemNotification = true,
                NotificationSound = true,
                DoNotDisturb = new DoNotDisturbSettingsData
                {
                    IsEnabled = true,
                    StartTime = new TimeSpan(22, 0, 0),
                    EndTime = new TimeSpan(8, 0, 0)
                },
                Clock = () => new DateTime(2026, 5, 10, 23, 0, 0)
            },
            history,
            sink,
            sound);

        var result = await dispatcher.DispatchAsync(new PortableNotificationRequest
        {
            Title = "夜间通知",
            Message = "应被勿扰拦截",
            Type = PortableNotificationType.Warning
        });

        Assert.Equal(PortableNotificationDispatchStatus.BlockedByDoNotDisturb, result.Status);
        Assert.Empty(sink.Delivered);
        Assert.Empty(sound.Played);
        var record = Assert.Single(await history.GetRecordsAsync());
        Assert.Equal("夜间通知", record.Title);
        Assert.Equal("警告", record.Type);
        Assert.True(record.WasBlocked);
    }

    [Fact]
    public async Task DispatchAsync_allows_high_priority_when_urgent_notifications_are_allowed()
    {
        using var workspace = new TempDirectory();
        var history = new FileNotificationHistoryStore(Path.Combine(workspace.Path, "history.json"));
        var sink = new RecordingNotificationSink();
        var sound = new RecordingNotificationSoundPlayer();
        var dispatcher = new PortableNotificationDispatcher(
            new PortableNotificationDispatcherOptions
            {
                EnableSystemNotification = true,
                NotificationSound = true,
                DoNotDisturb = new DoNotDisturbSettingsData
                {
                    IsEnabled = true,
                    AllowUrgentNotifications = true,
                    StartTime = new TimeSpan(22, 0, 0),
                    EndTime = new TimeSpan(8, 0, 0)
                },
                Clock = () => new DateTime(2026, 5, 10, 23, 0, 0)
            },
            history,
            sink,
            sound);

        var result = await dispatcher.DispatchAsync(new PortableNotificationRequest
        {
            Title = "紧急通知",
            Message = "需要投递",
            Type = PortableNotificationType.Error,
            IsHighPriority = true
        });

        Assert.Equal(PortableNotificationDispatchStatus.Delivered, result.Status);
        var delivered = Assert.Single(sink.Delivered);
        Assert.Equal("紧急通知", delivered.Title);
        Assert.Equal("错误", Assert.Single(sound.Played).TypeName);
        Assert.False(Assert.Single(await history.GetRecordsAsync()).WasBlocked);
    }

    [Fact]
    public async Task DispatchAsync_records_history_when_system_notification_is_disabled()
    {
        using var workspace = new TempDirectory();
        var history = new FileNotificationHistoryStore(Path.Combine(workspace.Path, "history.json"));
        var sink = new RecordingNotificationSink();
        var dispatcher = new PortableNotificationDispatcher(
            new PortableNotificationDispatcherOptions
            {
                EnableSystemNotification = false,
                NotificationSound = true,
                Clock = () => new DateTime(2026, 5, 10, 12, 0, 0)
            },
            history,
            sink,
            new RecordingNotificationSoundPlayer());

        var result = await dispatcher.DispatchAsync(new PortableNotificationRequest
        {
            Title = "普通通知",
            Message = "只写历史",
            Type = PortableNotificationType.Info
        });

        Assert.Equal(PortableNotificationDispatchStatus.RecordedOnly, result.Status);
        Assert.Empty(sink.Delivered);
        var record = Assert.Single(await history.GetRecordsAsync());
        Assert.Equal("普通通知", record.Title);
        Assert.False(record.WasBlocked);
    }

    private sealed class RecordingNotificationSink : IPortableNotificationSink
    {
        public List<PortableNotificationRequest> Delivered { get; } = [];

        public Task DeliverAsync(PortableNotificationRequest request, CancellationToken cancellationToken = default)
        {
            Delivered.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationSoundPlayer : IPortableNotificationSoundPlayer
    {
        public List<(string TypeName, bool IsHighPriority)> Played { get; } = [];

        public Task PlayAsync(PortableNotificationType type, bool isHighPriority, CancellationToken cancellationToken = default)
        {
            Played.Add((PortableNotificationDispatcher.ToHistoryType(type), isHighPriority));
            return Task.CompletedTask;
        }
    }
}
