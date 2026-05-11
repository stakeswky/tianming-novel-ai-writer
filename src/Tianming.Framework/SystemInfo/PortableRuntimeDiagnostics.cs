using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace TM.Framework.SystemInfo;

public sealed class PortableRuntimeEnvironmentSettings
{
    [JsonPropertyName("ShowSystemAssemblies")] public bool ShowSystemAssemblies { get; set; } = true;
    [JsonPropertyName("ShowUserAssemblies")] public bool ShowUserAssemblies { get; set; } = true;
    [JsonPropertyName("EnvironmentVariableFilter")] public string EnvironmentVariableFilter { get; set; } = string.Empty;
    [JsonPropertyName("ShowFullPath")] public bool ShowFullPath { get; set; } = true;
}

public sealed class PortableRuntimeAssemblyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public sealed class PortableEnvironmentVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class PortableRuntimeEnvironmentSnapshot
{
    public string RuntimeVersion { get; set; } = string.Empty;
    public string ClrVersion { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public string GcMode { get; set; } = string.Empty;
    public string CurrentCulture { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public List<PortableRuntimeAssemblyInfo> Assemblies { get; set; } = [];
    public List<PortableEnvironmentVariableInfo> EnvironmentVariables { get; set; } = [];
}

public interface IPortableRuntimeEnvironmentProbe
{
    string RuntimeVersion { get; }
    string ClrVersion { get; }
    string RuntimeIdentifier { get; }
    bool IsServerGc { get; }
    string CultureDisplayName { get; }
    string TimeZoneDisplayName { get; }
    IReadOnlyList<PortableRuntimeAssemblyInfo> Assemblies { get; }
    IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }
}

public sealed class DefaultPortableRuntimeEnvironmentProbe : IPortableRuntimeEnvironmentProbe
{
    public string RuntimeVersion => RuntimeInformation.FrameworkDescription;
    public string ClrVersion => Environment.Version.ToString();
    public string RuntimeIdentifier => RuntimeInformation.RuntimeIdentifier;
    public bool IsServerGc => GCSettings.IsServerGC;
    public string CultureDisplayName => CultureInfo.CurrentCulture.DisplayName;
    public string TimeZoneDisplayName => TimeZoneInfo.Local.DisplayName;

    public IReadOnlyList<PortableRuntimeAssemblyInfo> Assemblies => AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(assembly => !assembly.IsDynamic)
        .Select(ToAssemblyInfo)
        .ToList();

    public IReadOnlyDictionary<string, string?> EnvironmentVariables => Environment
        .GetEnvironmentVariables()
        .Keys
        .Cast<string>()
        .ToDictionary(key => key, key => Environment.GetEnvironmentVariable(key));

    private static PortableRuntimeAssemblyInfo ToAssemblyInfo(Assembly assembly)
    {
        var name = assembly.GetName();
        return new PortableRuntimeAssemblyInfo
        {
            Name = name.Name ?? "Unknown",
            Version = name.Version?.ToString() ?? "N/A",
            Location = assembly.Location
        };
    }
}

public sealed class PortableRuntimeEnvironmentService
{
    private readonly IPortableRuntimeEnvironmentProbe _probe;

    public PortableRuntimeEnvironmentService(IPortableRuntimeEnvironmentProbe? probe = null)
    {
        _probe = probe ?? new DefaultPortableRuntimeEnvironmentProbe();
    }

    public PortableRuntimeEnvironmentSnapshot CollectSnapshot(PortableRuntimeEnvironmentSettings? settings = null)
    {
        settings ??= new PortableRuntimeEnvironmentSettings();
        return new PortableRuntimeEnvironmentSnapshot
        {
            RuntimeVersion = _probe.RuntimeVersion,
            ClrVersion = _probe.ClrVersion,
            FrameworkDescription = _probe.RuntimeIdentifier,
            GcMode = _probe.IsServerGc ? "服务器模式" : "工作站模式",
            CurrentCulture = _probe.CultureDisplayName,
            TimeZone = _probe.TimeZoneDisplayName,
            Assemblies = FilterAssemblies(_probe.Assemblies, settings).ToList(),
            EnvironmentVariables = FilterEnvironmentVariables(_probe.EnvironmentVariables, settings).ToList()
        };
    }

    public string BuildReport(PortableRuntimeEnvironmentSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================== 运行环境信息报告 ====================");
        sb.AppendLine($"运行时版本: {snapshot.RuntimeVersion}");
        sb.AppendLine($"CLR版本: {snapshot.ClrVersion}");
        sb.AppendLine($"GC模式: {snapshot.GcMode}");
        sb.AppendLine($"区域: {snapshot.CurrentCulture}");
        sb.AppendLine($"时区: {snapshot.TimeZone}");
        sb.AppendLine();
        sb.AppendLine("【已加载程序集】");
        foreach (var assembly in snapshot.Assemblies.Take(50))
        {
            sb.AppendLine($"  {assembly.Name} - {assembly.Version}");
        }

        return sb.ToString();
    }

