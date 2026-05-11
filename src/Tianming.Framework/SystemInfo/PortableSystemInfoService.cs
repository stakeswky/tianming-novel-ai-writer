using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TM.Framework.SystemInfo;

public interface IPortableSystemInfoProbe
{
    string OSDescription { get; }
    string OSVersion { get; }
    string OSArchitecture { get; }
    string ProcessArchitecture { get; }
    string MachineName { get; }
    string UserName { get; }
    string ProcessorName { get; }
    int ProcessorCount { get; }
    long? TotalMemoryBytes { get; }
    IReadOnlyList<ProbeDriveInfo> Drives { get; }
    IReadOnlyList<ProbeNetworkAdapterInfo> NetworkAdapters { get; }
}

public sealed class ProbeDriveInfo
{
    public string Name { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public string FileSystem { get; init; } = string.Empty;
    public bool IsReady { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
}

public sealed class ProbeNetworkAdapterInfo
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string IPv4Address { get; init; } = string.Empty;
    public string IPv6Address { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string Dns { get; init; } = string.Empty;
    public long SpeedBitsPerSecond { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public bool IsWireless { get; init; }
}

public sealed class PortableSystemInfoSnapshot
{
    public PortableOperatingSystemInfo OS { get; init; } = new();
    public PortableCpuInfo Cpu { get; init; } = new();
    public PortableMemoryInfo Memory { get; init; } = new();
    public List<PortableDriveInfo> Drives { get; init; } = [];
    public List<PortableNetworkAdapterInfo> NetworkAdapters { get; init; } = [];
    public List<PortableGpuInfo> Gpus { get; init; } = [];
    public List<PortableDisplayInfo> Displays { get; init; } = [];
    public PortableFirmwareInfo Firmware { get; init; } = new();
}

public sealed class PortableOperatingSystemInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
}

public sealed class PortableCpuInfo
{
    public string Name { get; init; } = string.Empty;
    public int LogicalProcessors { get; init; }
    public string Architecture { get; init; } = string.Empty;
}

public sealed class PortableMemoryInfo
{
    public string TotalMemory { get; init; } = "Unknown";
}

public sealed class PortableGpuInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Bus { get; init; } = string.Empty;
    public string Vendor { get; init; } = string.Empty;
    public string TotalCores { get; init; } = string.Empty;
    public string Vram { get; init; } = string.Empty;
    public string MetalSupport { get; init; } = string.Empty;
}

public sealed class PortableDisplayInfo
{
    public string Name { get; init; } = string.Empty;
    public string DisplayType { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public bool IsMainDisplay { get; init; }
    public bool IsOnline { get; init; }
    public string Mirror { get; init; } = string.Empty;
    public string ConnectionType { get; init; } = string.Empty;
}

public sealed class PortableFirmwareInfo
{
    public string ModelName { get; init; } = string.Empty;
    public string ModelIdentifier { get; init; } = string.Empty;
    public string Chip { get; init; } = string.Empty;
    public string TotalCores { get; init; } = string.Empty;
    public string Memory { get; init; } = string.Empty;
    public string SystemFirmwareVersion { get; init; } = string.Empty;
    public string OSLoaderVersion { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string HardwareUuid { get; init; } = string.Empty;
}

public sealed class PortableSystemProfilerDetails
{
    public PortableFirmwareInfo Firmware { get; init; } = new();
    public List<PortableGpuInfo> Gpus { get; init; } = [];
    public List<PortableDisplayInfo> Displays { get; init; } = [];
}

public sealed class PortableDriveInfo
{
    public string Name { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public string FileSystem { get; init; } = string.Empty;
    public string TotalSize { get; init; } = string.Empty;
    public string UsedSize { get; init; } = string.Empty;
    public string FreeSize { get; init; } = string.Empty;
    public double UsagePercent { get; init; }
}

public sealed class PortableNetworkAdapterInfo
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string IPv4Address { get; init; } = string.Empty;
    public string IPv6Address { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string Dns { get; init; } = string.Empty;
    public string Speed { get; init; } = string.Empty;
    public string BytesSent { get; init; } = string.Empty;
    public string BytesReceived { get; init; } = string.Empty;
    public bool IsWireless { get; init; }
}

public sealed class PortableSystemInfoService
{
    private readonly IPortableSystemInfoProbe _probe;
    private readonly Func<DateTime> _clock;
    private readonly Func<PortableSystemProfilerDetails?>? _systemProfilerDetailsProvider;

