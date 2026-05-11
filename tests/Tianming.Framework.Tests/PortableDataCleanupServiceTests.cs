using System.Text.Json;
using TM.Framework.Cleanup;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableDataCleanupServiceTests
{
    [Fact]
    public void Scan_reports_existing_file_counts_and_total_bytes()
    {
        using var workspace = new TempDirectory();
        var directory = Path.Combine(workspace.Path, "Storage", "Logs");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "a.log"), "12345");
        File.WriteAllText(Path.Combine(directory, "b.log"), "123");
        var service = new PortableDataCleanupService(workspace.Path);

        var entries = service.Scan([
            new PortableCleanupItem
            {
                Id = "logs",
                Name = "Logs",
                RelativePath = "Storage/Logs",
                IsDirectory = true,
                Method = PortableCleanupMethod.ClearDirectory
            }
        ]);

        var entry = Assert.Single(entries);
        Assert.True(entry.Exists);
        Assert.Equal(2, entry.FileCount);
        Assert.Equal(8, entry.TotalBytes);
    }

    [Fact]
    public void Cleanup_clears_json_file_content_without_deleting_the_file()
    {
        using var workspace = new TempDirectory();
        var target = Path.Combine(workspace.Path, "Storage", "Services", "AI", "Monitoring", "api_statistics.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "[{\"Model\":\"kimi\"}]");
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "api_stats",
                Name = "API stats",
                RelativePath = "Storage/Services/AI/Monitoring/api_statistics.json",
                Method = PortableCleanupMethod.ClearContent
            }
        ]);

        Assert.Equal(1, result.SucceededItems);
        Assert.Empty(result.Failures);
        Assert.True(File.Exists(target));
        Assert.Equal("[]", File.ReadAllText(target));
    }

    [Fact]
    public void Cleanup_clears_directory_files_but_keeps_protected_built_in_files()
    {
        using var workspace = new TempDirectory();
        var templates = Path.Combine(workspace.Path, "Storage", "Modules", "AIAssistant", "PromptTools", "PromptManagement", "templates");
        var nested = Path.Combine(templates, "built_in_templates");
        Directory.CreateDirectory(nested);
        var custom = Path.Combine(templates, "custom.json");
        var builtIn = Path.Combine(nested, "default.json");
        var protectedCategory = Path.Combine(templates, "built_in_categories.json");
        File.WriteAllText(custom, "{}");
        File.WriteAllText(builtIn, "{}");
        File.WriteAllText(protectedCategory, "[]");
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "templates",
                Name = "Templates",
                RelativePath = "Storage/Modules/AIAssistant/PromptTools/PromptManagement/templates",
                IsDirectory = true,
                Method = PortableCleanupMethod.ClearDirectory
            }
        ]);

        Assert.Equal(1, result.DeletedFiles);
        Assert.False(File.Exists(custom));
        Assert.True(File.Exists(builtIn));
        Assert.True(File.Exists(protectedCategory));
    }

    [Fact]
    public void Cleanup_keeps_only_built_in_templates_inside_template_arrays()
    {
        using var workspace = new TempDirectory();
        var templates = Path.Combine(workspace.Path, "Storage", "Modules", "AIAssistant", "PromptTools", "PromptManagement", "templates");
        Directory.CreateDirectory(templates);
        var file = Path.Combine(templates, "writing.json");
        File.WriteAllText(file, """
            [
              { "Name": "Official", "IsBuiltIn": true },
              { "Name": "User", "IsBuiltIn": false },
              { "Name": "No flag" }
            ]
            """);
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "templates",
                Name = "Templates",
                RelativePath = "Storage/Modules/AIAssistant/PromptTools/PromptManagement/templates",
                IsDirectory = true,
                Method = PortableCleanupMethod.DeleteNonBuiltIn
            }
        ]);

        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(1, result.UpdatedFiles);
        var item = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("Official", item.GetProperty("Name").GetString());
    }

    [Fact]
    public void Cleanup_rejects_relative_paths_that_escape_the_storage_root()
    {
        using var workspace = new TempDirectory();
        var outside = Path.Combine(Path.GetTempPath(), "tianming-outside-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(outside, "{}");
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "outside",
                Name = "Outside",
                RelativePath = Path.GetRelativePath(workspace.Path, outside),
                Method = PortableCleanupMethod.DeleteFile
            }
        ]);

        Assert.Equal(0, result.SucceededItems);
        Assert.Single(result.Failures);
        Assert.True(File.Exists(outside));
        File.Delete(outside);
    }

    [Fact]
    public void Cleanup_keeps_only_level_one_model_categories()
    {
        using var workspace = new TempDirectory();
        var categories = Path.Combine(workspace.Path, "Storage", "Services", "AI", "Library", "categories.json");
        Directory.CreateDirectory(Path.GetDirectoryName(categories)!);
        File.WriteAllText(categories, """
            [
              { "Id": "official", "Level": 1 },
              { "Id": "child", "Level": 2 }
            ]
            """);
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "model_categories",
                Name = "Model categories",
                RelativePath = "Storage/Services/AI/Library/categories.json",
                Method = PortableCleanupMethod.KeepModelCategoryLevel1
            }
        ]);

        using var doc = JsonDocument.Parse(File.ReadAllText(categories));
        Assert.Equal(1, result.UpdatedFiles);
        var category = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("official", category.GetProperty("Id").GetString());
    }

    [Fact]
    public void Cleanup_clears_project_volumes_chapters_tracking_guides_and_indexes()
    {
        using var workspace = new TempDirectory();
        var project = Path.Combine(workspace.Path, "Storage", "Projects", "novel-a");
        var generated = Path.Combine(project, "Generated");
        var chapters = Path.Combine(generated, "chapters", "vol-1");
        var guides = Path.Combine(project, "Config", "guides");
        var factArchives = Path.Combine(guides, "fact_archives");
        var milestones = Path.Combine(guides, "milestones");
        var plotPoints = Path.Combine(guides, "plot_points");
        var summaries = Path.Combine(guides, "summaries");
        var vectorIndex = Path.Combine(project, "VectorIndex");
        Directory.CreateDirectory(chapters);
        Directory.CreateDirectory(factArchives);
        Directory.CreateDirectory(milestones);
        Directory.CreateDirectory(plotPoints);
        Directory.CreateDirectory(summaries);
        Directory.CreateDirectory(vectorIndex);
        var categories = Path.Combine(generated, "categories.json");
        var chapter = Path.Combine(chapters, "chapter-1.md");
        var backup = Path.Combine(chapters, "chapter-1.md.bak");
        var staging = Path.Combine(chapters, "chapter-1.md.staging");
        var trackingGuide = Path.Combine(guides, "character_state_guide.json");
        var unrelatedGuide = Path.Combine(guides, "world_rules.json");
        var keywordIndex = Path.Combine(guides, "keyword_index.json");
        var vectorFile = Path.Combine(vectorIndex, "chapter-1.json");
        File.WriteAllText(categories, "[{\"Id\":\"vol-1\"}]");
        File.WriteAllText(chapter, "# chapter");
        File.WriteAllText(backup, "backup");
        File.WriteAllText(staging, "staging");
        File.WriteAllText(trackingGuide, "{}");
        File.WriteAllText(unrelatedGuide, "{}");
        File.WriteAllText(Path.Combine(factArchives, "vol-1.json"), "{}");
        File.WriteAllText(Path.Combine(milestones, "m1.txt"), "m1");
        File.WriteAllText(Path.Combine(plotPoints, "p1.json"), "{}");
        File.WriteAllText(Path.Combine(summaries, "vol-1.json"), "{}");
        File.WriteAllText(keywordIndex, "{}");
        File.WriteAllText(vectorFile, "{}");
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "project_volumes_chapters",
                Name = "Project volumes and chapters",
                RelativePath = "Storage/Projects",
                IsDirectory = true,
                Method = PortableCleanupMethod.ClearProjectVolumesAndChapters
            }
        ]);

        Assert.Equal(1, result.SucceededItems);
        Assert.Empty(result.Failures);
        Assert.Equal("[]", File.ReadAllText(categories));
        Assert.False(File.Exists(chapter));
        Assert.False(File.Exists(backup));
        Assert.False(File.Exists(staging));
        Assert.False(File.Exists(trackingGuide));
        Assert.True(File.Exists(unrelatedGuide));
        Assert.Empty(Directory.EnumerateFiles(factArchives));
        Assert.Empty(Directory.EnumerateFiles(milestones));
        Assert.Empty(Directory.EnumerateFiles(plotPoints));
        Assert.Empty(Directory.EnumerateFiles(summaries));
        Assert.False(File.Exists(keywordIndex));
        Assert.Empty(Directory.EnumerateFiles(vectorIndex));
    }

    [Fact]
    public void Cleanup_clears_project_history_version_directories_but_keeps_top_level_files()
    {
        using var workspace = new TempDirectory();
        var projectAHistory = Path.Combine(workspace.Path, "Storage", "Projects", "novel-a", "History");
        var projectBHistory = Path.Combine(workspace.Path, "Storage", "Projects", "novel-b", "History");
        var versionA = Path.Combine(projectAHistory, "v1");
        var versionB = Path.Combine(projectBHistory, "v2");
        Directory.CreateDirectory(versionA);
        Directory.CreateDirectory(versionB);
        var topLevelFile = Path.Combine(projectAHistory, "history_index.json");
        File.WriteAllText(Path.Combine(versionA, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(versionB, "manifest.json"), "{}");
        File.WriteAllText(topLevelFile, "{}");
        var service = new PortableDataCleanupService(workspace.Path);

        var result = service.Cleanup([
            new PortableCleanupItem
            {
                Id = "project_history",
                Name = "Project history",
                RelativePath = "Storage/Projects",
                IsDirectory = true,
                Method = PortableCleanupMethod.ClearProjectHistory
            }
        ]);

        Assert.Equal(1, result.SucceededItems);
        Assert.Empty(result.Failures);
        Assert.False(Directory.Exists(versionA));
        Assert.False(Directory.Exists(versionB));
        Assert.True(File.Exists(topLevelFile));
        Assert.Equal(2, result.DeletedDirectories);
    }
}
