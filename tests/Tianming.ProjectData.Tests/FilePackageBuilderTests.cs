using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FilePackageBuilderTests
{
    [Fact]
    public async Task PackageModuleAsync_writes_target_file_with_enabled_scoped_subdirectory_data()
    {
        using var workspace = new TempDirectory();
        var builder = new FilePackageBuilder(workspace.Path, System.IO.Path.Combine(workspace.Path, "Published"));
        var sourceDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(sourceDir, "Characters.json"),
            """
            [
              { "Id": "C1", "Name": "林衡", "IsEnabled": true, "SourceBookId": "BOOK-A" },
              { "Id": "C2", "Name": "禁用角色", "IsEnabled": false, "SourceBookId": "BOOK-A" },
              { "Id": "C3", "Name": "其他书", "IsEnabled": true, "SourceBookId": "BOOK-B" }
            ]
            """);

        await builder.PackageModuleAsync(
            new PackageModuleMapping("Design", "Elements", ["CharacterRules"], "elements.json"),
            sourceBookId: "BOOK-A");

        var packagePath = System.IO.Path.Combine(workspace.Path, "Published", "Design", "elements.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(packagePath));
        var data = doc.RootElement.GetProperty("data").GetProperty("characterrules").GetProperty("characters");

        Assert.Equal("Elements", doc.RootElement.GetProperty("module").GetString());
        var item = Assert.Single(data.EnumerateArray());
        Assert.Equal("C1", item.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task PackageModulesAsync_returns_manifest_files_map()
    {
        using var workspace = new TempDirectory();
        var builder = new FilePackageBuilder(workspace.Path, System.IO.Path.Combine(workspace.Path, "Published"));
        var sourceDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "Planning", "Chapter");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(System.IO.Path.Combine(sourceDir, "Chapter.json"), """{ "Title": "山门" }""");

        var files = await builder.PackageModulesAsync(
            [
                new PackageModuleMapping("Generate", "Planning", ["Chapter"], "planning.json")
            ],
            sourceBookId: null);

        Assert.Equal(["planning.json"], files["Generate"]);
        Assert.True(File.Exists(System.IO.Path.Combine(workspace.Path, "Published", "Generate", "planning.json")));
    }

    [Fact]
    public async Task PackageDefaultModulesAsync_uses_default_catalog_and_enabled_module_filter()
    {
        using var workspace = new TempDirectory();
        var builder = new FilePackageBuilder(workspace.Path, System.IO.Path.Combine(workspace.Path, "Published"));
        var characterDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        var outlineDir = System.IO.Path.Combine(workspace.Path, "Modules", "Generate", "GlobalSettings", "Outline");
        Directory.CreateDirectory(characterDir);
        Directory.CreateDirectory(outlineDir);
        await File.WriteAllTextAsync(System.IO.Path.Combine(characterDir, "Characters.json"), """[]""");
        await File.WriteAllTextAsync(System.IO.Path.Combine(outlineDir, "Outline.json"), """{ "Title": "天命" }""");

        var files = await builder.PackageDefaultModulesAsync(
            sourceBookId: null,
            new Dictionary<string, bool>
            {
                ["Design/GlobalSettings"] = false,
                ["Design/Elements"] = false,
                ["Generate/GlobalSettings"] = true,
                ["Generate/Elements"] = false
            });

        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "Published", "Design", "elements.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(workspace.Path, "Published", "Generate", "globalsettings.json")));
        Assert.Equal(["globalsettings.json"], files["Generate"]);
        Assert.DoesNotContain("Design", files.Keys);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-package-builder-{Guid.NewGuid():N}");

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
