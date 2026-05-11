using System.Text.Json;
using TM.Services.Modules.VersionTracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileVersionTrackingServiceTests
{
    [Fact]
    public void IncrementModuleVersion_persists_state_and_reloads_versions()
    {
        using var workspace = new TempDirectory();
        var service = new FileVersionTrackingService(workspace.Path);

        Assert.Equal(0, service.GetModuleVersion("WorldRules"));

        Assert.Equal(1, service.IncrementModuleVersion("WorldRules", showDownstreamToast: false));
        Assert.Equal(2, service.IncrementModuleVersion("WorldRules", showDownstreamToast: false));

        var reloaded = new FileVersionTrackingService(workspace.Path);
        using var document = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(workspace.Path, "version_registry.json")));

        Assert.Equal(2, reloaded.GetModuleVersion("WorldRules"));
        Assert.Equal(2, document.RootElement.GetProperty("ModuleVersions").GetProperty("WorldRules").GetInt32());
    }

    [Fact]
    public void GetDependencySnapshot_returns_current_versions_for_configured_dependencies()
    {
        using var workspace = new TempDirectory();
        var service = new FileVersionTrackingService(workspace.Path);
        service.IncrementModuleVersion("CreativeMaterials", showDownstreamToast: false);
        service.IncrementModuleVersion("WorldRules", showDownstreamToast: false);
        service.IncrementModuleVersion("WorldRules", showDownstreamToast: false);
        service.IncrementModuleVersion("CharacterRules", showDownstreamToast: false);

        var snapshot = service.GetDependencySnapshot("FactionRules");

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(1, snapshot["CreativeMaterials"]);
        Assert.Equal(2, snapshot["WorldRules"]);
        Assert.Equal(1, snapshot["CharacterRules"]);
    }

    [Fact]
    public void CheckOutdatedDependencies_reports_only_modules_newer_than_saved_snapshot()
    {
        using var workspace = new TempDirectory();
        var service = new FileVersionTrackingService(workspace.Path);
        service.IncrementModuleVersion("WorldRules", showDownstreamToast: false);
        service.IncrementModuleVersion("WorldRules", showDownstreamToast: false);
        service.IncrementModuleVersion("CharacterRules", showDownstreamToast: false);

        var outdated = service.CheckOutdatedDependencies(new Dictionary<string, int>
        {
            ["WorldRules"] = 1,
            ["CharacterRules"] = 1,
            ["LocationRules"] = 0
        });

        Assert.Equal(["WorldRules"], outdated);
    }

    [Fact]
    public void FlushPendingDownstreamNotifications_replays_suppressed_downstream_impacts()
    {
        using var workspace = new TempDirectory();
        var notifications = new List<(string ModuleName, IReadOnlyList<string> Downstream)>();
        var service = new FileVersionTrackingService(
            workspace.Path,
            (moduleName, downstream) => notifications.Add((moduleName, downstream)));

        service.SuppressDownstreamToast = true;
        service.IncrementModuleVersion("WorldRules");
        service.IncrementModuleVersion("CreativeMaterials");

        Assert.Empty(notifications);

        service.SuppressDownstreamToast = false;
        service.FlushPendingDownstreamNotifications();

        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, item =>
            item.ModuleName == "WorldRules" && item.Downstream.Contains("CharacterRules"));
        Assert.Contains(notifications, item =>
            item.ModuleName == "CreativeMaterials" && item.Downstream.Contains("BookAnalysis") is false);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-version-tracking-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
