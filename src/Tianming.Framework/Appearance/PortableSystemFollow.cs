using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace TM.Framework.Appearance;

public enum PortableHighContrastBehavior
{
    Ignore,
    UseLight,
    UseDark,
    Custom
}

public enum PortableSystemFollowDecisionStatus
{
    Switch,
    Disabled,
    AlreadyCurrent,
    ExclusionSuppressed,
    SceneSuppressed,
    MinIntervalSuppressed
}

public sealed class PortableSystemFollowExclusionPeriod
{
    [JsonPropertyName("StartTime")] public TimeSpan StartTime { get; set; }

    [JsonPropertyName("EndTime")] public TimeSpan EndTime { get; set; }

    [JsonPropertyName("Days")] public PortableWeekday Days { get; set; }

    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
}

public sealed class PortableSystemFollowSceneRule
{
    [JsonPropertyName("SceneName")] public string SceneName { get; set; } = string.Empty;

    [JsonPropertyName("StartTime")] public TimeSpan StartTime { get; set; }

    [JsonPropertyName("EndTime")] public TimeSpan EndTime { get; set; }

    [JsonPropertyName("DisableSwitching")] public bool DisableSwitching { get; set; }

    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;

    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
}

public sealed class PortableApplicationSceneRule
{
    public string SceneName { get; set; } = string.Empty;

    public List<string> ProcessKeywords { get; set; } = [];

    public bool IgnoreCase { get; set; } = true;
}

public sealed class PortableSystemFollowSettings
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; }

    [JsonPropertyName("AutoStart")] public bool AutoStart { get; set; } = true;

    [JsonPropertyName("LightThemeMapping")] public PortableThemeType LightThemeMapping { get; set; } = PortableThemeType.Light;

    [JsonPropertyName("DarkThemeMapping")] public PortableThemeType DarkThemeMapping { get; set; } = PortableThemeType.Dark;

    [JsonPropertyName("HighContrastMapping")] public PortableHighContrastBehavior HighContrastMapping { get; set; } = PortableHighContrastBehavior.Ignore;

    [JsonPropertyName("HighContrastCustomTheme")] public PortableThemeType HighContrastCustomTheme { get; set; } = PortableThemeType.Dark;

    [JsonPropertyName("DelaySeconds")] public int DelaySeconds { get; set; } = 3;

    [JsonPropertyName("ShowNotification")] public bool ShowNotification { get; set; } = true;

    [JsonPropertyName("EnableAccentColor")] public bool EnableAccentColor { get; set; }

    [JsonPropertyName("OnlyWhenNotManual")] public bool OnlyWhenNotManual { get; set; }

    [JsonPropertyName("ExclusionPeriods")] public List<PortableSystemFollowExclusionPeriod> ExclusionPeriods { get; set; } = [];

    [JsonPropertyName("MinSwitchInterval")] public int MinSwitchInterval { get; set; } = 30;

    [JsonPropertyName("DebounceDelay")] public int DebounceDelay { get; set; } = 5;

    [JsonPropertyName("EnableSmartDelay")] public bool EnableSmartDelay { get; set; } = true;

    [JsonPropertyName("EnableSceneDetection")] public bool EnableSceneDetection { get; set; }

    [JsonPropertyName("SceneRules")] public List<PortableSystemFollowSceneRule> SceneRules { get; set; } = [];

    [JsonPropertyName("Priority")] public int Priority { get; set; } = 5;

    [JsonPropertyName("LastSwitchTime")] public DateTime? LastSwitchTime { get; set; }

    [JsonPropertyName("TotalSwitchCount")] public int TotalSwitchCount { get; set; }

    [JsonPropertyName("LastDetectedTheme")] public string LastDetectedTheme { get; set; } = "未知";

    [JsonPropertyName("EnableVerboseLog")] public bool EnableVerboseLog { get; set; }

    public static PortableSystemFollowSettings CreateDefault()
    {
        return new PortableSystemFollowSettings
        {
            Enabled = false,
            AutoStart = true,
            LightThemeMapping = PortableThemeType.Light,
            DarkThemeMapping = PortableThemeType.Dark,
            HighContrastMapping = PortableHighContrastBehavior.Ignore,
            HighContrastCustomTheme = PortableThemeType.Dark,
            DelaySeconds = 3,
            ShowNotification = true,
            EnableAccentColor = false,
            OnlyWhenNotManual = false,
            ExclusionPeriods = [],
            MinSwitchInterval = 30,
            DebounceDelay = 5,
            EnableSmartDelay = true,
            EnableSceneDetection = false,
            SceneRules = [],
            Priority = 5,
            LastSwitchTime = null,
            TotalSwitchCount = 0,
            LastDetectedTheme = "未知",
            EnableVerboseLog = false
        };
    }
}

