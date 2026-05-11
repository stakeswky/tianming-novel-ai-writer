using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableTimeScheduleTimerServiceTests
{
    [Fact]
    public async Task StartAsync_runs_initial_check_and_marks_running()
    {
        var checks = 0;
        await using var service = new PortableTimeScheduleTimerService(
            _ =>
            {
                Interlocked.Increment(ref checks);
                return Task.CompletedTask;
            },
            tickSource: new ManualTickSource());

        await service.StartAsync();

        Assert.True(service.IsRunning);
        Assert.Equal(1, checks);
    }

    [Fact]
    public async Task Tick_invokes_check_after_start()
    {
        var ticks = new ManualTickSource();
        var checks = 0;
        await using var service = new PortableTimeScheduleTimerService(
            _ =>
            {
                Interlocked.Increment(ref checks);
                return Task.CompletedTask;
            },
            tickSource: ticks);

        await service.StartAsync();
        await ticks.TickAsync();
        await ticks.TickAsync();
        await WaitForCheckCountAsync(() => Volatile.Read(ref checks), 3);

        Assert.Equal(3, checks);
    }

    [Fact]
    public async Task StartAsync_is_idempotent_and_does_not_create_duplicate_loops()
    {
        var ticks = new ManualTickSource();
        var checks = 0;
        await using var service = new PortableTimeScheduleTimerService(
            _ =>
            {
                Interlocked.Increment(ref checks);
                return Task.CompletedTask;
            },
            tickSource: ticks);

        await service.StartAsync();
        await service.StartAsync();
        await ticks.TickAsync();
        await WaitForCheckCountAsync(() => Volatile.Read(ref checks), 2);

        Assert.Equal(2, checks);
    }

    [Fact]
    public async Task StopAsync_cancels_loop_and_ignores_later_ticks()
    {
        var ticks = new ManualTickSource();
        var checks = 0;
        await using var service = new PortableTimeScheduleTimerService(
            _ =>
            {
                Interlocked.Increment(ref checks);
                return Task.CompletedTask;
            },
            tickSource: ticks);

        await service.StartAsync();
        await service.StopAsync();
        await ticks.TickAsync();
        await Task.Delay(25);

        Assert.False(service.IsRunning);
        Assert.Equal(1, checks);
    }

    private static async Task WaitForCheckCountAsync(Func<int> currentCount, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < deadline)
        {
            if (currentCount() >= expectedCount)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.Fail($"Expected at least {expectedCount} checks, observed {currentCount()}.");
    }

    private sealed class ManualTickSource : IPortableTimerTickSource
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
        private readonly ConcurrentQueue<TaskCompletionSource> _waiters = new();
        private int _observedTicks;

        public async Task TickAsync()
        {
            await _ticks.Writer.WriteAsync(true);
        }

        public async Task WaitForObservedTicksAsync(int expectedTicks)
        {
            if (Volatile.Read(ref _observedTicks) >= expectedTicks)
            {
                return;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue(waiter);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using var registration = timeout.Token.Register(() => waiter.TrySetCanceled(timeout.Token));
            await waiter.Task;
        }

        public async IAsyncEnumerable<DateTime> WaitForTicksAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _ticks.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_ticks.Reader.TryRead(out _))
                {
                    var observed = Interlocked.Increment(ref _observedTicks);
                    while (_waiters.TryDequeue(out var waiter))
                    {
                        waiter.TrySetResult();
                    }

                    yield return DateTime.UtcNow.AddTicks(observed);
                }
            }
        }
    }
}
