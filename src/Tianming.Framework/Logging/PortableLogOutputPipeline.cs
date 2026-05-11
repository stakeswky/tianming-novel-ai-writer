namespace TM.Framework.Logging;

public enum PortableLogOutputTargetType
{
    File,
    Console,
    EventLog,
    RemoteHttp,
    RemoteTcp
}

public sealed class PortableLogOutputTarget
{
    public string Name { get; set; } = string.Empty;

    public PortableLogOutputTargetType Type { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int Priority { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record PortableLogOutputAttempt(
    string TargetName,
    PortableLogOutputTargetType TargetType,
    bool Success,
    string? ErrorMessage = null);

public sealed record PortableLogOutputFailureRecord(
    DateTime FailureTime,
    string TargetName,
    PortableLogOutputTargetType TargetType,
    string ErrorMessage,
    string LogContent);

public class PortableLogOutputDispatchResult
{
    public List<PortableLogOutputAttempt> Attempts { get; } = new();

    public List<PortableLogOutputFailureRecord> Failures { get; } = new();

    public bool AllSucceeded => Failures.Count == 0;

    public void Add(PortableLogOutputDispatchResult result)
    {
        Attempts.AddRange(result.Attempts);
        Failures.AddRange(result.Failures);
    }
}

public sealed class PortableLogOutputQueueFlushResult : PortableLogOutputDispatchResult
{
    public int DroppedLowPriorityCount { get; init; }
}

public interface IPortableLogOutputSink
{
    PortableLogOutputTargetType TargetType { get; }

    Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default);
}

public sealed class PortableLogOutputDispatcher
{
    private readonly IReadOnlyDictionary<PortableLogOutputTargetType, IPortableLogOutputSink> _sinks;
    private readonly Func<DateTime> _clock;

    public PortableLogOutputDispatcher(
        IEnumerable<IPortableLogOutputSink> sinks,
        Func<DateTime>? clock = null)
    {
        _sinks = sinks.ToDictionary(sink => sink.TargetType);
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableLogOutputDispatchResult> DispatchAsync(
        IEnumerable<PortableLogOutputTarget> targets,
        string content,
        CancellationToken cancellationToken = default)
    {
        var result = new PortableLogOutputDispatchResult();
        foreach (var target in targets
                     .Where(target => target.IsEnabled)
                     .OrderBy(target => target.Priority))
        {
            if (!_sinks.TryGetValue(target.Type, out var sink))
            {
                AddFailure(result, target, content, $"No sink registered for {target.Type}.");
                continue;
            }

            try
            {
                await sink.WriteAsync(target, content, cancellationToken).ConfigureAwait(false);
                result.Attempts.Add(new PortableLogOutputAttempt(target.Name, target.Type, true));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or HttpRequestException)
            {
                AddFailure(result, target, content, ex.Message);
            }
        }

        return result;
    }

    private void AddFailure(
        PortableLogOutputDispatchResult result,
        PortableLogOutputTarget target,
        string content,
        string errorMessage)
    {
        result.Attempts.Add(new PortableLogOutputAttempt(target.Name, target.Type, false, errorMessage));
        result.Failures.Add(new PortableLogOutputFailureRecord(
            _clock(),
            target.Name,
            target.Type,
            errorMessage,
            content));
    }
}

public sealed class PortableAsyncLogOutputQueueOptions
{
    public int LowPriorityBufferSize { get; set; } = 4096;

    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
}

public sealed class PortableAsyncLogOutputQueue
{
    private readonly object _gate = new();
    private readonly Queue<string> _highPriority = new();
    private readonly Queue<string> _lowPriority = new();
    private readonly PortableAsyncLogOutputQueueOptions _options;
    private readonly PortableLogOutputDispatcher _dispatcher;
    private int _droppedLowPriority;

    public PortableAsyncLogOutputQueue(
        PortableAsyncLogOutputQueueOptions? options,
        PortableLogOutputDispatcher dispatcher)
    {
        _options = options ?? new PortableAsyncLogOutputQueueOptions();
        _dispatcher = dispatcher;
    }

    public bool Enqueue(LogSeverity level, string formattedLine)
    {
        lock (_gate)
        {
            if (level >= LogSeverity.Warning)
            {
                _highPriority.Enqueue(formattedLine);
                return true;
            }

            var capacity = Math.Max(0, _options.LowPriorityBufferSize);
            if (_lowPriority.Count >= capacity)
            {
                _droppedLowPriority++;
                return false;
            }

            _lowPriority.Enqueue(formattedLine);
            return true;
        }
    }

    public async Task<PortableLogOutputQueueFlushResult> FlushAsync(
        IEnumerable<PortableLogOutputTarget> targets,
        CancellationToken cancellationToken = default)
    {
        var targetList = targets.ToList();
        var (highPriority, lowPriority, dropped) = Drain();
        var result = new PortableLogOutputQueueFlushResult
        {
            DroppedLowPriorityCount = dropped
        };

        foreach (var line in highPriority)
        {
            result.Add(await _dispatcher.DispatchAsync(targetList, line, cancellationToken).ConfigureAwait(false));
        }

        foreach (var line in lowPriority)
        {
            result.Add(await _dispatcher.DispatchAsync(targetList, line, cancellationToken).ConfigureAwait(false));
        }

        if (dropped > 0)
        {
            var reportLine = $"[{_options.Clock():yyyy-MM-dd HH:mm:ss.fff}] [WRN] [LogManager] 低优先级日志队列已满，已丢弃 {dropped} 条日志";
            result.Add(await _dispatcher.DispatchAsync(targetList, reportLine, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private (List<string> HighPriority, List<string> LowPriority, int Dropped) Drain()
    {
        lock (_gate)
        {
            var highPriority = _highPriority.ToList();
            var lowPriority = _lowPriority.ToList();
            var dropped = _droppedLowPriority;

            _highPriority.Clear();
            _lowPriority.Clear();
            _droppedLowPriority = 0;

            return (highPriority, lowPriority, dropped);
        }
    }
}