public sealed record PortableSystemThemeSnapshot(
    bool IsLightTheme,
    bool IsHighContrast,
    string? AccentColor)
{
    public string DisplayName => IsHighContrast
        ? "高对比度模式"
        : IsLightTheme ? "浅色主题" : "深色主题";
}

public sealed record PortableDetectedScene(
    string SceneName,
    bool IsActive,
    bool DisableSwitching,
    string Description,
    TimeSpan StartTime,
    TimeSpan EndTime)
{
    public static PortableDetectedScene Default { get; } = new(
        "默认",
        IsActive: false,
        DisableSwitching: false,
        string.Empty,
        TimeSpan.Zero,
        TimeSpan.Zero);
}

public sealed record PortableSystemFollowDecision(
    PortableSystemFollowDecisionStatus Status,
    PortableThemeType TargetTheme,
    string? ActiveSceneName = null);

public static class PortableSystemFollowSceneDetector
{
    public static PortableDetectedScene DetectCurrentScene(
        IEnumerable<PortableSystemFollowSceneRule>? rules,
        TimeSpan now)
    {
        if (rules is null)
        {
            return PortableDetectedScene.Default;
        }

        foreach (var rule in rules.Where(rule => rule.Enabled))
        {
            if (!IsTimeInRange(now, rule.StartTime, rule.EndTime))
            {
                continue;
            }

            return new PortableDetectedScene(
                string.IsNullOrWhiteSpace(rule.SceneName) ? "默认" : rule.SceneName,
                IsActive: true,
                rule.DisableSwitching,
                rule.Description,
                rule.StartTime,
                rule.EndTime);
        }

        return PortableDetectedScene.Default;
    }

    internal static bool IsTimeInRange(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        return start <= end
            ? current >= start && current <= end
            : current >= start || current <= end;
    }
}

public static class PortableSystemFollowApplicationSceneClassifier
{
    private static readonly string[] WorkApps =
    [
        "WINWORD",
        "EXCEL",
        "POWERPNT",
        "Code",
        "devenv",
        "Rider",
        "Visual Studio Code",
        "Microsoft Word",
        "Microsoft Excel",
        "Pages",
        "Numbers",
        "Keynote"
    ];

    private static readonly string[] EntertainmentApps =
    [
        "spotify",
        "netflix",
        "vlc",
        "potplayer",
        "music",
        "tv"
    ];

    private static readonly string[] PresentationApps =
    [
        "POWERPNT",
        "Zoom",
        "Teams",
        "Microsoft Teams"
    ];

    public static string Classify(
        IEnumerable<string>? runningApplications,
        IEnumerable<PortableApplicationSceneRule>? customRules = null)
    {
        var apps = (runningApplications ?? [])
            .Where(app => !string.IsNullOrWhiteSpace(app))
            .ToList();

        foreach (var rule in customRules ?? [])
        {
            if (string.IsNullOrWhiteSpace(rule.SceneName) || rule.ProcessKeywords.Count == 0)
            {
                continue;
            }

            var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (apps.Any(app => rule.ProcessKeywords.Any(keyword => app.Contains(keyword, comparison))))
            {
                return rule.SceneName;
            }
        }

        if (apps.Any(app => WorkApps.Any(work => app.Contains(work, StringComparison.Ordinal))))
        {
            return "工作中";
        }

        if (apps.Any(app => EntertainmentApps.Any(entertainment =>
                app.Contains(entertainment, StringComparison.OrdinalIgnoreCase))))
        {
            return "娱乐中";
        }

        if (apps.Any(app => PresentationApps.Any(presentation =>
                app.Contains(presentation, StringComparison.Ordinal))))
        {
            return "演示中";
        }

        return "默认";
    }
}

