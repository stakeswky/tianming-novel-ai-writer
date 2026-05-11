using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemFollowRuntimeTests
{
    [Fact]
    public async Task InitializeAsync_starts_monitor_when_enabled_and_autostart()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.AutoStart = true;
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var runtime = new PortableSystemFollowRuntime(settings, monitor, controller.HandleAppearanceChangedAsync);

        await runtime.InitializeAsync();

        Assert.True(monitor.IsRunning);
        Assert.True(runtime.IsRunning);
    }

    [Fact]
    public async Task InitializeAsync_does_not_start_monitor_when_autostart_disabled()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.AutoStart = false;
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var runtime = new PortableSystemFollowRuntime(settings, monitor, controller.HandleAppearanceChangedAsync);

        await runtime.InitializeAsync();

        Assert.False(monitor.IsRunning);
        Assert.False(runtime.IsRunning);
    }

    [Fact]
    public async Task EnableAsync_sets_enabled_saves_and_starts_monitor()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var saves = 0;
        var runtime = new PortableSystemFollowRuntime(
            settings,
            monitor,
            controller.HandleAppearanceChangedAsync,
            (_, _) =>
            {
                saves++;
                return Task.CompletedTask;
            });

        await runtime.EnableAsync();

        Assert.True(settings.Enabled);
        Assert.True(monitor.IsRunning);
        Assert.True(runtime.IsRunning);
        Assert.Equal(1, saves);
    }

    [Fact]
    public async Task DisableAsync_sets_disabled_saves_and_stops_monitor()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var saves = 0;
        var runtime = new PortableSystemFollowRuntime(
            settings,
            monitor,
            controller.HandleAppearanceChangedAsync,
            (_, _) =>
            {
                saves++;
                return Task.CompletedTask;
            });

        await runtime.InitializeAsync();
        await runtime.DisableAsync();

        Assert.False(settings.Enabled);
        Assert.False(monitor.IsRunning);
        Assert.False(runtime.IsRunning);
        Assert.Equal(1, saves);
    }

    [Fact]
    public async Task AppearanceChanged_event_is_forwarded_to_controller()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var runtime = new PortableSystemFollowRuntime(settings, monitor, controller.HandleAppearanceChangedAsync);

        await runtime.InitializeAsync();
        monitor.RaiseAppearanceChanged(
            new PortableSystemThemeSnapshot(true, false, null),
            new PortableSystemThemeSnapshot(false, false, null));
        await controller.WaitForHandledCountAsync(1);

        Assert.Equal("深色主题", Assert.Single(controller.HandledSnapshots).DisplayName);
    }

    [Fact]
    public async Task DisposeAsync_unsubscribes_from_monitor_events()
    {
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        var monitor = new FakeAppearanceMonitor();
        var controller = new RecordingSystemFollowController();
        var runtime = new PortableSystemFollowRuntime(settings, monitor, controller.HandleAppearanceChangedAsync);

        await runtime.InitializeAsync();
        await runtime.DisposeAsync();
        monitor.RaiseAppearanceChanged(
            new PortableSystemThemeSnapshot(true, false, null),
            new PortableSystemThemeSnapshot(false, false, null));
        await Task.Delay(25);

        Assert.Empty(controller.HandledSnapshots);
        Assert.False(monitor.IsRunning);
    }

    private sealed class RecordingSystemFollowController
    {
        private readonly TaskCompletionSource _handled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<PortableSystemThemeSnapshot> HandledSnapshots { get; } = [];

        public Task<PortableSystemFollowDecision> HandleAppearanceChangedAsync(
            PortableSystemThemeSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            HandledSnapshots.Add(snapshot);
            _handled.TrySetResult();
            return Task.FromResult(new PortableSystemFollowDecision(
                PortableSystemFollowDecisionStatus.Switch,
                PortableThemeType.Dark));
        }

        public async Task WaitForHandledCountAsync(int expectedCount)
        {
            if (HandledSnapshots.Count >= expectedCount)
            {
                return;
            }

            await _handled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    private sealed class FakeAppearanceMonitor : IPortableSystemAppearanceMonitor
    {
        public event EventHandler<MacOSSystemAppearanceChangedEventArgs>? AppearanceChanged;

        public bool IsRunning { get; private set; }

        public PortableSystemThemeSnapshot? LastSnapshot { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        public void RaiseAppearanceChanged(
            PortableSystemThemeSnapshot previous,
            PortableSystemThemeSnapshot current)
        {
            LastSnapshot = current;
            AppearanceChanged?.Invoke(
                this,
                new MacOSSystemAppearanceChangedEventArgs(
                    previous,
                    current,
                    DateTime.UtcNow,
                    TimeSpan.FromMilliseconds(1)));
        }
    }
}
