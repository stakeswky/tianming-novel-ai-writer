using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Notifications;

public sealed class DoNotDisturbSettingsData
{
    [JsonPropertyName("IsEnabled")] public bool IsEnabled { get; set; }
    [JsonPropertyName("StartTime")] public TimeSpan StartTime { get; set; } = new(22, 0, 0);
    [JsonPropertyName("EndTime")] public TimeSpan EndTime { get; set; } = new(8, 0, 0);
    [JsonPropertyName("AllowUrgentNotifications")] public bool AllowUrgentNotifications { get; set; } = true;
    [JsonPropertyName("AutoEnableInFullscreen")] public bool AutoEnableInFullscreen { get; set; }
    [JsonPropertyName("ExceptionApps")] public List<string> ExceptionApps { get; set; } = new();

    public static DoNotDisturbSettingsData CreateDefault()
    {
        return new DoNotDisturbSettingsData
        {
            IsEnabled = false,
            StartTime = new TimeSpan(22, 0, 0),
            EndTime = new TimeSpan(8, 0, 0),
            AllowUrgentNotifications = true,
            AutoEnableInFullscreen = false,
            ExceptionApps = []
        };
    }

    public DoNotDisturbSettingsData Clone()
    {
        return new DoNotDisturbSettingsData
        {
            IsEnabled = IsEnabled,
            StartTime = StartTime,
            EndTime = EndTime,
            AllowUrgentNotifications = AllowUrgentNotifications,
            AutoEnableInFullscreen = AutoEnableInFullscreen,
            ExceptionApps = ExceptionApps.ToList()
        };
    }
}

public sealed record PortableDoNotDisturbCommandResult(string Message);

public sealed class PortableDoNotDisturbController
{
    private readonly DoNotDisturbSettingsData _settings;

    public PortableDoNotDisturbController(DoNotDisturbSettingsData settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string StatusText => _settings.IsEnabled ? "免打扰已启用" : "免打扰已关闭";

    public string StatusColor => _settings.IsEnabled ? "#4CAF50" : "#9E9E9E";

    public void Toggle()
    {
        _settings.IsEnabled = !_settings.IsEnabled;
    }

    public PortableDoNotDisturbCommandResult QuickEnable(string? duration)
    {
        _settings.IsEnabled = true;
        return new PortableDoNotDisturbCommandResult($"已启用免打扰模式：{duration}");
    }
}

public sealed class FileDoNotDisturbSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileDoNotDisturbSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Do not disturb settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<DoNotDisturbSettingsData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return DoNotDisturbSettingsData.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<DoNotDisturbSettingsData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? DoNotDisturbSettingsData.CreateDefault();
        }
        catch (JsonException)
        {
            return DoNotDisturbSettingsData.CreateDefault();
        }
        catch (IOException)
        {
            return DoNotDisturbSettingsData.CreateDefault();
        }
    }

    public async Task SaveAsync(DoNotDisturbSettingsData settings, CancellationToken cancellationToken = default)
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
            await JsonSerializer.SerializeAsync(stream, settings.Clone(), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public sealed class DoNotDisturbPolicy
{
    private readonly DoNotDisturbSettingsData _settings;

    public DoNotDisturbPolicy(DoNotDisturbSettingsData settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool ShouldBlock(
        bool isHighPriority = false,
        DateTime? now = null,
        string? sourceApp = null,
        bool isSystemFullscreen = false)
    {
        var isActive = _settings.IsEnabled ||
                       (_settings.AutoEnableInFullscreen && isSystemFullscreen);
        if (!isActive)
        {
            return false;
        }

        if (IsExceptionApp(sourceApp))
        {
            return false;
        }

        if (isHighPriority && _settings.AllowUrgentNotifications)
        {
            return false;
        }

        if (_settings.AutoEnableInFullscreen && isSystemFullscreen)
        {
            return true;
        }

        return IsWithinQuietHours((now ?? DateTime.Now).TimeOfDay);
    }

    private bool IsExceptionApp(string? sourceApp)
    {
        if (string.IsNullOrWhiteSpace(sourceApp))
        {
            return false;
        }

        return _settings.ExceptionApps.Any(app =>
            string.Equals(app, sourceApp, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsWithinQuietHours(TimeSpan time)
    {
        var start = _settings.StartTime;
        var end = _settings.EndTime;

        if (start == end)
        {
            return true;
        }

        if (start < end)
        {
            return time >= start && time < end;
        }

        return time >= start || time < end;
    }
}

public sealed record MacOSFullScreenApplicationState(
    string ApplicationName,
    bool IsFullScreen,
    string RawValue);

public sealed record MacOSFullScreenCommandRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments);

public sealed record MacOSFullScreenCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed class MacOSFullScreenApplicationDetector
{
    private const string OsaScriptPath = "/usr/bin/osascript";

    private const string FrontmostApplicationFullScreenScript = """
        tell application "System Events"
            set frontApp to first application process whose frontmost is true
            set frontAppName to name of frontApp
            set fullScreenValue to false
            try
                set fullScreenValue to value of attribute "AXFullScreen" of window 1 of frontApp
            end try
            return frontAppName & "|" & fullScreenValue
        end tell
        """;

    private readonly Func<MacOSFullScreenCommandRequest, CancellationToken, Task<MacOSFullScreenCommandResult>> _runner;

    public MacOSFullScreenApplicationDetector(
        Func<MacOSFullScreenCommandRequest, CancellationToken, Task<MacOSFullScreenCommandResult>>? runner = null)
    {
        _runner = runner ?? RunOsaScriptAsync;
    }

    public async Task<MacOSFullScreenApplicationState> DetectAsync(CancellationToken cancellationToken = default)
    {
        var request = new MacOSFullScreenCommandRequest(
            OsaScriptPath,
            ["-e", FrontmostApplicationFullScreenScript]);

        try
        {
            var result = await _runner(request, cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0
                ? ParseState(result.StandardOutput)
                : new MacOSFullScreenApplicationState(string.Empty, false, result.StandardError.Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MacOSFullScreenApplicationState(string.Empty, false, ex.Message);
        }
    }

    public static MacOSFullScreenApplicationState ParseState(string? output)
    {
        var line = (output ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
        {
            return new MacOSFullScreenApplicationState(string.Empty, false, string.Empty);
        }

        var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
        var appName = parts[0];
        var rawValue = parts.Length > 1 ? parts[1] : string.Empty;
        var isFullScreen = rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           rawValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                           rawValue.Equals("1", StringComparison.OrdinalIgnoreCase);

        return new MacOSFullScreenApplicationState(appName, isFullScreen, rawValue);
    }

    private static async Task<MacOSFullScreenCommandResult> RunOsaScriptAsync(
        MacOSFullScreenCommandRequest request,
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

        return new MacOSFullScreenCommandResult(
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }
}