    public PortableSystemInfoService(
        IPortableSystemInfoProbe? probe = null,
        Func<DateTime>? clock = null,
        Func<PortableSystemProfilerDetails?>? systemProfilerDetailsProvider = null)
    {
        _probe = probe ?? new DotNetSystemInfoProbe();
        _clock = clock ?? (() => DateTime.Now);
        _systemProfilerDetailsProvider = systemProfilerDetailsProvider;
    }

    public PortableSystemInfoSnapshot CollectSnapshot()
    {
        var profilerDetails = _systemProfilerDetailsProvider?.Invoke();
        return new PortableSystemInfoSnapshot
        {
            OS = new PortableOperatingSystemInfo
            {
                Name = EmptyToUnknown(_probe.OSDescription),
                Version = EmptyToUnknown(_probe.OSVersion),
                Architecture = EmptyToUnknown(_probe.OSArchitecture),
                ProcessArchitecture = EmptyToUnknown(_probe.ProcessArchitecture),
                ComputerName = EmptyToUnknown(_probe.MachineName),
                UserName = EmptyToUnknown(_probe.UserName)
            },
            Cpu = new PortableCpuInfo
            {
                Name = EmptyToUnknown(_probe.ProcessorName),
                LogicalProcessors = Math.Max(0, _probe.ProcessorCount),
                Architecture = EmptyToUnknown(_probe.ProcessArchitecture)
            },
            Memory = new PortableMemoryInfo
            {
                TotalMemory = _probe.TotalMemoryBytes is > 0
                    ? FormatBytes(_probe.TotalMemoryBytes.Value)
                    : "Unknown"
            },
            Drives = _probe.Drives
                .Where(drive => drive.IsReady && drive.TotalBytes > 0)
                .Select(MapDrive)
                .ToList(),
            NetworkAdapters = _probe.NetworkAdapters
                .Where(adapter => string.Equals(adapter.Status, "Up", StringComparison.OrdinalIgnoreCase))
                .Select(MapNetworkAdapter)
                .ToList(),
            Gpus = profilerDetails?.Gpus.ToList() ?? [],
            Displays = profilerDetails?.Displays.ToList() ?? [],
            Firmware = profilerDetails?.Firmware ?? new PortableFirmwareInfo()
        };
    }

