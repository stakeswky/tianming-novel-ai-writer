using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.AppInfo;

public sealed class PortableAppInfoSettings
{
    [JsonPropertyName("UpdateServerUrl")] public string UpdateServerUrl { get; set; } = "https://update.example.com";

    [JsonPropertyName("AutoCheckUpdate")] public bool AutoCheckUpdate { get; set; } = true;

    [JsonPropertyName("UpdateCheckIntervalDays")] public int UpdateCheckIntervalDays { get; set; } = 7;

    [JsonPropertyName("LastUpdateCheckTime")] public DateTime LastUpdateCheckTime { get; set; } = DateTime.MinValue;

    [JsonPropertyName("CurrentVersion")] public string CurrentVersion { get; set; } = "1.0.0.0";

    public static PortableAppInfoSettings CreateDefault()
    {
        return new PortableAppInfoSettings
        {
            UpdateServerUrl = "https://update.example.com",
            AutoCheckUpdate = true,
            UpdateCheckIntervalDays = 7,
            LastUpdateCheckTime = DateTime.MinValue,
            CurrentVersion = "1.0.0.0"
        };
    }

    public PortableAppInfoSettings Clone()
    {
        return new PortableAppInfoSettings
        {
            UpdateServerUrl = UpdateServerUrl,
            AutoCheckUpdate = AutoCheckUpdate,
            UpdateCheckIntervalDays = UpdateCheckIntervalDays,
            LastUpdateCheckTime = LastUpdateCheckTime,
            CurrentVersion = CurrentVersion
        };
    }
}

public sealed class PortableAppUpdateManifest
{
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")] public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("forceUpdate")] public bool ForceUpdate { get; set; }

    [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
}

public sealed record PortableAppUpdateDecision(
    bool UpdateAvailable,
    bool ForceUpdate,
    string TargetVersion,
    string DownloadUrl,
    string ReleaseNotes);

public static class PortableAppUpdateProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PortableAppUpdateManifest ParseManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Update manifest JSON is required.", nameof(json));
        }

        return JsonSerializer.Deserialize<PortableAppUpdateManifest>(json, JsonOptions)
            ?? throw new JsonException("Update manifest JSON did not contain a manifest object.");
    }
}

public static class PortableAppUpdatePolicy
{
    public static bool ShouldCheckForUpdate(PortableAppInfoSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.AutoCheckUpdate)
        {
            return false;
        }

        if (settings.LastUpdateCheckTime == DateTime.MinValue)
        {
            return true;
        }

        var intervalDays = Math.Max(0, settings.UpdateCheckIntervalDays);
        return now - settings.LastUpdateCheckTime >= TimeSpan.FromDays(intervalDays);
    }

    public static PortableAppInfoSettings MarkChecked(PortableAppInfoSettings settings, DateTime checkedAt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var updated = settings.Clone();
        updated.LastUpdateCheckTime = checkedAt;
        return updated;
    }

    public static PortableAppUpdateDecision EvaluateUpdate(
        string currentVersion,
        PortableAppUpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var updateAvailable = IsNewerVersion(manifest.LatestVersion, currentVersion);
        return new PortableAppUpdateDecision(
            updateAvailable,
            updateAvailable && manifest.ForceUpdate,
            manifest.LatestVersion,
            manifest.DownloadUrl,
            manifest.ReleaseNotes);
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(latestVersion, out var latest)
            && Version.TryParse(currentVersion, out var current))
        {
            return latest > current;
        }

        return string.CompareOrdinal(latestVersion, currentVersion) > 0;
    }
}

public sealed record PortableMacOSAppInstallerPlan(
    string DownloadUrl,
    string TargetFilePath,
    string? ExpectedSha256,
    string OpenExecutablePath,
    IReadOnlyList<string> OpenArguments);

public static class PortableMacOSAppInstallerPlanner
{
    public static PortableMacOSAppInstallerPlan CreatePlan(
        PortableAppUpdateManifest manifest,
        string downloadsDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            throw new ArgumentException("Update download URL is required.", nameof(manifest));
        }

        if (string.IsNullOrWhiteSpace(downloadsDirectory))
        {
            throw new ArgumentException("Downloads directory is required.", nameof(downloadsDirectory));
        }

