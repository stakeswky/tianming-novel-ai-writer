using TM.Framework.SystemInfo;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableRuntimeDiagnosticsTests
{
    [Fact]
    public void Runtime_environment_snapshot_uses_probe_data_and_original_display_labels()
    {
        var service = new PortableRuntimeEnvironmentService(new FakeRuntimeEnvironmentProbe
        {
            RuntimeVersion = ".NET 8.0.5",
            ClrVersion = "8.0.5",
            RuntimeIdentifier = "osx-arm64",
            IsServerGc = false,
            CultureDisplayName = "中文(中国)",
            TimeZoneDisplayName = "(UTC+08:00) Beijing, Chongqing, Hong Kong, Urumqi",
            Assemblies =
            [
                new PortableRuntimeAssemblyInfo { Name = "Tianming.Framework", Version = "1.0.0.0", Location = "/app/Tianming.Framework.dll" },
                new PortableRuntimeAssemblyInfo { Name = "System.Text.Json", Version = "8.0.0.0", Location = "/shared/System.Text.Json.dll" }
            ],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PATH"] = "/usr/bin",
                ["HOME"] = "/Users/writer"
            }
        });

        var snapshot = service.CollectSnapshot();

        Assert.Equal(".NET 8.0.5", snapshot.RuntimeVersion);
        Assert.Equal("8.0.5", snapshot.ClrVersion);
        Assert.Equal("osx-arm64", snapshot.FrameworkDescription);
        Assert.Equal("工作站模式", snapshot.GcMode);
        Assert.Equal("中文(中国)", snapshot.CurrentCulture);
        Assert.Equal("(UTC+08:00) Beijing, Chongqing, Hong Kong, Urumqi", snapshot.TimeZone);
        Assert.Equal(["System.Text.Json", "Tianming.Framework"], snapshot.Assemblies.Select(a => a.Name).ToArray());
        Assert.Equal(["HOME", "PATH"], snapshot.EnvironmentVariables.Select(v => v.Name).ToArray());
    }

    [Fact]
    public void Runtime_environment_settings_filter_system_assemblies_paths_and_environment_variables()
    {
        var service = new PortableRuntimeEnvironmentService(new FakeRuntimeEnvironmentProbe
        {
            Assemblies =
            [
                new PortableRuntimeAssemblyInfo { Name = "Microsoft.Extensions.Hosting", Version = "8.0.0.0", Location = "/shared/Microsoft.Extensions.Hosting.dll" },
                new PortableRuntimeAssemblyInfo { Name = "Tianming.AI", Version = "1.0.0.0", Location = "/app/Tianming.AI.dll" }
            ],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PATH"] = "/usr/bin",
                ["TIANMING_HOME"] = "/Users/writer/.tianming"
            }
        });

        var snapshot = service.CollectSnapshot(new PortableRuntimeEnvironmentSettings
        {
            ShowSystemAssemblies = false,
            ShowUserAssemblies = true,
            ShowFullPath = false,
            EnvironmentVariableFilter = "tianming"
        });

        var assembly = Assert.Single(snapshot.Assemblies);
        Assert.Equal("Tianming.AI", assembly.Name);
        Assert.Equal("Tianming.AI.dll", assembly.Location);
        var env = Assert.Single(snapshot.EnvironmentVariables);
        Assert.Equal("TIANMING_HOME", env.Name);
    }

    [Fact]
    public void Runtime_environment_report_matches_original_core_sections()
    {
        var service = new PortableRuntimeEnvironmentService(new FakeRuntimeEnvironmentProbe());
        var report = service.BuildReport(new PortableRuntimeEnvironmentSnapshot
        {
            RuntimeVersion = ".NET 8.0.5",
            ClrVersion = "8.0.5",
            GcMode = "工作站模式",
            CurrentCulture = "中文(中国)",
            TimeZone = "Asia/Shanghai",
            Assemblies = [new PortableRuntimeAssemblyInfo { Name = "Tianming.Framework", Version = "1.0.0.0" }]
        });

        Assert.Contains("==================== 运行环境信息报告 ====================", report);
        Assert.Contains("运行时版本: .NET 8.0.5", report);
        Assert.Contains("CLR版本: 8.0.5", report);
        Assert.Contains("GC模式: 工作站模式", report);
        Assert.Contains("【已加载程序集】", report);
        Assert.Contains("Tianming.Framework - 1.0.0.0", report);
    }

    [Fact]
    public void Runtime_environment_report_export_planner_builds_original_default_path()
    {
        var exportTime = new DateTime(2026, 5, 11, 10, 9, 8);

        var path = PortableRuntimeEnvironmentReportExportPlanner.BuildDefaultPath(
            "/Users/writer/Library/Application Support/Tianming",
            exportTime);

        Assert.Equal(
            "/Users/writer/Library/Application Support/Tianming/Framework/SystemSettings/Info/RuntimeEnv/runtime_env_export_20260511_100908.txt",
            path);
    }

    [Fact]
    public async Task Runtime_environment_report_exporter_writes_report_atomically()
    {
        using var workspace = new TempDirectory();
        var service = new PortableRuntimeEnvironmentService(new FakeRuntimeEnvironmentProbe());
        var snapshot = new PortableRuntimeEnvironmentSnapshot
        {
            RuntimeVersion = ".NET 8.0.5",
            ClrVersion = "8.0.5",
            GcMode = "工作站模式",
            CurrentCulture = "中文(中国)",
            TimeZone = "Asia/Shanghai",
            Assemblies = [new PortableRuntimeAssemblyInfo { Name = "Tianming.Framework", Version = "1.0.0.0" }]
        };
        var targetPath = Path.Combine(workspace.Path, "exports", "runtime_env_export.txt");

        var result = await PortableRuntimeEnvironmentReportExporter.ExportAsync(service, snapshot, targetPath);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.Contains("运行时版本: .NET 8.0.5", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public void Diagnostic_health_check_reports_original_status_and_suggestions()
    {
        var service = new PortableDiagnosticInfoService();
        var result = service.EvaluateHealth(
            new PortableDiagnosticSnapshot
            {
                MemoryUsageMB = 2048,
                ThreadCount = 150,
                Gen2Collections = 120
            },
            [
                new PortableDiagnosticDriveInfo { Name = "/", TotalBytes = 1000, AvailableFreeBytes = 50, IsReady = true }
            ],
            new PortableDiagnosticInfoSettings { DiskSpaceWarningThresholdPercent = 90 });

        Assert.Equal("需要关注", result.HealthStatus);
        Assert.Equal("#F44336", result.HealthStatusColor);
        Assert.Equal(4, result.IssueCount);
        Assert.Contains(result.Suggestions, s => s.Contains("驱动器 / 空间不足"));
        Assert.Contains(result.Suggestions, s => s.Contains("应用内存使用较高"));
        Assert.Contains(result.Suggestions, s => s.Contains("2代垃圾回收次数较多"));
        Assert.Contains(result.Suggestions, s => s.Contains("线程数较多"));
    }

    [Fact]
    public void Diagnostic_report_exports_original_sections()
    {
        var service = new PortableDiagnosticInfoService(() => new DateTime(2026, 5, 10, 9, 30, 0));
        var report = service.BuildReport(
            new PortableDiagnosticSnapshot
            {
                CpuUsage = 12.5,
                MemoryUsageMB = 512,
                ThreadCount = 12,
                HandleCount = 34,
                Gen0Collections = 1,
                Gen1Collections = 2,
                Gen2Collections = 3
            },
            new PortableDiagnosticHealthReport
            {
                HealthStatus = "健康",
                Suggestions = ["系统运行状态良好"]
            },
            new PortableDiagnosticRuntimeContext
            {
                AppVersion = "1.2.3.4",
                ProcessPath = "/Applications/Tianming.app",
                OperatingSystem = "macOS 15.4",
                DotNetVersion = "8.0.5",
                MachineName = "Mac",
                UserName = "writer",
                StorageRoot = "/Users/writer/Library/Application Support/Tianming"
            });

        Assert.Contains("==================== 诊断信息报告 ====================", report);
        Assert.Contains("生成时间: 2026-05-10 09:30:00", report);
        Assert.Contains("【应用信息】", report);
        Assert.Contains("版本: 1.2.3.4", report);
        Assert.Contains("【运行环境】", report);
        Assert.Contains("操作系统: macOS 15.4", report);
        Assert.Contains("【实时诊断数据】", report);
        Assert.Contains("CPU使用率: 12.5%", report);
        Assert.Contains("【GC统计】", report);
        Assert.Contains("2代回收: 3 次", report);
        Assert.Contains("【健康状态】", report);
        Assert.Contains("状态: 健康", report);
    }

    [Fact]
    public void Diagnostic_report_export_planner_builds_original_default_path()
    {
        var exportTime = new DateTime(2026, 5, 11, 11, 10, 9);

        var path = PortableDiagnosticReportExportPlanner.BuildDefaultPath(
            "/Users/writer/Library/Application Support/Tianming",
            exportTime);

        Assert.Equal(
            "/Users/writer/Library/Application Support/Tianming/Framework/SystemSettings/Info/DiagnosticInfo/diagnostic_report_20260511_111009.txt",
            path);
    }

    [Fact]
    public async Task Diagnostic_report_exporter_writes_report_atomically()
    {
        using var workspace = new TempDirectory();
        var service = new PortableDiagnosticInfoService(() => new DateTime(2026, 5, 10, 9, 30, 0));
        var snapshot = new PortableDiagnosticSnapshot
        {
            CpuUsage = 12.5,
            MemoryUsageMB = 512,
            ThreadCount = 12,
            HandleCount = 34,
            Gen0Collections = 1,
            Gen1Collections = 2,
            Gen2Collections = 3
        };
        var health = new PortableDiagnosticHealthReport
        {
            HealthStatus = "健康",
            Suggestions = ["系统运行状态良好"]
        };
        var context = new PortableDiagnosticRuntimeContext
        {
            AppVersion = "1.2.3.4",
            ProcessPath = "/Applications/Tianming.app",
            OperatingSystem = "macOS 15.4",
            DotNetVersion = "8.0.5",
            MachineName = "Mac",
            UserName = "writer",
            StorageRoot = "/Users/writer/Library/Application Support/Tianming"
        };
        var targetPath = Path.Combine(workspace.Path, "exports", "diagnostic_report.txt");

        var result = await PortableDiagnosticReportExporter.ExportAsync(
            service,
            snapshot,
            health,
            context,
            targetPath);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.Contains("生成时间: 2026-05-10 09:30:00", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task Diagnostic_controller_refreshes_snapshot_and_reports_auto_refresh_plan()
    {
        var settings = new PortableDiagnosticInfoSettings
        {
            EnableAutoRefresh = true,
            RefreshIntervalSeconds = 7
        };
        var probe = new FakeDiagnosticInfoProbe
        {
            Snapshot = new PortableDiagnosticSnapshot
            {
                CpuUsage = 9.5,
                MemoryUsageMB = 768,
                ThreadCount = 16,
                HandleCount = 42,
                Gen0Collections = 3,
                Gen1Collections = 2,
                Gen2Collections = 1
            }
        };
        var controller = new PortableDiagnosticInfoController(
            new PortableDiagnosticInfoService(),
            settings,
            probe,
            "/Users/writer/Library/Application Support/Tianming");

        var plan = controller.GetAutoRefreshPlan();
        var snapshot = await controller.RefreshAsync();

        Assert.True(plan.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(7), plan.Interval);
        Assert.Equal(1, probe.SnapshotReads);
        Assert.Equal(768, snapshot.MemoryUsageMB);
        Assert.Equal(16, controller.CurrentSnapshot.ThreadCount);
    }

    [Fact]
    public async Task Diagnostic_controller_runs_health_check_and_exports_default_report()
    {
        using var workspace = new TempDirectory();
        var settings = new PortableDiagnosticInfoSettings
        {
            DiskSpaceWarningThresholdPercent = 90
        };
        var probe = new FakeDiagnosticInfoProbe
        {
            Snapshot = new PortableDiagnosticSnapshot
            {
                CpuUsage = 12.5,
                MemoryUsageMB = 2048,
                ThreadCount = 12,
                HandleCount = 34,
                Gen0Collections = 1,
                Gen1Collections = 2,
                Gen2Collections = 3
            },
            Drives =
            [
                new PortableDiagnosticDriveInfo
                {
                    Name = "/",
                    TotalBytes = 1000,
                    AvailableFreeBytes = 50,
                    IsReady = true
                }
            ],
            RuntimeContext = new PortableDiagnosticRuntimeContext
            {
                AppVersion = "1.2.3.4",
                ProcessPath = "/Applications/Tianming.app",
                OperatingSystem = "macOS 15.4",
                DotNetVersion = "8.0.5",
                MachineName = "Mac",
                UserName = "writer"
            }
        };
        var controller = new PortableDiagnosticInfoController(
            new PortableDiagnosticInfoService(() => new DateTime(2026, 5, 10, 9, 30, 0)),
            settings,
            probe,
            workspace.Path,
            () => new DateTime(2026, 5, 11, 12, 13, 14));

        await controller.RefreshAsync();
        var health = controller.RunHealthCheck();
        var result = await controller.ExportReportAsync();

        var expectedPath = Path.Combine(
            workspace.Path,
            "Framework",
            "SystemSettings",
            "Info",
            "DiagnosticInfo",
            "diagnostic_report_20260511_121314.txt");
        Assert.Equal("良好", health.HealthStatus);
        Assert.True(result.Success);
        Assert.Equal(expectedPath, result.FilePath);
        Assert.Contains("状态: 良好", await File.ReadAllTextAsync(expectedPath));
        Assert.False(File.Exists(expectedPath + ".tmp"));
    }

    private sealed class FakeRuntimeEnvironmentProbe : IPortableRuntimeEnvironmentProbe
    {
        public string RuntimeVersion { get; init; } = string.Empty;
        public string ClrVersion { get; init; } = string.Empty;
        public string RuntimeIdentifier { get; init; } = string.Empty;
        public bool IsServerGc { get; init; }
        public string CultureDisplayName { get; init; } = string.Empty;
        public string TimeZoneDisplayName { get; init; } = string.Empty;
        public IReadOnlyList<PortableRuntimeAssemblyInfo> Assemblies { get; init; } = [];
        public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();
    }

    private sealed class FakeDiagnosticInfoProbe : IPortableDiagnosticInfoProbe
    {
        public PortableDiagnosticSnapshot Snapshot { get; init; } = new();
        public IReadOnlyList<PortableDiagnosticDriveInfo> Drives { get; init; } = [];
        public PortableDiagnosticRuntimeContext RuntimeContext { get; init; } = new();
        public int SnapshotReads { get; private set; }

        public PortableDiagnosticSnapshot CollectSnapshot()
        {
            SnapshotReads++;
            return Snapshot;
        }

        public IReadOnlyList<PortableDiagnosticDriveInfo> CollectDrives()
        {
            return Drives;
        }

        public PortableDiagnosticRuntimeContext CollectRuntimeContext(string storageRoot)
        {
            return new PortableDiagnosticRuntimeContext
            {
                AppVersion = RuntimeContext.AppVersion,
                ProcessPath = RuntimeContext.ProcessPath,
                OperatingSystem = RuntimeContext.OperatingSystem,
                DotNetVersion = RuntimeContext.DotNetVersion,
                MachineName = RuntimeContext.MachineName,
                UserName = RuntimeContext.UserName,
                StorageRoot = string.IsNullOrWhiteSpace(RuntimeContext.StorageRoot)
                    ? storageRoot
                    : RuntimeContext.StorageRoot
            };
        }
    }
}