    public string BuildReport(PortableSystemInfoSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================== 系统信息报告 ====================");
        sb.AppendLine($"生成时间: {_clock():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("【操作系统信息】");
        sb.AppendLine($"  操作系统: {snapshot.OS.Name}");
        sb.AppendLine($"  版本: {snapshot.OS.Version}");
        sb.AppendLine($"  架构: {snapshot.OS.Architecture}");
        sb.AppendLine($"  计算机名: {snapshot.OS.ComputerName}");
        sb.AppendLine($"  用户名: {snapshot.OS.UserName}");
        sb.AppendLine();

        sb.AppendLine("【CPU信息】");
        sb.AppendLine($"  处理器: {snapshot.Cpu.Name}");
        sb.AppendLine($"  逻辑处理器: {snapshot.Cpu.LogicalProcessors}");
        sb.AppendLine($"  架构: {snapshot.Cpu.Architecture}");
        sb.AppendLine();

        sb.AppendLine("【内存信息】");
        sb.AppendLine($"  总内存: {snapshot.Memory.TotalMemory}");
        sb.AppendLine();

        sb.AppendLine("【磁盘信息】");
        foreach (var drive in snapshot.Drives)
        {
            sb.AppendLine($"  驱动器 {drive.Name}:");
            sb.AppendLine($"    文件系统: {drive.FileSystem}");
            sb.AppendLine($"    总容量: {drive.TotalSize}");
            sb.AppendLine($"    已用: {drive.UsedSize}");
            sb.AppendLine($"    剩余: {drive.FreeSize}");
            sb.AppendLine($"    使用率: {drive.UsagePercent}%");
        }
        sb.AppendLine();

        sb.AppendLine("【网络适配器】");
        foreach (var adapter in snapshot.NetworkAdapters)
        {
            sb.AppendLine($"  {adapter.Name}:");
            sb.AppendLine($"    描述: {adapter.Description}");
            sb.AppendLine($"    类型: {adapter.Type}");
            sb.AppendLine($"    IP地址: {adapter.IPv4Address}");
            sb.AppendLine($"    MAC地址: {adapter.MacAddress}");
            sb.AppendLine($"    网关: {adapter.Gateway}");
        }

        if (snapshot.Gpus.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【GPU信息】");
            foreach (var gpu in snapshot.Gpus)
            {
                sb.AppendLine($"  {gpu.Name}:");
                sb.AppendLine($"    类型: {EmptyToUnknown(gpu.Type)}");
                sb.AppendLine($"    总核心: {EmptyToUnknown(gpu.TotalCores)}");
                sb.AppendLine($"    Metal支持: {EmptyToUnknown(gpu.MetalSupport)}");
            }
        }

        if (snapshot.Displays.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【显示器信息】");
            foreach (var display in snapshot.Displays)
            {
                var primary = display.IsMainDisplay ? " (主)" : string.Empty;
                sb.AppendLine($"  {display.Name}{primary}:");
                sb.AppendLine($"    分辨率: {EmptyToUnknown(display.Resolution)}");
                sb.AppendLine($"    连接类型: {EmptyToUnknown(display.ConnectionType)}");
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Firmware.ModelName) ||
            !string.IsNullOrWhiteSpace(snapshot.Firmware.ModelIdentifier) ||
            !string.IsNullOrWhiteSpace(snapshot.Firmware.SystemFirmwareVersion))
        {
            sb.AppendLine();
            sb.AppendLine("【硬件/固件信息】");
            sb.AppendLine($"  型号: {EmptyToUnknown(snapshot.Firmware.ModelName)}");
            sb.AppendLine($"  型号标识: {EmptyToUnknown(snapshot.Firmware.ModelIdentifier)}");
            sb.AppendLine($"  芯片: {EmptyToUnknown(snapshot.Firmware.Chip)}");
            sb.AppendLine($"  系统固件版本: {EmptyToUnknown(snapshot.Firmware.SystemFirmwareVersion)}");
        }

        return sb.ToString();
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

    private static PortableDriveInfo MapDrive(ProbeDriveInfo drive)
    {
        var usedBytes = Math.Max(0, drive.TotalBytes - drive.FreeBytes);
        return new PortableDriveInfo
        {
            Name = drive.Name,
            DriveType = drive.DriveType,
            FileSystem = drive.FileSystem,
            TotalSize = FormatBytes(drive.TotalBytes),
            UsedSize = FormatBytes(usedBytes),
            FreeSize = FormatBytes(drive.FreeBytes),
            UsagePercent = Math.Round((double)usedBytes / drive.TotalBytes * 100, 2)
        };
    }

    private static PortableNetworkAdapterInfo MapNetworkAdapter(ProbeNetworkAdapterInfo adapter)
    {
        return new PortableNetworkAdapterInfo
        {
            Name = adapter.Name,
            Description = adapter.Description,
            Type = adapter.Type,
            Status = adapter.Status,
            IPv4Address = EmptyToNone(adapter.IPv4Address),
            IPv6Address = EmptyToNone(adapter.IPv6Address),
            MacAddress = adapter.MacAddress,
            Gateway = EmptyToNone(adapter.Gateway),
            Dns = EmptyToNone(adapter.Dns),
            Speed = adapter.SpeedBitsPerSecond > 0 ? FormatBitrate(adapter.SpeedBitsPerSecond) : "Unknown",
            BytesSent = FormatBytes(adapter.BytesSent),
            BytesReceived = FormatBytes(adapter.BytesReceived),
            IsWireless = adapter.IsWireless
        };
    }

    private static string EmptyToUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static string EmptyToNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }
}

public static class PortableSystemInfoReportExportPlanner
{
    public static string BuildDefaultPath(string storageRoot, DateTime exportTime)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root is required.", nameof(storageRoot));
        }