public static class PortableSystemFollowPolicy
{
    public static PortableSystemFollowDecision EvaluateSwitch(
        PortableSystemThemeSnapshot snapshot,
        PortableSystemFollowSettings settings,
        PortableThemeType currentTheme,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var targetTheme = DetermineTargetTheme(snapshot, settings, currentTheme);
        if (!settings.Enabled)
        {
            return new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.Disabled, targetTheme);
        }

        if (IsInExclusionPeriod(settings, now))
        {
            return new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.ExclusionSuppressed, targetTheme);
        }

        if (settings.EnableSceneDetection)
        {
            var scene = PortableSystemFollowSceneDetector.DetectCurrentScene(settings.SceneRules, now.TimeOfDay);
            if (scene.IsActive && scene.DisableSwitching)
            {
                return new PortableSystemFollowDecision(
                    PortableSystemFollowDecisionStatus.SceneSuppressed,
                    targetTheme,
                    scene.SceneName);
            }
        }

        if (settings.EnableSmartDelay && settings.LastSwitchTime is DateTime lastSwitchTime)
        {
            var secondsSinceLastSwitch = (now - lastSwitchTime).TotalSeconds;
            if (secondsSinceLastSwitch >= 0 && secondsSinceLastSwitch < settings.MinSwitchInterval)
            {
                return new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.MinIntervalSuppressed, targetTheme);
            }
        }

        return targetTheme == currentTheme
            ? new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.AlreadyCurrent, targetTheme)
            : new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.Switch, targetTheme);
    }

    public static PortableThemeType DetermineTargetTheme(
        PortableSystemThemeSnapshot snapshot,
        PortableSystemFollowSettings settings,
        PortableThemeType currentTheme)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (snapshot.IsHighContrast)
        {
            return settings.HighContrastMapping switch
            {
                PortableHighContrastBehavior.Ignore => currentTheme,
                PortableHighContrastBehavior.UseLight => PortableThemeType.Light,
                PortableHighContrastBehavior.UseDark => PortableThemeType.Dark,
                PortableHighContrastBehavior.Custom => settings.HighContrastCustomTheme,
                _ => currentTheme
            };
        }

        return snapshot.IsLightTheme ? settings.LightThemeMapping : settings.DarkThemeMapping;
    }

    public static bool IsInExclusionPeriod(PortableSystemFollowSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ExclusionPeriods.Count == 0)
        {
            return false;
        }

        var day = ToPortableWeekday(now.DayOfWeek);
        foreach (var period in settings.ExclusionPeriods)
        {
            if ((period.Days & day) != day)
            {
                continue;
            }

            if (PortableSystemFollowSceneDetector.IsTimeInRange(now.TimeOfDay, period.StartTime, period.EndTime))
            {
                return true;
            }
        }

        return false;
    }

    private static PortableWeekday ToPortableWeekday(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => PortableWeekday.Monday,
            DayOfWeek.Tuesday => PortableWeekday.Tuesday,
            DayOfWeek.Wednesday => PortableWeekday.Wednesday,
            DayOfWeek.Thursday => PortableWeekday.Thursday,
            DayOfWeek.Friday => PortableWeekday.Friday,
            DayOfWeek.Saturday => PortableWeekday.Saturday,
            DayOfWeek.Sunday => PortableWeekday.Sunday,
            _ => PortableWeekday.None
        };
    }
}

