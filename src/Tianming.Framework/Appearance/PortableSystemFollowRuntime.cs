namespace TM.Framework.Appearance;

public sealed class PortableSystemFollowRuntime : IAsyncDisposable
{
    private readonly PortableSystemFollowSettings _settings;
    private readonly IPortableSystemAppearanceMonitor _monitor;
    private readonly Func<PortableSystemThemeSnapshot, CancellationToken, Task<PortableSystemFollowDecision>> _handleAppearanceChangedAsync;
    private readonly Func<PortableSystemFollowSettings, CancellationToken, Task> _saveSettingsAsync;
    private readonly object _lock = new();
    private bool _subscribed;

    public PortableSystemFollowRuntime(
        PortableSystemFollowSettings settings,
        IPortableSystemAppearanceMonitor monitor,
        Func<PortableSystemThemeSnapshot, CancellationToken, Task<PortableSystemFollowDecision>> handleAppearanceChangedAsync,
        Func<PortableSystemFollowSettings, CancellationToken, Task>? saveSettingsAsync = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _handleAppearanceChangedAsync = handleAppearanceChangedAsync
            ?? throw new ArgumentNullException(nameof(handleAppearanceChangedAsync));
        _saveSettingsAsync = saveSettingsAsync ?? ((_, _) => Task.CompletedTask);
    }

    public bool IsRunning => _monitor.IsRunning;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Subscribe();
        if (_settings.Enabled && _settings.AutoStart)
        {
            await _monitor.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        Subscribe();
        _settings.Enabled = true;
        await _saveSettingsAsync(_settings, cancellationToken).ConfigureAwait(false);
        await _monitor.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        _settings.Enabled = false;
        await _saveSettingsAsync(_settings, cancellationToken).ConfigureAwait(false);
        await _monitor.StopAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        Unsubscribe();
        await _monitor.StopAsync().ConfigureAwait(false);
    }

    private void Subscribe()
    {
        lock (_lock)
        {
            if (_subscribed)
            {
                return;
            }

            _monitor.AppearanceChanged += OnAppearanceChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        lock (_lock)
        {
            if (!_subscribed)
            {
                return;
            }

            _monitor.AppearanceChanged -= OnAppearanceChanged;
            _subscribed = false;
        }
    }

    private void OnAppearanceChanged(object? sender, MacOSSystemAppearanceChangedEventArgs args)
    {
        _ = _handleAppearanceChangedAsync(args.CurrentSnapshot, CancellationToken.None);
    }
}
