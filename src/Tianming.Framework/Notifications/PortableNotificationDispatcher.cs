namespace TM.Framework.Notifications;

public enum PortableNotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public enum PortableNotificationDispatchStatus
{
    Delivered,
    RecordedOnly,
    BlockedByDoNotDisturb
}

public sealed class PortableNotificationRequest
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public PortableNotificationType Type { get; init; } = PortableNotificationType.Info;

    public bool IsHighPriority { get; init; }

    public string? SourceApp { get; init; }
}

public sealed class PortableNotificationDispatcherOptions
{
    public bool EnableSystemNotification { get; init; }

    public bool NotificationSound { get; init; } = true;

    public DoNotDisturbSettingsData DoNotDisturb { get; init; } = new();

    public Func<DateTime>? Clock { get; init; }
}

public sealed class PortableNotificationDispatchResult
{
    public PortableNotificationDispatchStatus Status { get; init; }

    public NotificationRecordData HistoryRecord { get; init; } = new();
}

public interface IPortableNotificationSink
{
    Task DeliverAsync(PortableNotificationRequest request, CancellationToken cancellationToken = default);
}

public interface IPortableNotificationSoundPlayer
{
    Task PlayAsync(PortableNotificationType type, bool isHighPriority, CancellationToken cancellationToken = default);
}

public sealed class PortableNotificationDispatcher
{
    private readonly PortableNotificationDispatcherOptions _options;
    private readonly FileNotificationHistoryStore _historyStore;
    private readonly IPortableNotificationSink _sink;
    private readonly IPortableNotificationSoundPlayer? _soundPlayer;

    public PortableNotificationDispatcher(
        PortableNotificationDispatcherOptions options,
        FileNotificationHistoryStore historyStore,
        IPortableNotificationSink sink,
        IPortableNotificationSoundPlayer? soundPlayer = null)
    {
        _options = options;
        _historyStore = historyStore;
        _sink = sink;
        _soundPlayer = soundPlayer;
    }

    public async Task<PortableNotificationDispatchResult> DispatchAsync(
        PortableNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = new DoNotDisturbPolicy(_options.DoNotDisturb);
        var isBlocked = policy.ShouldBlock(
            request.IsHighPriority,
            _options.Clock?.Invoke(),
            request.SourceApp);

        var historyRecord = await _historyStore.AddRecordAsync(
            request.Title,
            request.Message,
            ToHistoryType(request.Type),
            isBlocked,
            cancellationToken).ConfigureAwait(false);

        if (isBlocked)
        {
            return new PortableNotificationDispatchResult
            {
                Status = PortableNotificationDispatchStatus.BlockedByDoNotDisturb,
                HistoryRecord = historyRecord
            };
        }

        if (!_options.EnableSystemNotification)
        {
            return new PortableNotificationDispatchResult
            {
                Status = PortableNotificationDispatchStatus.RecordedOnly,
                HistoryRecord = historyRecord
            };
        }

        if (_options.NotificationSound && _soundPlayer is not null)
        {
            await _soundPlayer.PlayAsync(request.Type, request.IsHighPriority, cancellationToken).ConfigureAwait(false);
        }

        await _sink.DeliverAsync(request, cancellationToken).ConfigureAwait(false);

        return new PortableNotificationDispatchResult
        {
            Status = PortableNotificationDispatchStatus.Delivered,
            HistoryRecord = historyRecord
        };
    }

    public static string ToHistoryType(PortableNotificationType type)
    {
        return type switch
        {
            PortableNotificationType.Success => "成功",
            PortableNotificationType.Warning => "警告",
            PortableNotificationType.Error => "错误",
            _ => "信息"
        };
    }
}
