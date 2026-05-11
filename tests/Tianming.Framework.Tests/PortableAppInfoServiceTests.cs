using System.Net;
using System.Security.Cryptography;
using System.Text;
using TM.Framework.AppInfo;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAppInfoServiceTests
{
    [Fact]
    public void Default_settings_match_original_app_info_defaults()
    {
        var settings = PortableAppInfoSettings.CreateDefault();

        Assert.Equal("https://update.example.com", settings.UpdateServerUrl);
        Assert.True(settings.AutoCheckUpdate);
        Assert.Equal(7, settings.UpdateCheckIntervalDays);
        Assert.Equal(DateTime.MinValue, settings.LastUpdateCheckTime);
        Assert.Equal("1.0.0.0", settings.CurrentVersion);
    }

    [Fact]
    public void ShouldCheckForUpdate_honors_auto_update_interval()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0);
        var settings = PortableAppInfoSettings.CreateDefault();

        Assert.True(PortableAppUpdatePolicy.ShouldCheckForUpdate(settings, now));

        settings.LastUpdateCheckTime = now.AddDays(-6).AddMinutes(-59);
        Assert.False(PortableAppUpdatePolicy.ShouldCheckForUpdate(settings, now));

        settings.LastUpdateCheckTime = now.AddDays(-7);
        Assert.True(PortableAppUpdatePolicy.ShouldCheckForUpdate(settings, now));

        settings.AutoCheckUpdate = false;
        Assert.False(PortableAppUpdatePolicy.ShouldCheckForUpdate(settings, now));
    }

    [Fact]
    public void MarkChecked_updates_timestamp_without_mutating_source()
    {
        var checkedAt = new DateTime(2026, 5, 10, 13, 14, 15);
        var settings = PortableAppInfoSettings.CreateDefault();

        var updated = PortableAppUpdatePolicy.MarkChecked(settings, checkedAt);

        Assert.Equal(DateTime.MinValue, settings.LastUpdateCheckTime);
        Assert.Equal(checkedAt, updated.LastUpdateCheckTime);
        Assert.Equal(settings.UpdateServerUrl, updated.UpdateServerUrl);
        Assert.Equal(settings.CurrentVersion, updated.CurrentVersion);
    }

    [Fact]
    public void UpdateProtocol_parses_server_manifest_and_detects_force_update()
    {
        var json = """
        {
          "latestVersion": "1.4.7",
          "downloadUrl": "https://updates.example.test/Tianming-1.4.7.dmg",
          "releaseNotes": "修复安全问题",
          "forceUpdate": true,
          "sha256": "abc123"
        }
        """;

        var manifest = PortableAppUpdateProtocol.ParseManifest(json);
        var decision = PortableAppUpdatePolicy.EvaluateUpdate(
            currentVersion: "1.4.6",
            manifest);

        Assert.Equal("1.4.7", manifest.LatestVersion);
        Assert.Equal("https://updates.example.test/Tianming-1.4.7.dmg", manifest.DownloadUrl);
        Assert.Equal("修复安全问题", manifest.ReleaseNotes);
        Assert.Equal("abc123", manifest.Sha256);
        Assert.True(decision.UpdateAvailable);
        Assert.True(decision.ForceUpdate);
        Assert.Equal("1.4.7", decision.TargetVersion);
    }

    [Fact]
    public void MacOSInstallerPlanner_creates_download_and_open_plan()
    {
        var manifest = new PortableAppUpdateManifest
        {
            LatestVersion = "1.4.7",
            DownloadUrl = "https://updates.example.test/downloads/Tianming-1.4.7.dmg",
            Sha256 = "abc123"
        };

        var plan = PortableMacOSAppInstallerPlanner.CreatePlan(
            manifest,
            downloadsDirectory: "/Users/jimmy/Downloads");

        Assert.Equal("https://updates.example.test/downloads/Tianming-1.4.7.dmg", plan.DownloadUrl);
        Assert.Equal("/Users/jimmy/Downloads/Tianming-1.4.7.dmg", plan.TargetFilePath);
        Assert.Equal("abc123", plan.ExpectedSha256);
        Assert.Equal("/usr/bin/open", plan.OpenExecutablePath);
        Assert.Equal(["/Users/jimmy/Downloads/Tianming-1.4.7.dmg"], plan.OpenArguments);
    }

    [Fact]
    public async Task UpdateDownloader_writes_verified_download_atomically()
    {
        using var workspace = new TempDirectory();
        var bytes = Encoding.UTF8.GetBytes("portable dmg bytes");
        using var httpClient = new HttpClient(new StaticDownloadHandler(bytes));
        var downloader = new PortableAppUpdateDownloader(httpClient);
        var targetPath = Path.Combine(workspace.Path, "nested", "Tianming-1.4.7.dmg");
        var plan = new PortableMacOSAppInstallerPlan(
            "https://updates.example.test/Tianming-1.4.7.dmg",
            targetPath,
            HexSha256(bytes),
            "/usr/bin/open",
            [targetPath]);

        var result = await downloader.DownloadAsync(plan);

        Assert.True(result.Success);
        Assert.Equal(bytes.Length, result.BytesWritten);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task UpdateDownloader_removes_download_when_sha256_does_not_match()
    {
        using var workspace = new TempDirectory();
        var bytes = Encoding.UTF8.GetBytes("tampered dmg bytes");
        using var httpClient = new HttpClient(new StaticDownloadHandler(bytes));
        var downloader = new PortableAppUpdateDownloader(httpClient);
        var targetPath = Path.Combine(workspace.Path, "Tianming-1.4.7.dmg");
        var plan = new PortableMacOSAppInstallerPlan(
            "https://updates.example.test/Tianming-1.4.7.dmg",
            targetPath,
            new string('0', 64),
            "/usr/bin/open",
            [targetPath]);

        var result = await downloader.DownloadAsync(plan);

        Assert.False(result.Success);
        Assert.Equal("SHA256_MISMATCH", result.ErrorCode);
        Assert.False(File.Exists(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileAppInfoSettingsStore(path);
        var settings = PortableAppInfoSettings.CreateDefault();
        settings.UpdateServerUrl = "https://updates.example.test/mac";
        settings.AutoCheckUpdate = false;
        settings.UpdateCheckIntervalDays = 3;
        settings.LastUpdateCheckTime = new DateTime(2026, 5, 10, 1, 2, 3);
        settings.CurrentVersion = "1.4.6";

        await store.SaveAsync(settings);
        var reloaded = await new FileAppInfoSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("https://updates.example.test/mac", reloaded.UpdateServerUrl);
        Assert.False(reloaded.AutoCheckUpdate);
        Assert.Equal(3, reloaded.UpdateCheckIntervalDays);
        Assert.Equal(new DateTime(2026, 5, 10, 1, 2, 3), reloaded.LastUpdateCheckTime);
        Assert.Equal("1.4.6", reloaded.CurrentVersion);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");

        Assert.Equal("1.0.0.0", (await new FileAppInfoSettingsStore(path).LoadAsync()).CurrentVersion);

        await File.WriteAllTextAsync(path, "{ invalid json");

        Assert.Equal("https://update.example.com", (await new FileAppInfoSettingsStore(path).LoadAsync()).UpdateServerUrl);
    }

    [Fact]
    public void Static_catalogs_match_original_view_model_data()
    {
        var featureModules = PortableAppInfoCatalog.GetDefaultFeatureModules();
        var licenses = PortableAppInfoCatalog.GetDefaultLicenses();
        var versionHistory = PortableAppInfoCatalog.GetDefaultVersionHistory();

        Assert.Equal(6, featureModules.Count);
        Assert.Contains(featureModules, module => module.CategoryName == "通知设置" && module.SubfunctionCount == 5);
        Assert.Contains(licenses, license => license.Name == ".NET Runtime" && license.LicenseType == "MIT License");
        Assert.Equal(10, versionHistory.Count);
        Assert.Equal("1.4.6", versionHistory[0].Version);
        Assert.True(versionHistory[0].IsCurrent);
        Assert.Equal("1.3.7", versionHistory[^1].Version);
    }

    [Fact]
    public void BuildReport_matches_original_core_app_info_export_shape()
    {
        var snapshot = new PortableAppInfoSnapshot
        {
            AppName = "天命",
            AppVersion = "1.4.6",
            TargetFramework = ".NETCoreApp,Version=v8.0",
            InstallPath = "/Applications/Tianming.app",
            ProcessId = 42,
            StartupTime = new DateTime(2026, 5, 10, 8, 0, 0),
            RunningTime = TimeSpan.FromMinutes(65),
            Assemblies =
            [
                new PortableAssemblyItem { Name = "Tianming.Framework", Version = "1.0.0.0", Location = "/tmp/Tianming.Framework.dll" }
            ]
        };

        var report = PortableAppInfoReportBuilder.BuildReport(snapshot);

        Assert.Contains("==================== 应用信息报告 ====================", report);
        Assert.Contains("应用名称: 天命", report);
        Assert.Contains("版本: 1.4.6", report);
        Assert.Contains("目标框架: .NETCoreApp,Version=v8.0", report);
        Assert.Contains("进程ID: 42", report);
        Assert.Contains("运行时长: 1小时 5分钟 0秒", report);
        Assert.Contains("【已加载程序集】", report);
        Assert.Contains("Tianming.Framework - 1.0.0.0", report);
    }

    [Fact]
    public void ReportExportPlanner_builds_original_default_export_path()
    {
        var exportTime = new DateTime(2026, 5, 11, 9, 8, 7);

        var path = PortableAppInfoReportExportPlanner.BuildDefaultPath(
            "/Users/jimmy/Library/Application Support/Tianming",
            exportTime);

        Assert.Equal(
            "/Users/jimmy/Library/Application Support/Tianming/Framework/SystemSettings/Info/AppInfo/app_info_export_20260511_090807.txt",
            path);
    }

    [Fact]
    public async Task ReportExporter_writes_report_atomically_and_creates_directory()
    {
        using var workspace = new TempDirectory();
        var snapshot = new PortableAppInfoSnapshot
        {
            AppName = "天命",
            AppVersion = "1.4.7",
            TargetFramework = ".NETCoreApp,Version=v8.0",
            InstallPath = "/Applications/Tianming.app",
            ProcessId = 88,
            StartupTime = new DateTime(2026, 5, 11, 8, 0, 0),
            RunningTime = TimeSpan.FromSeconds(9)
        };
        var targetPath = Path.Combine(workspace.Path, "exports", "app_info_export.txt");

        var result = await PortableAppInfoReportExporter.ExportAsync(snapshot, targetPath);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.Contains("应用名称: 天命", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
    }

    private static string HexSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class StaticDownloadHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        }
    }
}