        return Path.Combine(
            storageRoot,
            "Framework",
            "SystemSettings",
            "Info",
            "SystemInfo",
            $"system_info_export_{exportTime:yyyyMMdd_HHmmss}.txt");
    }
}

public sealed record PortableSystemInfoReportExportResult(
    bool Success,
    string FilePath,
    string? ErrorMessage);

public static class PortableSystemInfoReportExporter
{
    public static async Task<PortableSystemInfoReportExportResult> ExportAsync(
        PortableSystemInfoService service,
        PortableSystemInfoSnapshot snapshot,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Export file path is required.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(
                tempPath,
                service.BuildReport(snapshot),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            return new PortableSystemInfoReportExportResult(true, filePath, null);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(tempPath);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(tempPath);
            return new PortableSystemInfoReportExportResult(false, filePath, ex.Message);
        }
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

public static class MacOSSystemProfilerParser
{
    public static PortableSystemProfilerDetails Parse(string hardwareOutput, string displaysOutput)
    {
        var displayDetails = ParseDisplayOutput(displaysOutput);
        return new PortableSystemProfilerDetails
        {
            Firmware = ParseFirmware(hardwareOutput),
            Gpus = displayDetails.Gpus,
            Displays = displayDetails.Displays
        };
    }

    private static PortableFirmwareInfo ParseFirmware(string output)
    {
        var properties = ParseIndentedProperties(output, minimumIndent: 6);
        return new PortableFirmwareInfo
        {
            ModelName = properties.GetValueOrDefault("Model Name") ?? string.Empty,
            ModelIdentifier = properties.GetValueOrDefault("Model Identifier") ?? string.Empty,
            Chip = properties.GetValueOrDefault("Chip") ?? string.Empty,
            TotalCores = properties.GetValueOrDefault("Total Number of Cores") ?? string.Empty,
            Memory = properties.GetValueOrDefault("Memory") ?? string.Empty,
            SystemFirmwareVersion = properties.GetValueOrDefault("System Firmware Version") ?? string.Empty,
            OSLoaderVersion = properties.GetValueOrDefault("OS Loader Version") ?? string.Empty,
            SerialNumber = properties.GetValueOrDefault("Serial Number (system)") ?? string.Empty,
            HardwareUuid = properties.GetValueOrDefault("Hardware UUID") ?? string.Empty
        };
    }

    private static PortableSystemProfilerDetails ParseDisplayOutput(string output)
    {
        var details = new PortableSystemProfilerDetails();
        string? gpuName = null;
        Dictionary<string, string> gpuProperties = new(StringComparer.OrdinalIgnoreCase);
        string? displayName = null;
        Dictionary<string, string> displayProperties = new(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (TryReadSectionHeader(line, indent: 4, out var nextGpuName))
            {
                AddDisplay(details.Displays, displayName, displayProperties);
                displayName = null;
                displayProperties.Clear();
                AddGpu(details.Gpus, gpuName, gpuProperties);
                gpuName = nextGpuName;
                gpuProperties.Clear();
                continue;
            }

            if (TryReadSectionHeader(line, indent: 6, out var nextDisplayName))
            {
                AddDisplay(details.Displays, displayName, displayProperties);
                displayName = nextDisplayName;
                displayProperties.Clear();
                continue;
            }

            if (displayName is not null && TryReadProperty(line, indent: 8, out var displayKey, out var displayValue))
            {
                displayProperties[displayKey] = displayValue;
                continue;
            }

            if (gpuName is not null && TryReadProperty(line, indent: 6, out var gpuKey, out var gpuValue))
            {
                gpuProperties[gpuKey] = gpuValue;
            }
        }

        AddDisplay(details.Displays, displayName, displayProperties);
        AddGpu(details.Gpus, gpuName, gpuProperties);
        return details;
    }

    private static void AddGpu(
        List<PortableGpuInfo> gpus,
        string? name,
        Dictionary<string, string> properties)
    {
        var resolvedName = properties.GetValueOrDefault("Chipset Model") ?? name;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return;
        }

        gpus.Add(new PortableGpuInfo
        {
            Name = resolvedName,
            Type = properties.GetValueOrDefault("Type") ?? string.Empty,
            Bus = properties.GetValueOrDefault("Bus") ?? string.Empty,
            Vendor = properties.GetValueOrDefault("Vendor") ?? string.Empty,
            TotalCores = properties.GetValueOrDefault("Total Number of Cores") ?? string.Empty,
            Vram = properties.GetValueOrDefault("VRAM") ?? string.Empty,
            MetalSupport = properties.GetValueOrDefault("Metal Support") ?? string.Empty
        });
    }

    private static void AddDisplay(
        List<PortableDisplayInfo> displays,
        string? name,
        Dictionary<string, string> properties)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        displays.Add(new PortableDisplayInfo
        {
            Name = name,
            DisplayType = properties.GetValueOrDefault("Display Type") ?? string.Empty,
            Resolution = properties.GetValueOrDefault("Resolution") ?? string.Empty,
            IsMainDisplay = IsYes(properties.GetValueOrDefault("Main Display")),
            IsOnline = IsYes(properties.GetValueOrDefault("Online")),
            Mirror = properties.GetValueOrDefault("Mirror") ?? string.Empty,
            ConnectionType = properties.GetValueOrDefault("Connection Type") ?? string.Empty
        });
    }

    private static Dictionary<string, string> ParseIndentedProperties(string output, int minimumIndent)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            if (CountLeadingSpaces(line) >= minimumIndent &&
                TryReadProperty(line, CountLeadingSpaces(line), out var key, out var value))
            {
                properties[key] = value;
            }
        }

        return properties;
    }

