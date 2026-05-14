using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class MacOSNotificationDispatcherTests
{
    [Fact]
    public async Task Dispatcher_delivers_to_registered_sink()
    {
        using var workspace = new TempDirectory();
        var sink = new RecordingNotificationSink();
        var dispatcher = new PortableNotificationDispatcher(
            new PortableNotificationDispatcherOptions
            {
                EnableSystemNotification = true,
                NotificationSound = false,
                Clock = () => new DateTime(2026, 5, 14, 10, 0, 0)
            },
            new FileNotificationHistoryStore(
                Path.Combine(workspace.Path, "notification_history.json"),
                () => new DateTime(2026, 5, 14, 10, 0, 0),
                () => "N001"),
            sink);

        var result = await dispatcher.DispatchAsync(new PortableNotificationRequest
        {
            Title = "系统通知",
            Message = "已投递到 macOS sink",
            Type = PortableNotificationType.Success
        });

        Assert.Equal(PortableNotificationDispatchStatus.Delivered, result.Status);
        var delivered = Assert.Single(sink.Delivered);
        Assert.Equal("系统通知", delivered.Title);
        Assert.Equal("已投递到 macOS sink", delivered.Message);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tianming-notification-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
