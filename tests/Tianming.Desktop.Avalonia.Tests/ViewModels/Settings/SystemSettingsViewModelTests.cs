using TM.Framework.Appearance;
using TM.Framework.Preferences;
using TM.Framework.SystemInfo;
using TM.Framework.SystemMonitor;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class SystemSettingsViewModelTests
{
    [Fact]
    public void Ctor_loads_initial_state_from_settings_data()
    {
        var vm = BuildVm();

        Assert.True(vm.WindowWidth > 0);
        Assert.True(vm.WindowHeight > 0);
        Assert.False(string.IsNullOrEmpty(vm.Language));
        Assert.False(string.IsNullOrEmpty(vm.OsDescription));
        Assert.False(string.IsNullOrEmpty(vm.RuntimeVersion));
        Assert.True(vm.ProcessorCount > 0);
        Assert.False(string.IsNullOrEmpty(vm.ProxyStatusText));
        Assert.False(string.IsNullOrEmpty(vm.LogStatusText));
    }

    [Fact]
    public void Setting_ShowFunctionBar_writes_back_to_DisplaySettings()
    {
        var display = PortableDisplaySettings.CreateDefault();
        var vm = BuildVm(display: display);

        vm.ShowFunctionBar = false;

        Assert.False(display.ShowFunctionBar);
    }

    [Fact]
    public void Setting_Language_writes_back_to_LocaleSettings()
    {
        var locale = PortableLocaleSettings.CreateDefault();
        var vm = BuildVm(locale: locale);

        vm.Language = "en-US";

        Assert.Equal("en-US", locale.Language);
    }

    [Fact]
    public void Setting_WindowWidth_writes_back_to_UIResolutionSettings()
    {
        var uiRes = PortableUIResolutionSettings.CreateDefault();
        var vm = BuildVm(uiResolution: uiRes);

        vm.WindowWidth = 1440;

        Assert.Equal(1440, uiRes.WindowWidth);
    }

    [Fact]
    public void ScanDataCleanupCommand_populates_scan_result_text()
    {
        var vm = BuildVm();
        var before = vm.DataCleanupScanResult;

        vm.ScanDataCleanupCommand.Execute(null);

        Assert.NotEqual(before, vm.DataCleanupScanResult);
        Assert.Contains("扫描完成", vm.DataCleanupScanResult);
    }

    [Fact]
    public void RefreshDiagnosticsNowCommand_updates_process_memory()
    {
        var vm = BuildVm();

        vm.RefreshDiagnosticsNowCommand.Execute(null);

        Assert.True(vm.ProcessWorkingSetMb > 0);
        Assert.True(vm.ThreadCount > 0);
    }

    private static SystemSettingsViewModel BuildVm(
        PortableUIResolutionSettings? uiResolution = null,
        PortableDisplaySettings? display = null,
        PortableLocaleSettings? locale = null)
    {
        var loading = PortableLoadingAnimationSettings.CreateDefault();
        var runtimeEnv = new PortableRuntimeEnvironmentSettings();
        var monitorService = new PortableSystemMonitorService(new StubMonitorProbe());
        return new SystemSettingsViewModel(
            uiResolution ?? PortableUIResolutionSettings.CreateDefault(),
            loading,
            display ?? PortableDisplaySettings.CreateDefault(),
            locale ?? PortableLocaleSettings.CreateDefault(),
            runtimeEnv,
            monitorService);
    }

    private sealed class StubMonitorProbe : IPortableSystemMonitorProbe
    {
        public double? CpuFrequencyMhz => null;
        public double? CpuUsagePercent => null;
        public double? CpuTemperatureCelsius => null;
        public long? TotalMemoryBytes => null;
        public long? AvailableMemoryBytes => null;
        public System.Collections.Generic.IReadOnlyList<ProbeDiskUsage> Disks => System.Array.Empty<ProbeDiskUsage>();
        public System.Collections.Generic.IReadOnlyList<ProbeNetworkTraffic> NetworkInterfaces => System.Array.Empty<ProbeNetworkTraffic>();
        public System.Collections.Generic.IReadOnlyList<ProbeSensorReading> Sensors => System.Array.Empty<ProbeSensorReading>();
    }
}
