using TM.Framework.SystemInfo;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemInfoServiceTests
{
    [Fact]
    public void CollectSnapshot_uses_probe_data_and_formats_core_fields()
    {
        var service = new PortableSystemInfoService(new FakeSystemInfoProbe
        {
            OSDescription = "macOS 15.4",
            OSVersion = "15.4",
            OSArchitecture = "Arm64",
            ProcessArchitecture = "Arm64",
            MachineName = "MacBook-Pro",
            UserName = "writer",
            ProcessorName = "Apple M4 Pro",
            ProcessorCount = 14,
            TotalMemoryBytes = 18_253_611_008
        });

        var snapshot = service.CollectSnapshot();

        Assert.Equal("macOS 15.4", snapshot.OS.Name);
        Assert.Equal("15.4", snapshot.OS.Version);
        Assert.Equal("Arm64", snapshot.OS.Architecture);
        Assert.Equal("MacBook-Pro", snapshot.OS.ComputerName);
        Assert.Equal("writer", snapshot.OS.UserName);
        Assert.Equal("Apple M4 Pro", snapshot.Cpu.Name);
        Assert.Equal(14, snapshot.Cpu.LogicalProcessors);
        Assert.Equal("Arm64", snapshot.Cpu.Architecture);
        Assert.Equal("17 GB", snapshot.Memory.TotalMemory);
    }

    [Fact]
    public void CollectSnapshot_maps_ready_drives_and_active_network_adapters()
    {
        var service = new PortableSystemInfoService(new FakeSystemInfoProbe
        {
            Drives =
            [
                new ProbeDriveInfo
                {
                    Name = "/",
                    DriveType = "Fixed",
                    FileSystem = "apfs",
                    IsReady = true,
                    TotalBytes = 1_000,
                    FreeBytes = 250
                },
                new ProbeDriveInfo
                {
                    Name = "/Volumes/Offline",
                    DriveType = "Removable",
                    FileSystem = "apfs",
                    IsReady = false,
                    TotalBytes = 0,
                    FreeBytes = 0
                }
            ],
            NetworkAdapters =
            [
                new ProbeNetworkAdapterInfo
                {
                    Name = "en0",
                    Description = "Wi-Fi",
                    Type = "Wireless80211",
                    Status = "Up",
                    IPv4Address = "192.168.1.24",
                    IPv6Address = "fe80::1",
                    MacAddress = "001122334455",
                    Gateway = "192.168.1.1",
                    Dns = "1.1.1.1",
                    SpeedBitsPerSecond = 1_200_000_000,
                    BytesSent = 1_024,
                    BytesReceived = 2_048,
                    IsWireless = true
                }
            ]
        });

        var snapshot = service.CollectSnapshot();

        var drive = Assert.Single(snapshot.Drives);
        Assert.Equal("/", drive.Name);
        Assert.Equal("750 B", drive.UsedSize);
        Assert.Equal("250 B", drive.FreeSize);
        Assert.Equal(75, drive.UsagePercent);

        var adapter = Assert.Single(snapshot.NetworkAdapters);
        Assert.Equal("en0", adapter.Name);
        Assert.Equal("192.168.1.24", adapter.IPv4Address);
        Assert.Equal("1.20 Gbps", adapter.Speed);
        Assert.Equal("1 KB", adapter.BytesSent);
        Assert.Equal("2 KB", adapter.BytesReceived);
        Assert.True(adapter.IsWireless);
    }

    [Fact]
    public void CollectSnapshot_includes_injected_macos_profiler_details()
    {
        var service = new PortableSystemInfoService(
            new FakeSystemInfoProbe(),
            systemProfilerDetailsProvider: () => new PortableSystemProfilerDetails
            {
                Firmware = new PortableFirmwareInfo { ModelIdentifier = "Mac16,1" },
                Gpus = [new PortableGpuInfo { Name = "Apple M4 Pro" }],
                Displays = [new PortableDisplayInfo { Name = "Color LCD" }]
            });

        var snapshot = service.CollectSnapshot();

        Assert.Equal("Mac16,1", snapshot.Firmware.ModelIdentifier);
        Assert.Equal("Apple M4 Pro", Assert.Single(snapshot.Gpus).Name);
        Assert.Equal("Color LCD", Assert.Single(snapshot.Displays).Name);
    }


    [Fact]
    public void BuildReport_exports_the_original_core_sections()
    {
        var service = new PortableSystemInfoService(new FakeSystemInfoProbe(), () => new DateTime(2026, 5, 10, 8, 30, 0));
        var snapshot = new PortableSystemInfoSnapshot
        {
            OS = new PortableOperatingSystemInfo
            {
                Name = "macOS",
                Version = "15.4",
                Architecture = "Arm64",
                ComputerName = "Mac",
                UserName = "writer"
            },
            Cpu = new PortableCpuInfo
            {
                Name = "Apple M4",
                LogicalProcessors = 10,
                Architecture = "Arm64"
            },
            Memory = new PortableMemoryInfo { TotalMemory = "24 GB" },
            Drives =
            [
                new PortableDriveInfo
                {
                    Name = "/",
                    DriveType = "Fixed",
                    FileSystem = "apfs",
                    TotalSize = "1 TB",
                    UsedSize = "600 GB",
                    FreeSize = "400 GB",
                    UsagePercent = 60
                }
            ],
            NetworkAdapters =
            [
                new PortableNetworkAdapterInfo
                {
                    Name = "en0",
                    Description = "Wi-Fi",
                    IPv4Address = "192.168.1.24",
                    MacAddress = "001122334455",
                    Gateway = "192.168.1.1"
                }
            ]
        };

        var report = service.BuildReport(snapshot);

        Assert.Contains("生成时间: 2026-05-10 08:30:00", report);
        Assert.Contains("【操作系统信息】", report);
        Assert.Contains("操作系统: macOS", report);
        Assert.Contains("【CPU信息】", report);
        Assert.Contains("处理器: Apple M4", report);
        Assert.Contains("【内存信息】", report);
        Assert.Contains("【磁盘信息】", report);
        Assert.Contains("驱动器 /:", report);
        Assert.Contains("【网络适配器】", report);
    }

    [Fact]
    public void MacOSSystemProfilerParser_maps_hardware_gpu_and_display_details()
    {
        const string hardwareOutput = """
            Hardware:

                Hardware Overview:

                  Model Name: MacBook Pro
                  Model Identifier: Mac16,1
                  Chip: Apple M4 Pro
                  Total Number of Cores: 14 (10 performance and 4 efficiency)
                  Memory: 24 GB
                  System Firmware Version: 11881.101.1
                  OS Loader Version: 11881.101.1
                  Serial Number (system): C02TEST12345
                  Hardware UUID: 11111111-2222-3333-4444-555555555555
            """;
        const string displaysOutput = """
            Graphics/Displays:

                Apple M4 Pro:

                  Chipset Model: Apple M4 Pro
                  Type: GPU
                  Bus: Built-In
                  Total Number of Cores: 20
                  Vendor: Apple (0x106b)
                  Metal Support: Metal 3

                  Color LCD:
                    Display Type: Built-In Liquid Retina XDR
                    Resolution: 3456 x 2234 Retina
                    Main Display: Yes
                    Mirror: Off
                    Online: Yes
                    Connection Type: Internal
            """;

        var details = MacOSSystemProfilerParser.Parse(hardwareOutput, displaysOutput);

        Assert.Equal("MacBook Pro", details.Firmware.ModelName);
        Assert.Equal("Mac16,1", details.Firmware.ModelIdentifier);
        Assert.Equal("Apple M4 Pro", details.Firmware.Chip);
        Assert.Equal("24 GB", details.Firmware.Memory);
        Assert.Equal("11881.101.1", details.Firmware.SystemFirmwareVersion);

        var gpu = Assert.Single(details.Gpus);
        Assert.Equal("Apple M4 Pro", gpu.Name);
        Assert.Equal("20", gpu.TotalCores);
        Assert.Equal("Metal 3", gpu.MetalSupport);

        var display = Assert.Single(details.Displays);
        Assert.Equal("Color LCD", display.Name);
        Assert.Equal("3456 x 2234 Retina", display.Resolution);
        Assert.True(display.IsMainDisplay);
        Assert.True(display.IsOnline);
        Assert.Equal("Internal", display.ConnectionType);
    }

    [Fact]
    public void BuildReport_includes_optional_gpu_display_and_firmware_sections()
    {
        var service = new PortableSystemInfoService(new FakeSystemInfoProbe(), () => new DateTime(2026, 5, 10, 8, 30, 0));
        var snapshot = new PortableSystemInfoSnapshot
        {
            Firmware = new PortableFirmwareInfo
            {
                ModelName = "MacBook Pro",
                ModelIdentifier = "Mac16,1",
                SystemFirmwareVersion = "11881.101.1"
            },
            Gpus =
            [
                new PortableGpuInfo
                {
                    Name = "Apple M4 Pro",
                    MetalSupport = "Metal 3"
                }
            ],
            Displays =
            [
                new PortableDisplayInfo
                {
                    Name = "Color LCD",
                    Resolution = "3456 x 2234 Retina",
                    IsMainDisplay = true,
                    ConnectionType = "Internal"
                }
            ]
        };

        var report = service.BuildReport(snapshot);

        Assert.Contains("【GPU信息】", report);
        Assert.Contains("Apple M4 Pro", report);
        Assert.Contains("【显示器信息】", report);
        Assert.Contains("Color LCD", report);
        Assert.Contains("【硬件/固件信息】", report);
        Assert.Contains("Mac16,1", report);
    }

    [Fact]
    public void System_info_report_export_planner_builds_original_default_path()
    {
        var exportTime = new DateTime(2026, 5, 11, 16, 17, 18);

        var path = PortableSystemInfoReportExportPlanner.BuildDefaultPath(
            "/Users/writer/Library/Application Support/Tianming",
            exportTime);

        Assert.Equal(
            "/Users/writer/Library/Application Support/Tianming/Framework/SystemSettings/Info/SystemInfo/system_info_export_20260511_161718.txt",
            path);
    }

    [Fact]
    public async Task System_info_report_exporter_writes_report_atomically()
    {
        using var workspace = new TempDirectory();
        var service = new PortableSystemInfoService(new FakeSystemInfoProbe(), () => new DateTime(2026, 5, 10, 8, 30, 0));
        var snapshot = new PortableSystemInfoSnapshot
        {
            OS = new PortableOperatingSystemInfo
            {
                Name = "macOS",
                Version = "15.4",
                Architecture = "Arm64",
                ComputerName = "Mac",
                UserName = "writer"
            },
            Cpu = new PortableCpuInfo
            {
                Name = "Apple M4",
                LogicalProcessors = 10,
                Architecture = "Arm64"
            },
            Memory = new PortableMemoryInfo { TotalMemory = "24 GB" }
        };
        var targetPath = Path.Combine(workspace.Path, "exports", "system_info_export.txt");

        var result = await PortableSystemInfoReportExporter.ExportAsync(service, snapshot, targetPath);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.Contains("生成时间: 2026-05-10 08:30:00", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public void MacOSSystemProfilerCatalog_runs_hardware_and_display_probes()
    {
        var runner = new RecordingSystemProfilerCommandRunner(
            hardwareOutput: "      Model Name: MacBook Pro\n      Model Identifier: Mac16,1\n",
            displaysOutput: "    Apple M4 Pro:\n      Chipset Model: Apple M4 Pro\n");
        var catalog = new MacOSSystemProfilerCatalog(runner);

        var details = catalog.GetDetails();

        Assert.Equal("MacBook Pro", details.Firmware.ModelName);
        Assert.Equal("Apple M4 Pro", Assert.Single(details.Gpus).Name);
        Assert.Equal(2, runner.Invocations.Count);
        Assert.Equal("/usr/sbin/system_profiler", runner.Invocations[0].FileName);
        Assert.Equal(["SPHardwareDataType"], runner.Invocations[0].Arguments);
        Assert.Equal("/usr/sbin/system_profiler", runner.Invocations[1].FileName);
        Assert.Equal(["SPDisplaysDataType"], runner.Invocations[1].Arguments);
    }

    private sealed class FakeSystemInfoProbe : IPortableSystemInfoProbe
    {
        public string OSDescription { get; init; } = "Unix";
        public string OSVersion { get; init; } = "1.0";
        public string OSArchitecture { get; init; } = "Arm64";
        public string ProcessArchitecture { get; init; } = "Arm64";
        public string MachineName { get; init; } = "machine";
        public string UserName { get; init; } = "user";
        public string ProcessorName { get; init; } = "Processor";
        public int ProcessorCount { get; init; } = 1;
        public long? TotalMemoryBytes { get; init; }
        public IReadOnlyList<ProbeDriveInfo> Drives { get; init; } = [];
        public IReadOnlyList<ProbeNetworkAdapterInfo> NetworkAdapters { get; init; } = [];
    }

    private sealed class RecordingSystemProfilerCommandRunner : IMacOSSystemProfilerCommandRunner
    {
        private readonly string _hardwareOutput;
        private readonly string _displaysOutput;

        public RecordingSystemProfilerCommandRunner(string hardwareOutput, string displaysOutput)
        {
            _hardwareOutput = hardwareOutput;
            _displaysOutput = displaysOutput;
        }

        public List<MacOSSystemProfilerCommandInvocation> Invocations { get; } = [];

        public MacOSSystemProfilerCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new MacOSSystemProfilerCommandInvocation(fileName, arguments.ToArray()));
            return arguments.Single() switch
            {
                "SPHardwareDataType" => new MacOSSystemProfilerCommandResult(0, _hardwareOutput, string.Empty),
                "SPDisplaysDataType" => new MacOSSystemProfilerCommandResult(0, _displaysOutput, string.Empty),
                _ => new MacOSSystemProfilerCommandResult(1, string.Empty, "unexpected")
            };
        }
    }
}