    private static bool TryReadSectionHeader(string line, int indent, out string name)
    {
        name = string.Empty;
        if (CountLeadingSpaces(line) != indent)
        {
            return false;
        }

        var trimmed = line.Trim();
        if (!trimmed.EndsWith(':') || trimmed.Count(character => character == ':') != 1)
        {
            return false;
        }

        name = trimmed.TrimEnd(':').Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool TryReadProperty(string line, int indent, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (CountLeadingSpaces(line) != indent)
        {
            return false;
        }

        var trimmed = line.Trim();
        var separatorIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            return false;
        }

        key = trimmed[..separatorIndex].Trim();
        value = trimmed[(separatorIndex + 1)..].Trim();
        return true;
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var character in line)
        {
            if (character != ' ')
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static bool IsYes(string? value)
    {
        return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MacOSSystemProfilerCatalog
{
    private const string SystemProfilerToolPath = "/usr/sbin/system_profiler";

    private readonly IMacOSSystemProfilerCommandRunner _runner;

    public MacOSSystemProfilerCatalog()
        : this(new ProcessMacOSSystemProfilerCommandRunner())
    {
    }

    public MacOSSystemProfilerCatalog(IMacOSSystemProfilerCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public PortableSystemProfilerDetails GetDetails()
    {
        var hardware = RunProfiler("SPHardwareDataType");
        var displays = RunProfiler("SPDisplaysDataType");
        return MacOSSystemProfilerParser.Parse(hardware, displays);
    }

    private string RunProfiler(string dataType)
    {
        var result = _runner.Run(SystemProfilerToolPath, [dataType]);
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException($"Failed to run system_profiler {dataType}: {message}".Trim());
        }

        return result.StandardOutput;
    }
}

public interface IMacOSSystemProfilerCommandRunner
{
    MacOSSystemProfilerCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record MacOSSystemProfilerCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record MacOSSystemProfilerCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessMacOSSystemProfilerCommandRunner : IMacOSSystemProfilerCommandRunner
{
    public MacOSSystemProfilerCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        return new MacOSSystemProfilerCommandResult(process.ExitCode, output, error);
    }
}

internal sealed class DotNetSystemInfoProbe : IPortableSystemInfoProbe
{
    public string OSDescription => RuntimeInformation.OSDescription;

    public string OSVersion => Environment.OSVersion.VersionString;

    public string OSArchitecture => RuntimeInformation.OSArchitecture.ToString();

    public string ProcessArchitecture => RuntimeInformation.ProcessArchitecture.ToString();

    public string MachineName => Environment.MachineName;

    public string UserName => Environment.UserName;

    public string ProcessorName => Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
        ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
        ?? RuntimeInformation.ProcessArchitecture.ToString();

    public int ProcessorCount => Environment.ProcessorCount;

    public long? TotalMemoryBytes
    {
        get
        {
            var totalAvailable = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return totalAvailable > 0 ? totalAvailable : null;
        }
    }

    public IReadOnlyList<ProbeDriveInfo> Drives => DriveInfo.GetDrives()
        .Select(drive =>
        {
            try
            {
                return new ProbeDriveInfo
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    FileSystem = drive.IsReady ? drive.DriveFormat : string.Empty,
                    IsReady = drive.IsReady,
                    TotalBytes = drive.IsReady ? drive.TotalSize : 0,
                    FreeBytes = drive.IsReady ? drive.AvailableFreeSpace : 0
                };
            }
            catch (IOException)
            {
                return new ProbeDriveInfo
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    IsReady = false
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new ProbeDriveInfo
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType.ToString(),
                    IsReady = false
                };
            }
        })
        .ToList();

    public IReadOnlyList<ProbeNetworkAdapterInfo> NetworkAdapters => NetworkInterface.GetAllNetworkInterfaces()
        .Select(MapAdapter)
        .ToList();

    private static ProbeNetworkAdapterInfo MapAdapter(NetworkInterface adapter)
    {
        var properties = adapter.GetIPProperties();
        var stats = adapter.GetIPStatistics();

        return new ProbeNetworkAdapterInfo
        {
            Name = adapter.Name,
            Description = adapter.Description,
            Type = adapter.NetworkInterfaceType.ToString(),
            Status = adapter.OperationalStatus.ToString(),
            IPv4Address = properties.UnicastAddresses
                .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString() ?? string.Empty,
            IPv6Address = properties.UnicastAddresses
                .FirstOrDefault(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                    !address.Address.IsIPv6LinkLocal)
                ?.Address.ToString() ?? string.Empty,
            MacAddress = adapter.GetPhysicalAddress().ToString(),
            Gateway = properties.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? string.Empty,
            Dns = properties.DnsAddresses.FirstOrDefault()?.ToString() ?? string.Empty,
            SpeedBitsPerSecond = adapter.Speed,
            BytesSent = stats.BytesSent,
            BytesReceived = stats.BytesReceived,
            IsWireless = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
        };
    }
}