public static class MacOSSystemAppearanceParser
{
    public static PortableSystemThemeSnapshot ParseAppleInterfaceStyle(string? output)
    {
        var text = output?.Trim() ?? string.Empty;
        var isDark = text.Equals("Dark", StringComparison.OrdinalIgnoreCase);

        return new PortableSystemThemeSnapshot(
            IsLightTheme: !isDark,
            IsHighContrast: false,
            AccentColor: null);
    }
}

public sealed record MacOSAppearanceCommandRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments);

public sealed record MacOSAppearanceCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IPortableSystemAppearanceProbe
{
    Task<PortableSystemThemeSnapshot> DetectAsync(CancellationToken cancellationToken = default);
}

public sealed class MacOSSystemAppearanceProbe : IPortableSystemAppearanceProbe
{
    private static readonly MacOSAppearanceCommandRequest DefaultsRequest = new(
        "/usr/bin/defaults",
        ["read", "-g", "AppleInterfaceStyle"]);

    private readonly Func<MacOSAppearanceCommandRequest, CancellationToken, Task<MacOSAppearanceCommandResult>> _runner;

    public MacOSSystemAppearanceProbe(
        Func<MacOSAppearanceCommandRequest, CancellationToken, Task<MacOSAppearanceCommandResult>>? runner = null)
    {
        _runner = runner ?? RunDefaultsAsync;
    }

    public async Task<PortableSystemThemeSnapshot> DetectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner(DefaultsRequest, cancellationToken).ConfigureAwait(false);
            var output = string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardError
                : result.StandardOutput;

            return MacOSSystemAppearanceParser.ParseAppleInterfaceStyle(output);
        }
        catch (Exception)
        {
            return MacOSSystemAppearanceParser.ParseAppleInterfaceStyle(string.Empty);
        }
    }

    private static async Task<MacOSAppearanceCommandResult> RunDefaultsAsync(
        MacOSAppearanceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new MacOSAppearanceCommandResult(
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }
}

