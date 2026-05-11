namespace TM.Framework.Appearance;

public sealed class PortableTimeScheduleController
{
    private readonly PortableTimeBasedThemeSettings _settings;
    private readonly Func<PortableThemeType> _getCurrentTheme;
    private readonly Func<PortableThemeType, CancellationToken, Task> _switchThemeAsync;
    private readonly Func<PortableTimeBasedThemeSettings, CancellationToken, Task> _saveSettingsAsync;
    private readonly Func<DateTime> _clock;
    private readonly Func<DateTime, bool>? _builtInHolidayChecker;
    private readonly Func<DateTime, double, double, (TimeSpan Sunrise, TimeSpan Sunset)>? _sunTimesProvider;

    public PortableTimeScheduleController(
        PortableTimeBasedThemeSettings settings,
        Func<PortableThemeType> getCurrentTheme,
        Func<PortableThemeType, CancellationToken, Task> switchThemeAsync,
        Func<PortableTimeBasedThemeSettings, CancellationToken, Task>? saveSettingsAsync = null,
        Func<DateTime>? clock = null,
        Func<DateTime, bool>? builtInHolidayChecker = null,
        Func<DateTime, double, double, (TimeSpan Sunrise, TimeSpan Sunset)>? sunTimesProvider = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _getCurrentTheme = getCurrentTheme ?? throw new ArgumentNullException(nameof(getCurrentTheme));
        _switchThemeAsync = switchThemeAsync ?? throw new ArgumentNullException(nameof(switchThemeAsync));
        _saveSettingsAsync = saveSettingsAsync ?? ((_, _) => Task.CompletedTask);
        _clock = clock ?? (() => DateTime.Now);
        _builtInHolidayChecker = builtInHolidayChecker;
        _sunTimesProvider = sunTimesProvider;
    }

    public bool IsRunning { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_settings.Enabled && !_settings.TemporaryDisabled)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        await CheckAndSwitchAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public async Task CheckAndSwitchAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var now = _clock();
        if (_settings.TemporaryDisabled)
        {
            if (!_settings.DisabledUntil.HasValue || now < _settings.DisabledUntil.Value)
            {
                return;
            }

            _settings.TemporaryDisabled = false;
            _settings.DisabledUntil = null;
            await SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        var decision = PortableThemeScheduleService.CalculateTargetTheme(
            _settings,
            now,
            _builtInHolidayChecker,
            _sunTimesProvider);

        if (decision.Status != PortableThemeScheduleDecisionStatus.Switch || !decision.TargetTheme.HasValue)
        {
            return;
        }

        var targetTheme = decision.TargetTheme.Value;
        if (_getCurrentTheme() == targetTheme)
        {
            return;
        }

        try
        {
            await _switchThemeAsync(targetTheme, cancellationToken).ConfigureAwait(false);
            PortableThemeScheduleHistoryRecorder.RecordSwitch(
                _settings,
                now,
                "定时调度",
                targetTheme,
                success: true);
        }
        catch
        {
            PortableThemeScheduleHistoryRecorder.RecordSwitch(
                _settings,
                now,
                "定时调度",
                targetTheme,
                success: false);
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return _saveSettingsAsync(_settings, cancellationToken);
    }
}
