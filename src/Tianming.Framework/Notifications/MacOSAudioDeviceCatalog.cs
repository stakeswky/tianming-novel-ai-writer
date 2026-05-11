using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TM.Framework.Notifications;

public enum PortableAudioDeviceType
{
    Input,
    Output
}

public sealed class PortableAudioDeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public PortableAudioDeviceType DeviceType { get; init; }

    public bool IsDefault { get; init; }

    public string Status { get; init; } = "已连接";

    public string? Transport { get; init; }

    public string DisplayName => IsDefault ? $"{DeviceName} (默认)" : DeviceName;
}

public sealed class MacOSAudioDeviceCatalog
{
    private const string SystemProfilerToolPath = "/usr/sbin/system_profiler";
    private static readonly Regex SectionHeaderRegex = new(@"^\s{4,}(?<name>[^:]+):\s*$", RegexOptions.Compiled);
    private static readonly Regex PropertyRegex = new(@"^\s{6,}(?<key>[^:]+):\s*(?<value>.*)$", RegexOptions.Compiled);

    private readonly IMacOSAudioCommandRunner _runner;

    public MacOSAudioDeviceCatalog()
        : this(new ProcessMacOSAudioCommandRunner())
    {
    }

    public MacOSAudioDeviceCatalog(IMacOSAudioCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public IReadOnlyList<PortableAudioDeviceInfo> GetDevices()
    {
        var result = _runner.Run(SystemProfilerToolPath, ["SPAudioDataType"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("list macOS audio devices", result));
        }

        return ParseDevices(result.StandardOutput);
    }

    public static IReadOnlyList<PortableAudioDeviceInfo> ParseDevices(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var devices = new List<PortableAudioDeviceInfo>();
        string? currentName = null;
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            var sectionMatch = SectionHeaderRegex.Match(line);
            if (sectionMatch.Success)
            {
                AddCurrentDevice(devices, currentName, properties);
                currentName = sectionMatch.Groups["name"].Value.Trim();
                properties.Clear();
                continue;
            }

            if (currentName is null)
            {
                continue;
            }

            var propertyMatch = PropertyRegex.Match(line);
            if (propertyMatch.Success)
            {
                properties[propertyMatch.Groups["key"].Value.Trim()] = propertyMatch.Groups["value"].Value.Trim();
            }
        }

        AddCurrentDevice(devices, currentName, properties);
        return devices;
    }

    private static void AddCurrentDevice(
        List<PortableAudioDeviceInfo> devices,
        string? name,
        Dictionary<string, string> properties)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var hasOutput = properties.ContainsKey("Output Channels");
        var hasInput = properties.ContainsKey("Input Channels");
        if (!hasOutput && !hasInput)
        {
            return;
        }

        var type = hasInput && !hasOutput
            ? PortableAudioDeviceType.Input
            : PortableAudioDeviceType.Output;
        var isDefault = IsYes(properties.GetValueOrDefault(type == PortableAudioDeviceType.Input
            ? "Default Input Device"
            : "Default Output Device"));

        devices.Add(new PortableAudioDeviceInfo
        {
            DeviceId = BuildDeviceId(type, name),
            DeviceName = name.Trim(),
            DeviceType = type,
            IsDefault = isDefault,
            Status = "已连接",
            Transport = properties.GetValueOrDefault("Transport")
        });
    }

    private static string BuildDeviceId(PortableAudioDeviceType type, string name)
    {
        var slug = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "device";
        }

        return $"macos-{(type == PortableAudioDeviceType.Input ? "input" : "output")}-{slug}";
    }

    private static bool IsYes(string? value)
    {
        return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommandError(string action, MacOSAudioCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action}: {message}".Trim();
    }
}

public sealed class MacOSSystemVolumeController
{
    private const string OsaScriptToolPath = "/usr/bin/osascript";
    private readonly IMacOSAudioCommandRunner _runner;

    public MacOSSystemVolumeController()
        : this(new ProcessMacOSAudioCommandRunner())
    {
    }

    public MacOSSystemVolumeController(IMacOSAudioCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public double GetMasterVolume()
    {
        var result = RunScript("output volume of (get volume settings)");
        if (result.ExitCode != 0 ||
            !double.TryParse(result.StandardOutput.Trim(), out var volume))
        {
            return 80;
        }

        return Math.Clamp(volume, 0, 100);
    }

    public bool SetMasterVolume(double volume)
    {
        var clamped = (int)Math.Round(Math.Clamp(volume, 0, 100), MidpointRounding.AwayFromZero);
        return RunScript($"set volume output volume {clamped}").ExitCode == 0;
    }

    public bool IsMuted()
    {
        var result = RunScript("output muted of (get volume settings)");
        if (result.ExitCode != 0)
        {
            return false;
        }

        var value = result.StandardOutput.Trim();
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    public bool SetMute(bool mute)
    {
        return RunScript($"set volume output muted {mute.ToString().ToLowerInvariant()}").ExitCode == 0;
    }

    public bool ToggleMute()
    {
        return SetMute(!IsMuted());
    }

    public (double min, double max) GetVolumeRange()
    {
        return (0, 100);
    }

    private MacOSAudioCommandResult RunScript(string script)
    {
        return _runner.Run(OsaScriptToolPath, ["-e", script]);
    }
}

public interface IMacOSAudioCommandRunner
{
    MacOSAudioCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record MacOSAudioCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record MacOSAudioCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessMacOSAudioCommandRunner : IMacOSAudioCommandRunner
{
    public MacOSAudioCommandResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start command: {fileName}");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new MacOSAudioCommandResult(process.ExitCode, output, error);
    }
}
