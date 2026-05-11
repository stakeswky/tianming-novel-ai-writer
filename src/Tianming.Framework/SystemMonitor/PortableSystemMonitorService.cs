using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.SystemMonitor;

public interface IPortableSystemMonitorProbe
{
    double? CpuFrequencyMhz { get; }
    double? CpuUsagePercent { get; }
    double? CpuTemperatureCelsius { get; }
    long? TotalMemoryBytes { get; }
    long? AvailableMemoryBytes { get; }
    IReadOnlyList<ProbeDiskUsage> Disks { get; }
    IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces { get; }
    IReadOnlyList<ProbeSensorReading> Sensors { get; }
}

public sealed class ProbeDiskUsage
{
    public string Name { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public bool IsReady { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
}

public sealed class ProbeNetworkTraffic
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsUp { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public long SpeedBitsPerSecond { get; init; }
}

public sealed class ProbeSensorReading
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}

public sealed class PortableSystemMonitorSnapshot
{
    public PortableCpuMonitorInfo Cpu { get; init; } = new();
    public PortableMemoryMonitorInfo Memory { get; init; } = new();
    public List<PortableDiskUsageInfo> Disks { get; init; } = [];
    public List<PortableNetworkTrafficInfo> NetworkTraffics { get; init; } = [];
    public List<PortableSensorInfo> Sensors { get; init; } = [];
}

public sealed class PortableSystemMonitorSettings
{
    [JsonPropertyName("LastRefreshTime")] public DateTime LastRefreshTime { get; set; } = DateTime.Now;

    public PortableSystemMonitorSettings Clone()
    {
        return new PortableSystemMonitorSettings
        {
            LastRefreshTime = LastRefreshTime
        };
    }
}

public sealed class FileSystemMonitorSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileSystemMonitorSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableSystemMonitorSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableSystemMonitorSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableSystemMonitorSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? new PortableSystemMonitorSettings();
        }
        catch (JsonException)
        {
            return new PortableSystemMonitorSettings();
        }
        catch (IOException)
        {
            return new PortableSystemMonitorSettings();
        }
    }

    public async Task SaveAsync(
        PortableSystemMonitorSettings settings,
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
            await JsonSerializer.SerializeAsync(
                stream,
                settings.Clone(),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public sealed class PortableSystemMonitorController
{
    private readonly PortableSystemMonitorService _service;
    private readonly PortableSystemMonitorSettings _settings;
    private readonly Func<PortableSystemMonitorSettings, CancellationToken, Task> _saveSettingsAsync;
    private readonly Func<DateTime> _clock;

    public PortableSystemMonitorController(
        PortableSystemMonitorService service,
        PortableSystemMonitorSettings settings,
        Func<PortableSystemMonitorSettings, CancellationToken, Task>? saveSettingsAsync = null,
        Func<DateTime>? clock = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _saveSettingsAsync = saveSettingsAsync ?? ((_, _) => Task.CompletedTask);
        _clock = clock ?? (() => DateTime.Now);
    }

    public PortableSystemMonitorSnapshot CurrentSnapshot { get; private set; } = new();

    public async Task<PortableSystemMonitorSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentSnapshot = _service.CaptureSnapshot();
        _settings.LastRefreshTime = _clock();
        await _saveSettingsAsync(_settings, cancellationToken).ConfigureAwait(false);
        return CurrentSnapshot;
    }
}

public sealed class PortableCpuMonitorInfo
{
    public string CurrentFrequency { get; init; } = "Unknown";
    public double UsagePercent { get; init; }
    public string Temperature { get; init; } = "Unavailable";
}

public sealed class PortableMemoryMonitorInfo
{
    public string AvailableMemory { get; init; } = "Unknown";
    public double UsagePercent { get; init; }
}

public sealed class PortableDiskUsageInfo
{
    public string Name { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public string TotalSize { get; init; } = string.Empty;
    public string UsedSize { get; init; } = string.Empty;
    public string FreeSize { get; init; } = string.Empty;
    public double UsagePercent { get; init; }
}

public sealed class PortableNetworkTrafficInfo
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string BytesSent { get; init; } = string.Empty;
    public string BytesReceived { get; init; } = string.Empty;
    public string CurrentSpeed { get; init; } = string.Empty;
}

public sealed class PortableSensorInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}

public sealed class PortableSystemMonitorService
{
    private readonly IPortableSystemMonitorProbe _probe;

    public PortableSystemMonitorService(IPortableSystemMonitorProbe? probe = null)
    {
        _probe = probe ?? new DotNetSystemMonitorProbe();
    }

