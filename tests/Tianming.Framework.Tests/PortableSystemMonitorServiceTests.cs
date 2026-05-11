using TM.Framework.SystemMonitor;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemMonitorServiceTests
{
    [Fact]
    public void CaptureSnapshot_formats_cpu_and_memory_metrics()
    {
        var service = new PortableSystemMonitorService(new FakeSystemMonitorProbe
        {
            CpuFrequencyMhz = 3225,
            CpuUsagePercent = 42.456,
            CpuTemperatureCelsius = 61.25,
            TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
            AvailableMemoryBytes = 4L * 1024 * 1024 * 1024
        });

        var snapshot = service.CaptureSnapshot();

        Assert.Equal("3225 MHz (3.23 GHz)", snapshot.Cpu.CurrentFrequency);
        Assert.Equal(42.46, snapshot.Cpu.UsagePercent);
        Assert.Equal("61.3 °C", snapshot.Cpu.Temperature);
        Assert.Equal("4 GB", snapshot.Memory.AvailableMemory);
        Assert.Equal(75, snapshot.Memory.UsagePercent);
    }

    [Fact]
    public void CaptureSnapshot_maps_ready_disks_and_active_network_interfaces()
    {
        var service = new PortableSystemMonitorService(new FakeSystemMonitorProbe
        {
            Disks =
            [
                new ProbeDiskUsage
                {
                    Name = "/",
                    DriveType = "Fixed",
                    IsReady = true,
                    TotalBytes = 1_000,
                    FreeBytes = 125
                },
                new ProbeDiskUsage
                {
                    Name = "/Volumes/Offline",
                    DriveType = "Removable",
                    IsReady = false,
                    TotalBytes = 1_000,
                    FreeBytes = 1_000
                }
            ],
            NetworkInterfaces =
            [
                new ProbeNetworkTraffic
                {
                    Name = "en0",
                    Description = "Wi-Fi",
                    Type = "Wireless80211",
                    IsUp = true,
                    BytesSent = 2_048,
                    BytesReceived = 4_096,
                    SpeedBitsPerSecond = 866_000_000
                }
            ]
        });

        var snapshot = service.CaptureSnapshot();

        var disk = Assert.Single(snapshot.Disks);
        Assert.Equal("/", disk.Name);
        Assert.Equal("875 B", disk.UsedSize);
        Assert.Equal("125 B", disk.FreeSize);
        Assert.Equal(87.5, disk.UsagePercent);

        var network = Assert.Single(snapshot.NetworkTraffics);
        Assert.Equal("en0", network.Name);
        Assert.Equal("2 KB", network.BytesSent);
        Assert.Equal("4 KB", network.BytesReceived);
        Assert.Equal("866.00 Mbps", network.CurrentSpeed);
    }

    [Fact]
    public void CaptureSnapshot_uses_sensor_fallback_when_no_sensor_data_is_available()
    {
        var service = new PortableSystemMonitorService(new FakeSystemMonitorProbe());

        var snapshot = service.CaptureSnapshot();

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("传感器监控", sensor.Name);
        Assert.Equal("系统", sensor.Type);
        Assert.Equal("不可用", sensor.Value);
        Assert.Contains("硬件不支持", sensor.Status);
    }

    [Fact]
    public async Task System_monitor_settings_store_writes_last_refresh_time_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "SystemSettings", "Info", "SystemMonitor", "settings.json");
        var store = new FileSystemMonitorSettingsStore(path);
        var refreshTime = new DateTime(2026, 5, 11, 14, 15, 16);

        await store.SaveAsync(new PortableSystemMonitorSettings { LastRefreshTime = refreshTime });
        var loaded = await store.LoadAsync();

        Assert.Equal(refreshTime, loaded.LastRefreshTime);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task System_monitor_controller_refreshes_snapshot_and_persists_last_refresh_time()
    {
        var refreshTime = new DateTime(2026, 5, 11, 15, 16, 17);
        var settings = new PortableSystemMonitorSettings();
        var savedTimes = new List<DateTime>();
        var service = new PortableSystemMonitorService(new FakeSystemMonitorProbe
        {
            CpuUsagePercent = 27.5,
            TotalMemoryBytes = 8L * 1024 * 1024 * 1024,
            AvailableMemoryBytes = 2L * 1024 * 1024 * 1024
        });
        var controller = new PortableSystemMonitorController(
            service,
            settings,
            (updatedSettings, _) =>
            {
                savedTimes.Add(updatedSettings.LastRefreshTime);
                return Task.CompletedTask;
            },
            () => refreshTime);

        var snapshot = await controller.RefreshAsync();

        Assert.Equal(27.5, snapshot.Cpu.UsagePercent);
        Assert.Equal(75, snapshot.Memory.UsagePercent);
        Assert.Equal(refreshTime, settings.LastRefreshTime);
        Assert.Equal([refreshTime], savedTimes);
        Assert.Equal(snapshot, controller.CurrentSnapshot);
    }

    [Fact]
    public void MacOSSystemMonitorParser_maps_cpu_battery_temperature_and_fan_output()
    {
        const string topOutput = "CPU usage: 12.34% user, 5.66% sys, 82.00% idle";
        const string batteryOutput = """
            Now drawing from 'Battery Power'
             -InternalBattery-0 (id=1234567)    87%; discharging; 4:12 remaining present: true
            """;
        const string powermetricsOutput = """
            SMC sensors:
            CPU die temperature: 61.25 C
            Fan: 1925 rpm
            """;

        var metrics = MacOSSystemMonitorParser.Parse(topOutput, batteryOutput, powermetricsOutput);

        Assert.Equal(18.0, metrics.CpuUsagePercent);
        Assert.Equal(61.25, metrics.CpuTemperatureCelsius);
        Assert.Collection(
            metrics.Sensors,
            sensor =>
            {
                Assert.Equal("电池电量", sensor.Name);
                Assert.Equal("电源", sensor.Type);
                Assert.Equal("87%", sensor.Value);
                Assert.Equal("✅ 正常", sensor.Status);
            },
            sensor =>
            {
                Assert.Equal("CPU Die", sensor.Name);
                Assert.Equal("温度", sensor.Type);
                Assert.Equal("61.3 °C", sensor.Value);
            },
            sensor =>
            {
                Assert.Equal("系统风扇 1", sensor.Name);
                Assert.Equal("风扇", sensor.Type);
                Assert.Equal("1925 RPM", sensor.Value);
            });
    }

    [Fact]
    public void MacOSSystemMonitorParser_maps_multiple_temperature_sensors()
    {
        const string powermetricsOutput = """
            SMC sensors:
            CPU die temperature: 61.25 C
            GPU die temperature: 54.5 C
            Ambient temperature: 29.25 C
            Fan 1: 1925 rpm
            Fan 2: 2010 rpm
            """;

        var metrics = MacOSSystemMonitorParser.Parse(string.Empty, string.Empty, powermetricsOutput);

        Assert.Equal(61.25, metrics.CpuTemperatureCelsius);
        Assert.Collection(
            metrics.Sensors,
            sensor =>
            {
                Assert.Equal("CPU Die", sensor.Name);
                Assert.Equal("温度", sensor.Type);
                Assert.Equal("61.3 °C", sensor.Value);
            },
            sensor =>
            {
                Assert.Equal("GPU Die", sensor.Name);
                Assert.Equal("温度", sensor.Type);
                Assert.Equal("54.5 °C", sensor.Value);
            },
            sensor =>
            {
                Assert.Equal("Ambient", sensor.Name);
                Assert.Equal("温度", sensor.Type);
                Assert.Equal("29.3 °C", sensor.Value);
            },
            sensor =>
            {
                Assert.Equal("系统风扇 1", sensor.Name);
                Assert.Equal("风扇", sensor.Type);
                Assert.Equal("1925 RPM", sensor.Value);
            },
            sensor =>
            {
                Assert.Equal("系统风扇 2", sensor.Name);
                Assert.Equal("风扇", sensor.Type);
                Assert.Equal("2010 RPM", sensor.Value);
            });
    }

    [Fact]
    public void MacOSSystemMonitorProbe_runs_injectable_commands_and_exposes_metrics()
    {
        var runner = new RecordingSystemMonitorCommandRunner(
            topOutput: "CPU usage: 10.00% user, 2.50% sys, 87.50% idle",
            batteryOutput: " -InternalBattery-0 (id=1) 19%; discharging; 0:45 remaining present: true",
            powermetricsOutput: "CPU die temperature: 71.0 C\nFan: 2400 rpm\n");
        var probe = new MacOSSystemMonitorProbe(runner);

        Assert.Equal(12.5, probe.CpuUsagePercent);
        Assert.Equal(71.0, probe.CpuTemperatureCelsius);
        Assert.Equal(3, probe.Sensors.Count);
        Assert.Equal(19, probe.Sensors[0].Value == "19%" ? 19 : 0);
        Assert.Equal(3, runner.Invocations.Count);
        Assert.Equal("/usr/bin/top", runner.Invocations[0].FileName);
        Assert.Equal(["-l", "1", "-n", "0"], runner.Invocations[0].Arguments);
        Assert.Equal("/usr/bin/pmset", runner.Invocations[1].FileName);
        Assert.Equal(["-g", "batt"], runner.Invocations[1].Arguments);
        Assert.Equal("/usr/bin/powermetrics", runner.Invocations[2].FileName);
        Assert.Equal(["--samplers", "smc", "-n", "1"], runner.Invocations[2].Arguments);
    }

    [Fact]
    public void MacOSSystemMonitorProbe_surfaces_powermetrics_permission_failure_as_sensor()
    {
        var runner = new PermissionDeniedPowermetricsRunner();
        var probe = new MacOSSystemMonitorProbe(runner, new FakeSystemMonitorProbe());

        var sensor = Assert.Single(probe.Sensors);

        Assert.Equal("powermetrics 权限", sensor.Name);
        Assert.Equal("权限", sensor.Type);
        Assert.Equal("不可用", sensor.Value);
        Assert.Contains("需要管理员权限", sensor.Status);
        Assert.Equal("lock", sensor.Icon);
    }

    private sealed class FakeSystemMonitorProbe : IPortableSystemMonitorProbe
    {
        public double? CpuFrequencyMhz { get; init; }
        public double? CpuUsagePercent { get; init; }
        public double? CpuTemperatureCelsius { get; init; }
        public long? TotalMemoryBytes { get; init; }
        public long? AvailableMemoryBytes { get; init; }
        public IReadOnlyList<ProbeDiskUsage> Disks { get; init; } = [];
        public IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces { get; init; } = [];
        public IReadOnlyList<ProbeSensorReading> Sensors { get; init; } = [];
    }

    private sealed class RecordingSystemMonitorCommandRunner : IMacOSSystemMonitorCommandRunner
    {
        private readonly string _topOutput;
        private readonly string _batteryOutput;
        private readonly string _powermetricsOutput;

        public RecordingSystemMonitorCommandRunner(
            string topOutput,
            string batteryOutput,
            string powermetricsOutput)
        {
            _topOutput = topOutput;
            _batteryOutput = batteryOutput;
            _powermetricsOutput = powermetricsOutput;
        }

        public List<MacOSSystemMonitorCommandInvocation> Invocations { get; } = [];

        public MacOSSystemMonitorCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new MacOSSystemMonitorCommandInvocation(fileName, arguments.ToArray()));
            return Path.GetFileName(fileName) switch
            {
                "top" => new MacOSSystemMonitorCommandResult(0, _topOutput, string.Empty),
                "pmset" => new MacOSSystemMonitorCommandResult(0, _batteryOutput, string.Empty),
                "powermetrics" => new MacOSSystemMonitorCommandResult(0, _powermetricsOutput, string.Empty),
                _ => new MacOSSystemMonitorCommandResult(1, string.Empty, "unexpected")
            };
        }
    }

    private sealed class PermissionDeniedPowermetricsRunner : IMacOSSystemMonitorCommandRunner
    {
        public MacOSSystemMonitorCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            return Path.GetFileName(fileName) switch
            {
                "powermetrics" => new MacOSSystemMonitorCommandResult(
                    1,
                    string.Empty,
                    "powermetrics must be run as root"),
                _ => new MacOSSystemMonitorCommandResult(0, string.Empty, string.Empty)
            };
        }
    }
}