        var fileName = GetDownloadFileName(manifest);
        var targetFilePath = Path.Combine(downloadsDirectory, fileName);
        return new PortableMacOSAppInstallerPlan(
            manifest.DownloadUrl,
            targetFilePath,
            manifest.Sha256,
            "/usr/bin/open",
            [targetFilePath]);
    }

    private static string GetDownloadFileName(PortableAppUpdateManifest manifest)
    {
        if (Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        var versionSuffix = string.IsNullOrWhiteSpace(manifest.LatestVersion)
            ? "latest"
            : manifest.LatestVersion;
        return $"Tianming-{versionSuffix}.dmg";
    }
}

public sealed record PortableAppUpdateDownloadResult(
    bool Success,
    string? ErrorCode,
    long BytesWritten,
    string TargetFilePath);

public sealed class PortableAppUpdateDownloader
{
    private readonly HttpClient _httpClient;

    public PortableAppUpdateDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<PortableAppUpdateDownloadResult> DownloadAsync(
        PortableMacOSAppInstallerPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var directory = Path.GetDirectoryName(plan.TargetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = plan.TargetFilePath + ".tmp";
        try
        {
            using var response = await _httpClient.GetAsync(plan.DownloadUrl, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                DeleteIfExists(tempPath);
                return new PortableAppUpdateDownloadResult(
                    false,
                    "HTTP_ERROR",
                    0,
                    plan.TargetFilePath);
            }

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                             .ConfigureAwait(false))
            await using (var destination = File.Create(tempPath))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            var bytesWritten = new FileInfo(tempPath).Length;
            if (!HashMatches(tempPath, plan.ExpectedSha256))
            {
                DeleteIfExists(tempPath);
                return new PortableAppUpdateDownloadResult(
                    false,
                    "SHA256_MISMATCH",
                    bytesWritten,
                    plan.TargetFilePath);
            }

            File.Move(tempPath, plan.TargetFilePath, overwrite: true);
            return new PortableAppUpdateDownloadResult(
                true,
                null,
                bytesWritten,
                plan.TargetFilePath);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(tempPath);
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(tempPath);
            return new PortableAppUpdateDownloadResult(
                false,
                "DOWNLOAD_FAILED",
                0,
                plan.TargetFilePath);
        }
    }