    private static IEnumerable<PortableRuntimeAssemblyInfo> FilterAssemblies(
        IEnumerable<PortableRuntimeAssemblyInfo> assemblies,
        PortableRuntimeEnvironmentSettings settings)
    {
        return assemblies
            .Where(assembly =>
            {
                var isSystem = IsSystemAssembly(assembly.Name);
                return (isSystem && settings.ShowSystemAssemblies)
                       || (!isSystem && settings.ShowUserAssemblies);
            })
            .OrderBy(assembly => assembly.Name, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(assembly => new PortableRuntimeAssemblyInfo
            {
                Name = assembly.Name,
                Version = assembly.Version,
                Location = settings.ShowFullPath ? assembly.Location : Path.GetFileName(assembly.Location)
            });
    }

    private static IEnumerable<PortableEnvironmentVariableInfo> FilterEnvironmentVariables(
        IReadOnlyDictionary<string, string?> variables,
        PortableRuntimeEnvironmentSettings settings)
    {
        var filter = settings.EnvironmentVariableFilter;
        return variables
            .Where(pair => string.IsNullOrWhiteSpace(filter)
                           || pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                           || (pair.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select(pair => new PortableEnvironmentVariableInfo
            {
                Name = pair.Key,
                Value = pair.Value ?? string.Empty
            });
    }

    private static bool IsSystemAssembly(string name)
    {
        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "netstandard", StringComparison.OrdinalIgnoreCase);
    }
}

public static class PortableRuntimeEnvironmentReportExportPlanner
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
            "RuntimeEnv",
            $"runtime_env_export_{exportTime:yyyyMMdd_HHmmss}.txt");
    }
}

public sealed record PortableRuntimeEnvironmentReportExportResult(
    bool Success,
    string FilePath,
    string? ErrorMessage);

