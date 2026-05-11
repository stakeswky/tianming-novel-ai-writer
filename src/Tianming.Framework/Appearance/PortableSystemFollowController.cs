namespace TM.Framework.Appearance;

public sealed record PortableSystemFollowSwitchNotification(
    PortableThemeType FromTheme,
    PortableThemeType ToTheme,
    string Message,
    DateTime Timestamp);

public sealed class PortableSystemFollowController
{
    private readonly PortableSystemFollowSettings _settings;
    private readonly Func<PortableThemeType> _getCurrentTheme;
    private readonly Func<PortableThemeType, CancellationToken, Task> _switchThemeAsync;
    private readonly Func<PortableSystemFollowSettings, CancellationToken, Task> _saveSettingsAsync;
    private readonly Func<PortableSystemFollowSwitchNotification, CancellationToken, Task> _notifyAsync;
    private readonly Func<DateTime> _clock;

    public PortableSystemFollowController(
        PortableSystemFollowSettings settings,
        Func<PortableThemeType> getCurrentTheme,
        Func<PortableThemeType, CancellationToken, Task> switchThemeAsync,
        Func<PortableSystemFollowSettings, CancellationToken, Task>? saveSettingsAsync = null,
        Func<PortableSystemFollowSwitchNotification, CancellationToken, Task>? notifyAsync = null,
        Func<DateTime>? clock = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _getCurrentTheme = getCurrentTheme ?? throw new ArgumentNullException(nameof(getCurrentTheme));
        _switchThemeAsync = switchThemeAsync ?? throw new ArgumentNullException(nameof(switchThemeAsync));
        _saveSettingsAsync = saveSettingsAsync ?? ((_, _) => Task.CompletedTask);
        _notifyAsync = notifyAsync ?? ((_, _) => Task.CompletedTask);
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableSystemFollowDecision> HandleAppearanceChangedAsync(
        PortableSystemThemeSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var now = _clock();
        var currentTheme = _getCurrentTheme();
        var decision = PortableSystemFollowPolicy.EvaluateSwitch(snapshot, _settings, currentTheme, now);
        _settings.LastDetectedTheme = snapshot.DisplayName;

        if (decision.Status != PortableSystemFollowDecisionStatus.Switch)
        {
            return decision;
        }

        try
        {
            if (_settings.DelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.DelaySeconds), cancellationToken)
                    .ConfigureAwait(false);
            }

            await _switchThemeAsync(decision.TargetTheme, cancellationToken).ConfigureAwait(false);

            _settings.LastSwitchTime = now;
            _settings.TotalSwitchCount++;
            await _saveSettingsAsync(_settings, cancellationToken).ConfigureAwait(false);

            if (_settings.ShowNotification)
            {
                await _notifyAsync(
                    new PortableSystemFollowSwitchNotification(
                        currentTheme,
                        decision.TargetTheme,
                        $"已切换到 {decision.TargetTheme} 主题",
                        now),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await _saveSettingsAsync(_settings, cancellationToken).ConfigureAwait(false);
        }

        return decision;
    }
}
