using System.Text.Json;
using TM.Services.Modules.ProjectData.ProjectSystem;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileProjectManagerTests
{
    [Fact]
    public async Task LoadAsync_creates_default_project_and_required_directories()
    {
        using var workspace = new TempDirectory();
        var manager = new FileProjectManager(workspace.Path);

        await manager.LoadAsync();

        var project = Assert.Single(manager.Projects);
        Assert.Equal("default", project.Id);
        Assert.Equal("默认项目", project.Name);
        Assert.Equal(project.Id, manager.CurrentProject?.Id);
        Assert.True(Directory.Exists(System.IO.Path.Combine(project.Path, "Config", "Design")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(project.Path, "Config", "Generate")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(project.Path, "Generated", "chapters")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(project.Path, "Validation", "reports")));
        Assert.True(Directory.Exists(System.IO.Path.Combine(project.Path, "History")));
    }

    [Fact]
    public async Task CreateProjectAsync_preserves_single_book_constraint()
    {
        using var workspace = new TempDirectory();
        var manager = new FileProjectManager(workspace.Path);
        await manager.LoadAsync();

        var project = await manager.CreateProjectAsync("新书");

        Assert.Equal("default", project.Id);
        Assert.Single(manager.Projects);
    }

    [Fact]
    public async Task RenameProjectAsync_moves_directory_and_persists_config()
    {
        using var workspace = new TempDirectory();
        var manager = new FileProjectManager(workspace.Path);
        await manager.LoadAsync();
        var project = manager.CurrentProject!;
        var oldPath = project.Path;

        var renamed = await manager.RenameProjectAsync(project.Id, "新:书?");

        var reloaded = new FileProjectManager(workspace.Path);
        await reloaded.LoadAsync();

        Assert.True(renamed);
        Assert.False(Directory.Exists(oldPath));
        Assert.Equal("新:书?", reloaded.CurrentProject?.Name);
        Assert.EndsWith("新_书_", reloaded.CurrentProject?.Path);
        Assert.True(Directory.Exists(reloaded.CurrentProject!.Path));
    }

    [Fact]
    public async Task SwitchProjectAsync_persists_current_project()
    {
        using var workspace = new TempDirectory();
        var configDir = System.IO.Path.Combine(workspace.Path, "Config");
        Directory.CreateDirectory(configDir);
        var firstPath = System.IO.Path.Combine(workspace.Path, "Projects", "A");
        var secondPath = System.IO.Path.Combine(workspace.Path, "Projects", "B");
        Directory.CreateDirectory(firstPath);
        Directory.CreateDirectory(secondPath);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(configDir, "projects.json"),
            JsonSerializer.Serialize(new ProjectConfig
            {
                CurrentProject = "A",
                Projects =
                {
                    new ProjectInfo { Id = "A", Name = "A", Path = firstPath },
                    new ProjectInfo { Id = "B", Name = "B", Path = secondPath }
                }
            }));
        var manager = new FileProjectManager(workspace.Path);
        await manager.LoadAsync();

        var switched = await manager.SwitchProjectAsync("B");

        var reloaded = new FileProjectManager(workspace.Path);
        await reloaded.LoadAsync();
        Assert.True(switched);
        Assert.Equal("B", reloaded.CurrentProject?.Id);
    }

    [Fact]
    public async Task DeleteProjectAsync_removes_project_directory_and_selects_next_project()
    {
        using var workspace = new TempDirectory();
        var configDir = System.IO.Path.Combine(workspace.Path, "Config");
        Directory.CreateDirectory(configDir);
        var firstPath = System.IO.Path.Combine(workspace.Path, "Projects", "A");
        var secondPath = System.IO.Path.Combine(workspace.Path, "Projects", "B");
        Directory.CreateDirectory(firstPath);
        Directory.CreateDirectory(secondPath);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(configDir, "projects.json"),
            JsonSerializer.Serialize(new ProjectConfig
            {
                CurrentProject = "A",
                Projects =
                {
                    new ProjectInfo { Id = "A", Name = "A", Path = firstPath },
                    new ProjectInfo { Id = "B", Name = "B", Path = secondPath }
                }
            }));
        var manager = new FileProjectManager(workspace.Path);
        await manager.LoadAsync();

        var deleted = await manager.DeleteProjectAsync("A");

        Assert.True(deleted);
        Assert.False(Directory.Exists(firstPath));
        Assert.Equal("B", manager.CurrentProject?.Id);
        Assert.Single(manager.Projects);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-project-manager-{Guid.NewGuid():N}");

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