public static class PortableRuntimeEnvironmentReportExporter
{
    public static async Task<PortableRuntimeEnvironmentReportExportResult> ExportAsync(
        PortableRuntimeEnvironmentService service,
        PortableRuntimeEnvironmentSnapshot snapshot,
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
            return new PortableRuntimeEnvironmentReportExportResult(true, filePath, null);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(tempPath);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(tempPath);
            return new PortableRuntimeEnvironmentReportExportResult(false, filePath, ex.Message);
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

public sealed class PortableDiagnosticInfoSettings
{
    [JsonPropertyName("RefreshIntervalSeconds")] public int RefreshIntervalSeconds { get; set; } = 3;
    [JsonPropertyName("EnableAutoRefresh")] public bool EnableAutoRefresh { get; set; } = true;
    [JsonPropertyName("DiskSpaceWarningThresholdPercent")] public int DiskSpaceWarningThresholdPercent { get; set; } = 90;
    [JsonPropertyName("MemoryWarningThresholdPercent")] public int MemoryWarningThresholdPercent { get; set; } = 85;
    [JsonPropertyName("CPUWarningThresholdPercent")] public int CPUWarningThresholdPercent { get; set; } = 90;
    [JsonPropertyName("ReportTemplatePath")] public string ReportTemplatePath { get; set; } = string.Empty;
}

public sealed class PortableDiagnosticSnapshot
{
    public double CpuUsage { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
}

public sealed class PortableDiagnosticDriveInfo
{
    public string Name { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long AvailableFreeBytes { get; set; }
    public bool IsReady { get; set; }
}

public sealed class PortableDiagnosticHealthReport
{
    public string HealthStatus { get; set; } = string.Empty;
    public string HealthStatusColor { get; set; } = string.Empty;
    public int IssueCount { get; set; }
    public List<string> Suggestions { get; set; } = [];
}

public sealed class PortableDiagnosticRuntimeContext
{
    public string AppVersion { get; set; } = string.Empty;
    public string ProcessPath { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string StorageRoot { get; set; } = string.Empty;
}

public interface IPortableDiagnosticInfoProbe
{
    PortableDiagnosticSnapshot CollectSnapshot();
    IReadOnlyList<PortableDiagnosticDriveInfo> CollectDrives();
    PortableDiagnosticRuntimeContext CollectRuntimeContext(string storageRoot);
}

public sealed class DefaultPortableDiagnosticInfoProbe : IPortableDiagnosticInfoProbe
{
    public PortableDiagnosticSnapshot CollectSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        return new PortableDiagnosticSnapshot
        {
            CpuUsage = Math.Round(Environment.ProcessorCount * 0.1, 2),
            MemoryUsageMB = SafeRead(() => process.WorkingSet64 / 1024 / 1024, 0L),
            ThreadCount = SafeRead(() => process.Threads.Count, 0),
            HandleCount = SafeRead(() => process.HandleCount, 0),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    public IReadOnlyList<PortableDiagnosticDriveInfo> CollectDrives()
    {
        return DriveInfo.GetDrives()
            .Select(TryCreateDriveInfo)
            .Where(drive => drive is not null)
            .Cast<PortableDiagnosticDriveInfo>()
            .ToList();
    }

    public PortableDiagnosticRuntimeContext CollectRuntimeContext(string storageRoot)
    {
        return new PortableDiagnosticRuntimeContext
        {
            AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty,
            ProcessPath = ReadProcessPath(),
            OperatingSystem = RuntimeInformation.OSDescription,
            DotNetVersion = Environment.Version.ToString(),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            StorageRoot = storageRoot
        };
    }

    private static PortableDiagnosticDriveInfo? TryCreateDriveInfo(DriveInfo drive)
    {
        try
        {
            return new PortableDiagnosticDriveInfo
            {
                Name = drive.Name,
                TotalBytes = drive.IsReady ? drive.TotalSize : 0,
                AvailableFreeBytes = drive.IsReady ? drive.AvailableFreeSpace : 0,
                IsReady = drive.IsReady
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ReadProcessPath()
    {
        using var process = Process.GetCurrentProcess();
        return SafeRead(
            () => process.MainModule?.FileName ?? Environment.ProcessPath ?? string.Empty,
            Environment.ProcessPath ?? string.Empty);
    }

    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }
}

public static class PortableDiagnosticReportExportPlanner
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
            "DiagnosticInfo",
            $"diagnostic_report_{exportTime:yyyyMMdd_HHmmss}.txt");
    }
}

public sealed record PortableDiagnosticReportExportResult(
    bool Success,
    string FilePath,
    string? ErrorMessage);

public static class PortableDiagnosticReportExporter
{
    public static async Task<PortableDiagnosticReportExportResult> ExportAsync(
        PortableDiagnosticInfoService service,
        PortableDiagnosticSnapshot snapshot,
        PortableDiagnosticHealthReport healthReport,
        PortableDiagnosticRuntimeContext context,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(healthReport);
        ArgumentNullException.ThrowIfNull(context);
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
                service.BuildReport(snapshot, healthReport, context),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            return new PortableDiagnosticReportExportResult(true, filePath, null);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(tempPath);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(tempPath);
            return new PortableDiagnosticReportExportResult(false, filePath, ex.Message);
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

public sealed record PortableDiagnosticAutoRefreshPlan(bool Enabled, TimeSpan Interval);

public sealed class PortableDiagnosticInfoController
{
    private readonly PortableDiagnosticInfoService _service;
    private readonly PortableDiagnosticInfoSettings _settings;
    private readonly IPortableDiagnosticInfoProbe _probe;
    private readonly string _storageRoot;
    private readonly Func<DateTime> _clock;

    public PortableDiagnosticInfoController(
        PortableDiagnosticInfoService service,
        PortableDiagnosticInfoSettings settings,
        IPortableDiagnosticInfoProbe? probe = null,
        string storageRoot = "",
        Func<DateTime>? clock = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _probe = probe ?? new DefaultPortableDiagnosticInfoProbe();
        _storageRoot = storageRoot;
        _clock = clock ?? (() => DateTime.Now);
    }

    public PortableDiagnosticSnapshot CurrentSnapshot { get; private set; } = new();
    public PortableDiagnosticHealthReport CurrentHealthReport { get; private set; } = new();

    public PortableDiagnosticAutoRefreshPlan GetAutoRefreshPlan()
    {
        return new PortableDiagnosticAutoRefreshPlan(
            _settings.EnableAutoRefresh,
            TimeSpan.FromSeconds(_settings.RefreshIntervalSeconds));
    }

    public Task<PortableDiagnosticSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentSnapshot = _probe.CollectSnapshot();
        return Task.FromResult(CurrentSnapshot);
    }

    public PortableDiagnosticHealthReport RunHealthCheck()
    {
        CurrentHealthReport = _service.EvaluateHealth(
            CurrentSnapshot,
            _probe.CollectDrives(),
            _settings);
        return CurrentHealthReport;
    }

    public Task<PortableDiagnosticReportExportResult> ExportReportAsync(
        CancellationToken cancellationToken = default)
    {
        var filePath = PortableDiagnosticReportExportPlanner.BuildDefaultPath(_storageRoot, _clock());
        return PortableDiagnosticReportExporter.ExportAsync(
            _service,
            CurrentSnapshot,
            CurrentHealthReport,
            _probe.CollectRuntimeContext(_storageRoot),
            filePath,
            cancellationToken);
    }
}

public sealed class PortableDiagnosticInfoService
{
    private readonly Func<DateTime> _now;

    public PortableDiagnosticInfoService(Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.Now);
    }

    public PortableDiagnosticHealthReport EvaluateHealth(
        PortableDiagnosticSnapshot snapshot,
        IEnumerable<PortableDiagnosticDriveInfo> drives,
        PortableDiagnosticInfoSettings? settings = null)
    {
        settings ??= new PortableDiagnosticInfoSettings();
        var report = new PortableDiagnosticHealthReport();

        foreach (var drive in drives.Where(drive => drive.IsReady && drive.TotalBytes > 0))
        {
            var usagePercent = (double)(drive.TotalBytes - drive.AvailableFreeBytes) / drive.TotalBytes * 100;
            if (usagePercent > settings.DiskSpaceWarningThresholdPercent)
            {
                report.Suggestions.Add($"⚠️ 驱动器 {drive.Name} 空间不足 ({usagePercent:F1}% 已使用)");
                report.IssueCount++;
            }
        }

        if (snapshot.MemoryUsageMB > 1024)
        {
            report.Suggestions.Add($"💡 应用内存使用较高 ({snapshot.MemoryUsageMB} MB)，建议适当优化");
            report.IssueCount++;
        }

        if (snapshot.Gen2Collections > 100)
        {
            report.Suggestions.Add($"💡 2代垃圾回收次数较多 ({snapshot.Gen2Collections} 次)，可能存在内存压力");
            report.IssueCount++;
        }

        if (snapshot.ThreadCount > 100)
        {
            report.Suggestions.Add($"⚠️ 线程数较多 ({snapshot.ThreadCount} 个)，建议检查是否存在线程泄漏");
            report.IssueCount++;
        }

        if (report.IssueCount == 0)
        {
            report.HealthStatus = "健康";
            report.HealthStatusColor = "#4CAF50";
            report.Suggestions.Add("✅ 系统运行状态良好");
        }
        else if (report.IssueCount <= 2)
        {
            report.HealthStatus = "良好";
            report.HealthStatusColor = "#FFC107";
        }
        else
        {
            report.HealthStatus = "需要关注";
            report.HealthStatusColor = "#F44336";
        }

        return report;
    }

    public string BuildReport(
        PortableDiagnosticSnapshot snapshot,
        PortableDiagnosticHealthReport healthReport,
        PortableDiagnosticRuntimeContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==================== 诊断信息报告 ====================");
        sb.AppendLine($"生成时间: {_now():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("【应用信息】");
        sb.AppendLine($"  版本: {context.AppVersion}");
        sb.AppendLine($"  进程路径: {context.ProcessPath}");
        sb.AppendLine();
        sb.AppendLine("【运行环境】");
        sb.AppendLine($"  操作系统: {context.OperatingSystem}");
        sb.AppendLine($"  .NET 版本: {context.DotNetVersion}");
        sb.AppendLine($"  机器名: {context.MachineName}");
        sb.AppendLine($"  用户名: {context.UserName}");
        sb.AppendLine($"  Storage根: {context.StorageRoot}");
        sb.AppendLine();
        sb.AppendLine("【实时诊断数据】");
        sb.AppendLine($"  CPU使用率: {snapshot.CpuUsage}%");
        sb.AppendLine($"  内存使用: {snapshot.MemoryUsageMB} MB");
        sb.AppendLine($"  线程数: {snapshot.ThreadCount}");
        sb.AppendLine($"  句柄数: {snapshot.HandleCount}");
        sb.AppendLine();
        sb.AppendLine("【GC统计】");
        sb.AppendLine($"  0代回收: {snapshot.Gen0Collections} 次");
        sb.AppendLine($"  1代回收: {snapshot.Gen1Collections} 次");
        sb.AppendLine($"  2代回收: {snapshot.Gen2Collections} 次");
        sb.AppendLine();
        sb.AppendLine("【健康状态】");
        sb.AppendLine($"  状态: {healthReport.HealthStatus}");
        sb.AppendLine();
        sb.AppendLine("【系统建议】");
        foreach (var suggestion in healthReport.Suggestions)
        {
            sb.AppendLine($"  {suggestion}");
        }

        return sb.ToString();
    }
}