    public PortableSystemMonitorSnapshot CaptureSnapshot()
    {
        var sensors = _probe.Sensors.Select(sensor => new PortableSensorInfo
        {
            Name = sensor.Name,
            Type = sensor.Type,
            Value = sensor.Value,
            Status = sensor.Status,
            Icon = sensor.Icon
        }).ToList();

        if (sensors.Count == 0)
        {
            sensors.Add(new PortableSensorInfo
            {
                Name = "传感器监控",
                Type = "系统",
                Value = "不可用",
                Status = "需要管理员权限或硬件不支持",
                Icon = "info"
            });
        }

        return new PortableSystemMonitorSnapshot
        {
            Cpu = new PortableCpuMonitorInfo
            {
                CurrentFrequency = FormatCpuFrequency(_probe.CpuFrequencyMhz),
                UsagePercent = Math.Round(Math.Clamp(_probe.CpuUsagePercent ?? 0, 0, 100), 2),
                Temperature = FormatTemperature(_probe.CpuTemperatureCelsius)
            },
            Memory = new PortableMemoryMonitorInfo
            {
                AvailableMemory = _probe.AvailableMemoryBytes is > 0
                    ? FormatBytes(_probe.AvailableMemoryBytes.Value)
                    : "Unknown",
                UsagePercent = CalculateMemoryUsage(_probe.TotalMemoryBytes, _probe.AvailableMemoryBytes)
            },
            Disks = _probe.Disks
                .Where(disk => disk.IsReady && disk.TotalBytes > 0)
                .Select(MapDisk)
                .ToList(),
            NetworkTraffics = _probe.NetworkInterfaces
                .Where(network => network.IsUp)
                .Select(MapNetwork)
                .ToList(),
            Sensors = sensors
        };
    }

    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public static string FormatBitrate(long bitsPerSecond)
    {
        if (bitsPerSecond >= 1_000_000_000)
        {
            return $"{bitsPerSecond / 1_000_000_000.0:F2} Gbps";
        }

        if (bitsPerSecond >= 1_000_000)
        {
            return $"{bitsPerSecond / 1_000_000.0:F2} Mbps";
        }

        if (bitsPerSecond >= 1_000)
        {
            return $"{bitsPerSecond / 1_000.0:F2} Kbps";
        }

        return $"{bitsPerSecond} bps";
    }

    private static PortableDiskUsageInfo MapDisk(ProbeDiskUsage disk)
    {
        var usedBytes = Math.Max(0, disk.TotalBytes - disk.FreeBytes);
        return new PortableDiskUsageInfo
        {
            Name = disk.Name,
            DriveType = disk.DriveType,
            TotalSize = FormatBytes(disk.TotalBytes),
            UsedSize = FormatBytes(usedBytes),
            FreeSize = FormatBytes(disk.FreeBytes),
            UsagePercent = Math.Round((double)usedBytes / disk.TotalBytes * 100, 2)
        };
    }

    private static PortableNetworkTrafficInfo MapNetwork(ProbeNetworkTraffic network)
    {
        return new PortableNetworkTrafficInfo
        {
            Name = network.Name,
            Description = network.Description,
            Type = network.Type,
            BytesSent = FormatBytes(network.BytesSent),
            BytesReceived = FormatBytes(network.BytesReceived),
            CurrentSpeed = network.SpeedBitsPerSecond > 0
                ? FormatBitrate(network.SpeedBitsPerSecond)
                : "Unknown"
        };
    }

    private static string FormatCpuFrequency(double? mhz)
    {
        if (mhz is null or <= 0)
        {
            return "Unknown";
        }

        return $"{mhz.Value:0} MHz ({mhz.Value / 1000.0:F2} GHz)";
    }

    private static string FormatTemperature(double? celsius)
    {
        if (celsius is null)
        {
            return "Unavailable";
        }

        return celsius is >= 0 and <= 150
            ? $"{Math.Round(celsius.Value, 1, MidpointRounding.AwayFromZero):F1} °C"
            : "Invalid";
    }

    private static double CalculateMemoryUsage(long? totalBytes, long? availableBytes)
    {
        if (totalBytes is null or <= 0 || availableBytes is null or < 0)
        {
            return 0;
        }

        var usedBytes = Math.Max(0, totalBytes.Value - availableBytes.Value);
        return Math.Round((double)usedBytes / totalBytes.Value * 100, 2);
    }
}

internal sealed class DotNetSystemMonitorProbe : IPortableSystemMonitorProbe
{
    public double? CpuFrequencyMhz => null;

    public double? CpuUsagePercent => null;

    public double? CpuTemperatureCelsius => null;

    public long? TotalMemoryBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes > 0
        ? GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
        : null;

    public long? AvailableMemoryBytes
    {
        get
        {
            var total = TotalMemoryBytes;
            if (total is null)
            {
                return null;
            }

            var usedByProcess = Process.GetCurrentProcess().WorkingSet64;
            return Math.Max(0, total.Value - usedByProcess);
        }
    }

