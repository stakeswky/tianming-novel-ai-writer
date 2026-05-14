using TM.Framework.SystemMonitor;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class MacOSSystemMonitorTests
{
    [Fact]
    public void System_monitor_service_reads_registered_probe()
    {
        var probe = new CountingSystemMonitorProbe();
        var service = new PortableSystemMonitorService(probe);

        var snapshot = service.CaptureSnapshot();

        Assert.True(probe.CpuUsageReads > 0);
        Assert.Equal(42.5, snapshot.Cpu.UsagePercent);
        Assert.Equal("8 GB", snapshot.Memory.AvailableMemory);
    }

    private sealed class CountingSystemMonitorProbe : IPortableSystemMonitorProbe
    {
        public int CpuUsageReads { get; private set; }

        public double? CpuFrequencyMhz => 3200;

        public double? CpuUsagePercent
        {
            get
            {
                CpuUsageReads++;
                return 42.5;
            }
        }

        public double? CpuTemperatureCelsius => 55;

        public long? TotalMemoryBytes => 16L * 1024 * 1024 * 1024;

        public long? AvailableMemoryBytes => 8L * 1024 * 1024 * 1024;

        public IReadOnlyList<ProbeDiskUsage> Disks => [];

        public IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces => [];

        public IReadOnlyList<ProbeSensorReading> Sensors => [];
    }
}
