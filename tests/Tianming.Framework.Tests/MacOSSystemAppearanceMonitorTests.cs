using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSSystemAppearanceMonitorTests
{
    [Fact]
    public async Task StartAsync_captures_initial_snapshot_without_emitting_change()
    {
        var monitor = new MacOSSystemAppearanceMonitor(
            new QueueAppearanceProbe(
                new PortableSystemThemeSnapshot(true, false, null)),
            tickSource: new ManualTickSource());
        var events = new List<MacOSSystemAppearanceChangedEventArgs>();
        monitor.AppearanceChanged += (_, args) => events.Add(args);

        await monitor.StartAsync();

        Assert.True(monitor.IsRunning);
        Assert.Equal("浅色主题", monitor.LastSnapshot?.DisplayName);
        Assert.Empty(events);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task Tick_emits_change_when_snapshot_differs_from_baseline()
    {
        var ticks = new ManualTickSource();
        var probe = new QueueAppearanceProbe(
            new PortableSystemThemeSnapshot(true, false, null),
            new PortableSystemThemeSnapshot(false, false, null));
        var monitor = new MacOSSystemAppearanceMonitor(probe, tickSource: ticks);
        var changed = new TaskCompletionSource<MacOSSystemAppearanceChangedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.AppearanceChanged += (_, args) => changed.TrySetResult(args);

        await monitor.StartAsync();
        await ticks.TickAsync();
        var args = await changed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("浅色主题", args.PreviousSnapshot.DisplayName);
        Assert.Equal("深色主题", args.CurrentSnapshot.DisplayName);
        Assert.Equal("深色主题", monitor.LastSnapshot?.DisplayName);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task Tick_ignores_unchanged_snapshot()
    {
        var ticks = new ManualTickSource();
        var probe = new QueueAppearanceProbe(
            new PortableSystemThemeSnapshot(true, false, "#ABCDEF"),
            new PortableSystemThemeSnapshot(true, false, "#ABCDEF"));
        var monitor = new MacOSSystemAppearanceMonitor(probe, tickSource: ticks);
        var events = 0;
        monitor.AppearanceChanged += (_, _) => events++;

        await monitor.StartAsync();
        await ticks.TickAsync();
        await Task.Delay(25);

        Assert.Equal(0, events);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task StopAsync_cancels_polling_loop()
    {
        var ticks = new ManualTickSource();
        var probe = new QueueAppearanceProbe(
            new PortableSystemThemeSnapshot(true, false, null),
            new PortableSystemThemeSnapshot(false, false, null));
        var monitor = new MacOSSystemAppearanceMonitor(probe, tickSource: ticks);
        var events = 0;
        monitor.AppearanceChanged += (_, _) => events++;

        await monitor.StartAsync();
        await monitor.StopAsync();
        await ticks.TickAsync();
        await Task.Delay(25);

        Assert.False(monitor.IsRunning);
        Assert.Equal(0, events);
    }

    private sealed class QueueAppearanceProbe(params PortableSystemThemeSnapshot[] snapshots)
        : IPortableSystemAppearanceProbe
    {
        private readonly Queue<PortableSystemThemeSnapshot> _snapshots = new(snapshots);
        private PortableSystemThemeSnapshot? _lastSnapshot;

        public Task<PortableSystemThemeSnapshot> DetectAsync(CancellationToken cancellationToken = default)
        {
            _lastSnapshot = _snapshots.Count == 0 ? _lastSnapshot : _snapshots.Dequeue();
            return Task.FromResult(_lastSnapshot ?? new PortableSystemThemeSnapshot(true, false, null));
        }
    }

    private sealed class ManualTickSource : IPortableTimerTickSource
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();

        public async Task TickAsync()
        {
            await _ticks.Writer.WriteAsync(true);
        }

        public async IAsyncEnumerable<DateTime> WaitForTicksAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _ticks.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_ticks.Reader.TryRead(out _))
                {
                    yield return DateTime.UtcNow;
                }
            }
        }
    }
}