    public IReadOnlyList<ProbeDiskUsage> Disks => DriveInfo.GetDrives()
        .Select(drive =>
        {
            try
            {
                return new ProbeDiskUsage
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    IsReady = drive.IsReady,
                    TotalBytes = drive.IsReady ? drive.TotalSize : 0,
                    FreeBytes = drive.IsReady ? drive.AvailableFreeSpace : 0
                };
            }
            catch (IOException)
            {
                return new ProbeDiskUsage
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    IsReady = false
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new ProbeDiskUsage
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    IsReady = false
                };
            }
        })
        .ToList();

    public IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces => NetworkInterface.GetAllNetworkInterfaces()
        .Select(adapter =>
        {
            var stats = adapter.GetIPStatistics();
            return new ProbeNetworkTraffic
            {
                Name = adapter.Name,
                Description = adapter.Description,
                Type = adapter.NetworkInterfaceType.ToString(),
                IsUp = adapter.OperationalStatus == OperationalStatus.Up,
                BytesSent = stats.BytesSent,
                BytesReceived = stats.BytesReceived,
                SpeedBitsPerSecond = adapter.Speed
            };
        })
        .ToList();

    public IReadOnlyList<ProbeSensorReading> Sensors => [];
}

public sealed class MacOSSystemMonitorMetrics
{
    public double? CpuUsagePercent { get; init; }

    public double? CpuTemperatureCelsius { get; init; }

    public List<ProbeSensorReading> Sensors { get; init; } = [];
}

public static class MacOSSystemMonitorParser
{
    private static readonly Regex CpuUsageRegex = new(
        @"CPU usage:\s*(?<user>[0-9.]+)%\s*user,\s*(?<sys>[0-9.]+)%\s*sys",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BatteryRegex = new(
        @"(?<charge>\d+)%;\s*(?<state>[^;]+);",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TemperatureSensorRegex = new(
        @"(?<name>[A-Za-z][A-Za-z0-9 /\-()]*?)\s+temperature:\s*(?<temp>[0-9.]+)\s*C",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FanRegex = new(
        @"Fan(?:\s+\d+)?:\s*(?<rpm>\d+)\s*rpm",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static MacOSSystemMonitorMetrics Parse(
        string topOutput,
        string batteryOutput,
        string powermetricsOutput)
    {
        var sensors = new List<ProbeSensorReading>();
        AddBatterySensor(sensors, batteryOutput);
        var temperature = AddTemperatureSensors(sensors, powermetricsOutput);
        AddFanSensors(sensors, powermetricsOutput);

        return new MacOSSystemMonitorMetrics
        {
            CpuUsagePercent = ParseCpuUsage(topOutput),
            CpuTemperatureCelsius = temperature,
            Sensors = sensors
        };
    }

    private static double? ParseCpuUsage(string output)
    {
        var match = CpuUsageRegex.Match(output);
        if (!match.Success ||
            !double.TryParse(match.Groups["user"].Value, out var user) ||
            !double.TryParse(match.Groups["sys"].Value, out var sys))
        {
            return null;
        }

        return Math.Round(user + sys, 2);
    }

    private static void AddBatterySensor(List<ProbeSensorReading> sensors, string output)
    {
        var match = BatteryRegex.Match(output);
        if (!match.Success || !int.TryParse(match.Groups["charge"].Value, out var charge))
        {
            return;
        }

        sensors.Add(new ProbeSensorReading
        {
            Name = "电池电量",
            Type = "电源",
            Value = $"{charge}%",
            Status = charge > 20 ? "✅ 正常" : "⚠️ 低电量",
            Icon = charge > 20 ? "battery" : "battery-low"
        });
    }

    private static double? AddTemperatureSensors(List<ProbeSensorReading> sensors, string output)
    {
        double? cpuTemperature = null;

        foreach (Match match in TemperatureSensorRegex.Matches(output))
        {
            if (!double.TryParse(match.Groups["temp"].Value, out var celsius) || celsius is < 0 or > 150)
            {
                continue;
            }

            var name = NormalizeTemperatureSensorName(match.Groups["name"].Value);
            sensors.Add(new ProbeSensorReading
            {
                Name = name,
                Type = "温度",
                Value = $"{Math.Round(celsius, 1, MidpointRounding.AwayFromZero):F1} °C",
                Status = celsius > 80 ? "⚠️ 偏高" : celsius > 60 ? "🟡 正常" : "✅ 良好",
                Icon = "thermometer"
            });

            if (cpuTemperature is null && name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase))
            {
                cpuTemperature = celsius;
            }
        }

        return cpuTemperature;
    }

    private static string NormalizeTemperatureSensorName(string rawName)
    {
        var words = rawName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", words.Select(word => word.ToUpperInvariant() switch
        {
            "CPU" => "CPU",
            "GPU" => "GPU",
            _ => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()
        }));
    }

    private static void AddFanSensors(List<ProbeSensorReading> sensors, string output)
    {
        var fanIndex = 1;
        foreach (Match match in FanRegex.Matches(output))
        {
            if (!int.TryParse(match.Groups["rpm"].Value, out var rpm))
            {
                continue;
            }

            sensors.Add(new ProbeSensorReading
            {
                Name = $"系统风扇 {fanIndex}",
                Type = "风扇",
                Value = $"{rpm} RPM",
                Status = rpm > 0 ? "✅ 正常" : "⚠️ 异常",
                Icon = "fan"
            });
            fanIndex++;
        }
    }
}

public sealed class MacOSSystemMonitorProbe : IPortableSystemMonitorProbe
{
    private const string TopToolPath = "/usr/bin/top";
    private const string PmsetToolPath = "/usr/bin/pmset";
    private const string PowermetricsToolPath = "/usr/bin/powermetrics";

