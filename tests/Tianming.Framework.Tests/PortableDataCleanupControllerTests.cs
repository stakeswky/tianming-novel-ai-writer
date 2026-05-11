using TM.Framework.Cleanup;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableDataCleanupControllerTests
{
    [Fact]
    public void RefreshPlanner_maps_ai_and_project_session_paths_to_original_services_once()
    {
        var services = PortableCleanupServiceRefreshPlanner.GetServicesToRefresh([
            new PortableCleanupItem
            {
                Id = "models",
                RelativePath = "Storage/Services/AI/Library/providers.json"
            },
            new PortableCleanupItem
            {
                Id = "configs",
                RelativePath = "Storage\\Services\\AI\\Configurations\\user_configurations.json"
            },
            new PortableCleanupItem
            {
                Id = "sessions",
                RelativePath = "Storage/Projects/Sessions",
                IsDirectory = true
            }
        ]);

        Assert.Equal(["AIService", "SessionManager"], services);
    }

    [Fact]
    public async Task Controller_cleans_items_and_refreshes_services_for_selected_paths()
    {
        using var workspace = new TempDirectory();
        var statisticsFile = Path.Combine(workspace.Path, "Storage", "Services", "AI", "Library", "providers.json");
        Directory.CreateDirectory(Path.GetDirectoryName(statisticsFile)!);
        File.WriteAllText(statisticsFile, "[{\"Name\":\"Provider\"}]");
        var refresher = new RecordingCleanupServiceRefresher();
        var controller = new PortableDataCleanupController(
            new PortableDataCleanupService(workspace.Path),
            refresher);

        var execution = await controller.CleanupAsync([
            new PortableCleanupItem
            {
                Id = "providers",
                RelativePath = "Storage/Services/AI/Library/providers.json",
                Method = PortableCleanupMethod.ClearContent
            }
        ]);

        Assert.Equal("[]", File.ReadAllText(statisticsFile));
        Assert.Equal(1, execution.CleanupResult.SucceededItems);
        Assert.Equal(["AIService"], execution.RefreshedServices);
        Assert.Equal(["AIService"], refresher.RefreshedServices);
    }

    private sealed class RecordingCleanupServiceRefresher : IPortableCleanupServiceRefresher
    {
        public List<string> RefreshedServices { get; } = [];

        public Task RefreshAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            RefreshedServices.Add(serviceName);
            return Task.CompletedTask;
        }
    }
}
