using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Microsoft.Extensions.Logging.Abstractions;
using Tianming.Desktop.Avalonia.Theme;
using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class MacOSAppearanceBridgeTests
{
    [AvaloniaFact]
    public async Task System_appearance_dark_event_switches_application_to_dark()
    {
        var app = Application.Current ?? new TestApp();
        app.RequestedThemeVariant = ThemeVariant.Light;

        var bridge = new ThemeBridge(NullLogger<ThemeBridge>.Instance);
        var state = new PortableThemeState();
        var controller = new PortableThemeStateController(state, bridge.ApplyAsync);
        var settings = PortableSystemFollowSettings.CreateDefault();
        settings.Enabled = true;
        settings.AutoStart = true;
        settings.DelaySeconds = 0;
        settings.ShowNotification = false;
        settings.EnableSmartDelay = false;

        var monitor = new FakeAppearanceMonitor();
        var followController = new PortableSystemFollowController(
            settings,
            () => controller.CurrentTheme,
            async (theme, ct) =>
            {
                await controller.SwitchThemeAsync(theme, cancellationToken: ct);
            });
        var runtime = new PortableSystemFollowRuntime(
            settings,
            monitor,
            followController.HandleAppearanceChangedAsync);

        await runtime.InitializeAsync();
        monitor.Raise(
            new PortableSystemThemeSnapshot(true, false, null),
            new PortableSystemThemeSnapshot(false, false, null));

        await WaitUntilAsync(() => app.RequestedThemeVariant == ThemeVariant.Dark);

        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
        await runtime.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!predicate())
        {
            if (cts.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(10);
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

        public void Raise(PortableSystemThemeSnapshot previous, PortableSystemThemeSnapshot current)
        {
            LastSnapshot = current;
            AppearanceChanged?.Invoke(
                this,
                new MacOSSystemAppearanceChangedEventArgs(
                    previous,
                    current,
                    DateTime.UtcNow,
                    TimeSpan.Zero));
        }
    }
}