    private readonly IMacOSSystemMonitorCommandRunner _runner;
    private readonly IPortableSystemMonitorProbe _fallbackProbe;
    private MacOSSystemMonitorMetrics? _metrics;

    public MacOSSystemMonitorProbe()
        : this(new ProcessMacOSSystemMonitorCommandRunner())
    {
    }

    public MacOSSystemMonitorProbe(
        IMacOSSystemMonitorCommandRunner runner,
        IPortableSystemMonitorProbe? fallbackProbe = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _fallbackProbe = fallbackProbe ?? new DotNetSystemMonitorProbe();
    }

    public double? CpuFrequencyMhz => _fallbackProbe.CpuFrequencyMhz;

    public double? CpuUsagePercent => GetMetrics().CpuUsagePercent ?? _fallbackProbe.CpuUsagePercent;

    public double? CpuTemperatureCelsius => GetMetrics().CpuTemperatureCelsius ?? _fallbackProbe.CpuTemperatureCelsius;

    public long? TotalMemoryBytes => _fallbackProbe.TotalMemoryBytes;

    public long? AvailableMemoryBytes => _fallbackProbe.AvailableMemoryBytes;

    public IReadOnlyList<ProbeDiskUsage> Disks => _fallbackProbe.Disks;

    public IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces => _fallbackProbe.NetworkInterfaces;

    public IReadOnlyList<ProbeSensorReading> Sensors => GetMetrics().Sensors.Count > 0
        ? GetMetrics().Sensors
        : _fallbackProbe.Sensors;

    private MacOSSystemMonitorMetrics GetMetrics()
    {
        if (_metrics is not null)
        {
            return _metrics;
        }

        var topResult = _runner.Run(TopToolPath, ["-l", "1", "-n", "0"]);
        var batteryResult = _runner.Run(PmsetToolPath, ["-g", "batt"]);
        var powermetricsResult = _runner.Run(PowermetricsToolPath, ["--samplers", "smc", "-n", "1"]);
        _metrics = MacOSSystemMonitorParser.Parse(
            topResult.ExitCode == 0 ? topResult.StandardOutput : string.Empty,
            batteryResult.ExitCode == 0 ? batteryResult.StandardOutput : string.Empty,
            powermetricsResult.ExitCode == 0 ? powermetricsResult.StandardOutput : string.Empty);

        if (powermetricsResult.ExitCode != 0)
        {
            _metrics.Sensors.Add(BuildPowermetricsFailureSensor(powermetricsResult));
        }

        return _metrics;
    }

    private static ProbeSensorReading BuildPowermetricsFailureSensor(MacOSSystemMonitorCommandResult result)
    {
        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"exit code {result.ExitCode}"
            : result.StandardError.Trim();
        var requiresPrivilege = error.Contains("root", StringComparison.OrdinalIgnoreCase)
                                || error.Contains("privilege", StringComparison.OrdinalIgnoreCase)
                                || error.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase);

        return new ProbeSensorReading
        {
            Name = "powermetrics 权限",
            Type = "权限",
            Value = "不可用",
            Status = requiresPrivilege
                ? $"需要管理员权限或系统授权: {error}"
                : $"powermetrics 执行失败: {error}",
            Icon = requiresPrivilege ? "lock" : "warning"
        };
    }
}

public interface IMacOSSystemMonitorCommandRunner
{
    MacOSSystemMonitorCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record MacOSSystemMonitorCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record MacOSSystemMonitorCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessMacOSSystemMonitorCommandRunner : IMacOSSystemMonitorCommandRunner
{
    public MacOSSystemMonitorCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        return new MacOSSystemMonitorCommandResult(process.ExitCode, output, error);
    }
}