    private static bool HashMatches(string filePath, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return true;
        }

        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

public sealed class FileAppInfoSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileAppInfoSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("App info settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableAppInfoSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableAppInfoSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableAppInfoSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableAppInfoSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableAppInfoSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableAppInfoSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(PortableAppInfoSettings settings, CancellationToken cancellationToken = default)
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

public sealed class PortableAppInfoSnapshot
{
    public string AppName { get; init; } = "天命";
    public string AppVersion { get; init; } = string.Empty;
    public string TargetFramework { get; init; } = string.Empty;
    public string InstallPath { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public DateTime StartupTime { get; init; }
    public TimeSpan RunningTime { get; init; }
    public string Developer { get; init; } = "子夜";
    public string DeveloperDescription { get; init; } = "由独立开发者 子夜 倾力打造，从架构设计到功能实现，每一行代码皆为匠心之作。";
    public List<PortableAssemblyItem> Assemblies { get; init; } = [];
}

public sealed class PortableAssemblyItem
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}

public sealed class PortableFeatureModuleItem
{
    public string CategoryName { get; init; } = string.Empty;
    public int SubfunctionCount { get; init; }
    public string Icon { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class PortableLicenseItem
{
    public string Name { get; init; } = string.Empty;
    public string LicenseType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class PortableVersionHistoryItem
{
    public string Version { get; init; } = string.Empty;
    public DateTime ReleaseDate { get; init; }
    public string ChangeLog { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}

public static class PortableAppInfoCatalog
{
    public static IReadOnlyList<PortableFeatureModuleItem> GetDefaultFeatureModules()
    {
        return
        [
            new PortableFeatureModuleItem { CategoryName = "界面设置", SubfunctionCount = 5, Icon = "🎨", Status = "已启用" },
            new PortableFeatureModuleItem { CategoryName = "用户设置", SubfunctionCount = 4, Icon = "👤", Status = "已启用" },
            new PortableFeatureModuleItem { CategoryName = "通知设置", SubfunctionCount = 5, Icon = "🔔", Status = "已启用" },
            new PortableFeatureModuleItem { CategoryName = "安全设置", SubfunctionCount = 5, Icon = "🔒", Status = "已启用" },
            new PortableFeatureModuleItem { CategoryName = "网络设置", SubfunctionCount = 4, Icon = "🌐", Status = "已启用" },
            new PortableFeatureModuleItem { CategoryName = "系统设置", SubfunctionCount = 5, Icon = "⚙️", Status = "已启用" }
        ];
    }

    public static IReadOnlyList<PortableLicenseItem> GetDefaultLicenses()
    {
        return
        [
            new PortableLicenseItem
            {
                Name = "天命",
                LicenseType = "专有许可证",
                Description = "本应用遵循专有软件许可协议"
            },
            new PortableLicenseItem
            {
                Name = "Emoji" + ".Wpf",
                LicenseType = "MIT License",
                Description = "彩色Emoji显示库"
            },
            new PortableLicenseItem
            {
                Name = ".NET Runtime",
                LicenseType = "MIT License",
                Description = "微软.NET运行时环境"
            }
        ];
    }

    public static IReadOnlyList<PortableVersionHistoryItem> GetDefaultVersionHistory()
    {
        return
        [
            Version("1.4.6", 2026, 3, 5, true),
            Version("1.4.5", 2026, 3, 5, false),
            Version("1.4.4", 2026, 3, 5, false),
            Version("1.4.3", 2026, 3, 5, false),
            Version("1.4.2", 2026, 3, 4, false),
            Version("1.4.1", 2026, 3, 4, false),
            Version("1.4.0", 2026, 3, 4, false),
            Version("1.3.9", 2026, 3, 3, false),
            Version("1.3.8", 2026, 3, 3, false),
            Version("1.3.7", 2026, 3, 3, false)
        ];
    }

    private static PortableVersionHistoryItem Version(string version, int year, int month, int day, bool isCurrent)
    {
        return new PortableVersionHistoryItem
        {
            Version = version,
            ReleaseDate = new DateTime(year, month, day),
            ChangeLog = "版本更新",
            IsCurrent = isCurrent
        };
    }
}

public static class PortableAppInfoReportBuilder
{
    public static string BuildReport(PortableAppInfoSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sb = new StringBuilder();
        sb.AppendLine("==================== 应用信息报告 ====================");
        sb.AppendLine($"应用名称: {snapshot.AppName}");
        sb.AppendLine($"版本: {snapshot.AppVersion}");
        sb.AppendLine($"目标框架: {snapshot.TargetFramework}");
        sb.AppendLine($"安装路径: {snapshot.InstallPath}");
        sb.AppendLine($"进程ID: {snapshot.ProcessId}");
        sb.AppendLine($"启动时间: {snapshot.StartupTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"运行时长: {FormatRunningTime(snapshot.RunningTime)}");
        sb.AppendLine();
        sb.AppendLine("【已加载程序集】");
        foreach (var assembly in snapshot.Assemblies.OrderBy(assembly => assembly.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"  {assembly.Name} - {assembly.Version}");
        }

        return sb.ToString();
    }

    private static string FormatRunningTime(TimeSpan runningTime)
    {
        return $"{(int)runningTime.TotalHours}小时 {runningTime.Minutes}分钟 {runningTime.Seconds}秒";
    }
}

public static class PortableAppInfoReportExportPlanner
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
            "AppInfo",
            $"app_info_export_{exportTime:yyyyMMdd_HHmmss}.txt");
    }
}

public sealed record PortableAppInfoReportExportResult(
    bool Success,
    string FilePath,
    string? ErrorMessage);

public static class PortableAppInfoReportExporter
{
    public static async Task<PortableAppInfoReportExportResult> ExportAsync(
        PortableAppInfoSnapshot snapshot,
        string filePath,
        CancellationToken cancellationToken = default)
    {
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
                PortableAppInfoReportBuilder.BuildReport(snapshot),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            return new PortableAppInfoReportExportResult(true, filePath, null);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(tempPath);
            throw;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(tempPath);
            return new PortableAppInfoReportExportResult(false, filePath, ex.Message);
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
