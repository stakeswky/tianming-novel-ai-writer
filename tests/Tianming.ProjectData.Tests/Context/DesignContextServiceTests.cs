using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class DesignContextServiceTests
{
    [Fact]
    public async Task ListByCategory_returns_empty_for_unknown_category()
    {
        using var workspace = new TempDirectory();
        var svc = new DesignContextService(workspace.Path);

        var list = await svc.ListByCategoryAsync("UnknownCategory");

        Assert.Empty(list);
    }

    [Fact]
    public async Task ListByCategory_reads_array_backed_module_store_data()
    {
        using var workspace = new TempDirectory();
        await WriteModuleDataAsync(
            workspace.Path,
            Path.Combine("Design", "Elements", "Characters"),
            new object[]
            {
                new
                {
                    Id = "char-001",
                    Name = "沈砚",
                    Category = "角色",
                    Description = "剑客",
                }
            });

        var svc = new DesignContextService(workspace.Path);

        var results = await svc.ListByCategoryAsync("Characters");

        var character = Assert.Single(results);
        Assert.Equal("char-001", character.Id);
        Assert.Equal("沈砚", character.Name);
        Assert.Equal("Characters", character.Category);
        Assert.Contains("\"Id\":\"char-001\"", character.RawJson);
    }

    [Fact]
    public async Task Search_finds_match_in_real_module_store_layout()
    {
        using var workspace = new TempDirectory();
        await WriteModuleDataAsync(
            workspace.Path,
            Path.Combine("Design", "Elements", "Characters"),
            new object[]
            {
                new
                {
                    Id = "char-001",
                    Name = "沈砚",
                    Category = "角色",
                    Description = "剑客",
                }
            });

        var svc = new DesignContextService(workspace.Path);

        var results = await svc.SearchAsync("沈砚");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "沈砚");
    }

    [Fact]
    public async Task GetById_returns_matching_reference()
    {
        using var workspace = new TempDirectory();
        await WriteModuleDataAsync(
            workspace.Path,
            Path.Combine("Design", "Elements", "Plot"),
            new object[]
            {
                new
                {
                    Id = "plot-001",
                    Name = "命火试炼",
                    Category = "剧情",
                    OneLineSummary = "试炼主线",
                }
            });

        var svc = new DesignContextService(workspace.Path);

        var reference = await svc.GetByIdAsync("plot-001");

        Assert.NotNull(reference);
        Assert.Equal("命火试炼", reference!.Name);
        Assert.Equal("Plot", reference.Category);
    }

    private static async Task WriteModuleDataAsync(string projectRoot, string moduleRelativePath, object[] data)
    {
        var moduleDir = Path.Combine(projectRoot, moduleRelativePath);
        Directory.CreateDirectory(moduleDir);
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "categories.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "built_in_categories.json"), "[]");
        await File.WriteAllTextAsync(Path.Combine(moduleDir, "data.json"), JsonSerializer.Serialize(data));
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");

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
