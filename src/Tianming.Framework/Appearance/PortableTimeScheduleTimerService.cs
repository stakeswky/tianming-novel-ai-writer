using System.Runtime.CompilerServices;

namespace TM.Framework.Appearance;

public interface IPortableTimerTickSource
{
    IAsyncEnumerable<DateTime> WaitForTicksAsync(
        TimeSpan interval,
        CancellationToken cancellationToken);
}

public sealed class PortablePeriodicTimerTickSource : IPortableTimerTickSource
{
    public async IAsyncEnumerable<DateTime> WaitForTicksAsync(
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return DateTime.UtcNow;
        }
    }
}

public sealed class PortableTimeScheduleTimerService : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _checkAndSwitchAsync;
    private readonly TimeSpan _interval;
    private readonly IPortableTimerTickSource _tickSource;
    private readonly bool _runImmediately;
    private readonly object _lock = new();
    private CancellationTokenSource? _stopSource;
    private Task? _loopTask;

    public PortableTimeScheduleTimerService(
        Func<CancellationToken, Task> checkAndSwitchAsync,
        TimeSpan? interval = null,
        IPortableTimerTickSource? tickSource = null,
        bool runImmediately = true)
    {
        _checkAndSwitchAsync = checkAndSwitchAsync ?? throw new ArgumentNullException(nameof(checkAndSwitchAsync));
        _interval = interval ?? TimeSpan.FromMinutes(1);
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Timer interval must be positive.");
        }

        _tickSource = tickSource ?? new PortablePeriodicTimerTickSource();
        _runImmediately = runImmediately;
    }

    public bool IsRunning { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                return;
            }

            _stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            _loopTask = RunLoopAsync(_stopSource.Token);
        }

        if (_runImmediately)
        {
            await _checkAndSwitchAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync()
    {
        Task? loopTask;
        CancellationTokenSource? stopSource;

        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            loopTask = _loopTask;
            stopSource = _stopSource;
            _loopTask = null;
            _stopSource = null;
        }

        stopSource?.Cancel();
        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        stopSource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in _tickSource.WaitForTicksAsync(_interval, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await _checkAndSwitchAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