public static class MacOSRunningApplicationParser
{
    public static IReadOnlyList<string> ParseApplicationNames(string? output)
    {
        return (output ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed record MacOSSystemAppearanceChangedEventArgs(
    PortableSystemThemeSnapshot PreviousSnapshot,
    PortableSystemThemeSnapshot CurrentSnapshot,
    DateTime DetectedAt,
    TimeSpan DetectionDuration);

public interface IPortableSystemAppearanceMonitor : IAsyncDisposable
{
    event EventHandler<MacOSSystemAppearanceChangedEventArgs>? AppearanceChanged;

    bool IsRunning { get; }

    PortableSystemThemeSnapshot? LastSnapshot { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public sealed class MacOSSystemAppearanceMonitor : IPortableSystemAppearanceMonitor
{
    private readonly IPortableSystemAppearanceProbe _probe;
    private readonly IPortableTimerTickSource _tickSource;
    private readonly TimeSpan _pollInterval;
    private readonly object _lock = new();
    private CancellationTokenSource? _stopSource;
    private Task? _loopTask;

    public MacOSSystemAppearanceMonitor(
        IPortableSystemAppearanceProbe? probe = null,
        TimeSpan? pollInterval = null,
        IPortableTimerTickSource? tickSource = null)
    {
        _probe = probe ?? new MacOSSystemAppearanceProbe();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(3);
        if (_pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Appearance poll interval must be positive.");
        }

        _tickSource = tickSource ?? new PortablePeriodicTimerTickSource();
    }

    public event EventHandler<MacOSSystemAppearanceChangedEventArgs>? AppearanceChanged;

    public bool IsRunning { get; private set; }

    public PortableSystemThemeSnapshot? LastSnapshot { get; private set; }

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
        }

        LastSnapshot = await _probe.DetectAsync(cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _loopTask = RunLoopAsync(_stopSource!.Token);
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
        await foreach (var _ in _tickSource.WaitForTicksAsync(_pollInterval, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await DetectAndRaiseIfChangedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DetectAndRaiseIfChangedAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var current = await _probe.DetectAsync(cancellationToken).ConfigureAwait(false);
        var duration = DateTime.UtcNow - startedAt;
        var previous = LastSnapshot;

        if (previous is null)
        {
            LastSnapshot = current;
            return;
        }

        if (previous == current)
        {
            return;
        }

        LastSnapshot = current;
        AppearanceChanged?.Invoke(
            this,
            new MacOSSystemAppearanceChangedEventArgs(previous, current, DateTime.UtcNow, duration));
    }
}

public sealed record MacOSRunningApplicationCommandRequest(
    string ExecutablePath,
    string Script);

public sealed record MacOSRunningApplicationCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed class MacOSRunningApplicationProbe
{
    private const string RunningApplicationsScript =
        "tell application \"System Events\" to get the name of every process whose background only is false";

    private static readonly MacOSRunningApplicationCommandRequest DefaultRequest = new(
        "/usr/bin/osascript",
        RunningApplicationsScript);

    private readonly Func<MacOSRunningApplicationCommandRequest, CancellationToken, Task<MacOSRunningApplicationCommandResult>> _runner;

    public MacOSRunningApplicationProbe(
        Func<MacOSRunningApplicationCommandRequest, CancellationToken, Task<MacOSRunningApplicationCommandResult>>? runner = null)
    {
        _runner = runner ?? RunOsascriptAsync;
    }

    public async Task<IReadOnlyList<string>> GetRunningApplicationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _runner(DefaultRequest, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                return [];
            }

            return MacOSRunningApplicationParser.ParseApplicationNames(result.StandardOutput);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<MacOSRunningApplicationCommandResult> RunOsascriptAsync(
        MacOSRunningApplicationCommandRequest request,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(request.Script);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new MacOSRunningApplicationCommandResult(
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }
}

public sealed class PortableSystemThemeChangeHistory
{
    private const int MaxHistoryCount = 20;
    private readonly object _lock = new();
    private readonly List<PortableThemeChangeRecord> _changeHistory = [];

    public void AddChangeRecord(
        string fromTheme,
        string toTheme,
        TimeSpan duration)
    {
        AddChangeRecord(fromTheme, toTheme, duration, DateTime.Now);
    }

    public void AddChangeRecord(
        string fromTheme,
        string toTheme,
        TimeSpan duration,
        DateTime timestamp)
    {
        var record = new PortableThemeChangeRecord
        {
            Timestamp = timestamp,
            FromTheme = fromTheme,
            ToTheme = toTheme,
            Duration = duration,
            Details = $"{fromTheme} -> {toTheme}"
        };

        lock (_lock)
        {
            _changeHistory.Insert(0, record);
            if (_changeHistory.Count > MaxHistoryCount)
            {
                _changeHistory.RemoveRange(MaxHistoryCount, _changeHistory.Count - MaxHistoryCount);
            }
        }
    }

    public List<PortableThemeChangeRecord> GetChangeHistory()
    {
        lock (_lock)
        {
            return _changeHistory
                .Select(record => record.Clone())
                .ToList();
        }
    }
}

public sealed class PortableThemeChangeRecord
{
    public DateTime Timestamp { get; set; }

    public string FromTheme { get; set; } = string.Empty;

    public string ToTheme { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public string Details { get; set; } = string.Empty;

    public string DisplayText => $"{Timestamp:HH:mm:ss} - {Details} (耗时: {Duration.TotalMilliseconds:F0}ms)";

    public PortableThemeChangeRecord Clone()
    {
        return new PortableThemeChangeRecord
        {
            Timestamp = Timestamp,
            FromTheme = FromTheme,
            ToTheme = ToTheme,
            Duration = Duration,
            Details = Details
        };
    }
}

public sealed class FileSystemFollowSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileSystemFollowSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("System follow settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableSystemFollowSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableSystemFollowSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableSystemFollowSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableSystemFollowSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableSystemFollowSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableSystemFollowSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableSystemFollowSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
