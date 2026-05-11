namespace TM.Framework.Logging;

public sealed class PortableLogOutputSettingsController
{
    private readonly FileLogOutputSettingsStore _settingsStore;
    private readonly PortableLogOutputTestRunner _testRunner;
    private readonly PortableLogOutputTelemetryStore _telemetryStore;

    public PortableLogOutputSettingsController(
        FileLogOutputSettingsStore settingsStore,
        PortableLogOutputTestRunner testRunner,
        PortableLogOutputTelemetryStore telemetryStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _testRunner = testRunner ?? throw new ArgumentNullException(nameof(testRunner));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
    }

    public PortableLogOutputSettings Settings { get; private set; } = PortableLogOutputSettings.CreateDefault();

    public async Task<PortableLogOutputSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return Settings;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return _settingsStore.SaveAsync(Settings, cancellationToken);
    }

    public PortableLogOutputTarget? AddTarget(
        string name,
        PortableLogOutputTargetType type = PortableLogOutputTargetType.File,
        IReadOnlyDictionary<string, string>? targetSettings = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var target = new PortableLogOutputTarget
        {
            Name = name.Trim(),
            Type = type,
            IsEnabled = true,
            Priority = Settings.OutputTargets.Count,
            Settings = targetSettings is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(targetSettings, StringComparer.OrdinalIgnoreCase)
        };
        Settings.OutputTargets.Add(target);
        return target;
    }

    public bool RemoveTarget(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var target = Settings.OutputTargets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        return Settings.OutputTargets.Remove(target);
    }

    public Task<IReadOnlyList<PortableLogOutputTestResult>> TestAllOutputsAsync(
        string testContent,
        CancellationToken cancellationToken = default)
    {
        return _testRunner.TestAllAsync(Settings, testContent, cancellationToken);
    }

    public async Task ClearTelemetryAsync(CancellationToken cancellationToken = default)
    {
        await _telemetryStore.ResetStatisticsAsync(cancellationToken).ConfigureAwait(false);
        await _telemetryStore.ClearFailuresAsync(cancellationToken).ConfigureAwait(false);
    }
}
