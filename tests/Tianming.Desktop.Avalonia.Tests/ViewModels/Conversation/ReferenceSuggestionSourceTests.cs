using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Conversation;

public class ReferenceSuggestionSourceTests
{
    [Fact]
    public async Task SuggestAsync_reads_project_chapter_character_and_world_items_with_filtering()
    {
        using var workspace = new TempDirectory();
        await SeedProjectDataAsync(workspace.Path);
        var source = new ReferenceSuggestionSource(new StubCurrentProjectService(workspace.Path));

        var results = await source.SuggestAsync("九");

        Assert.Equal(3, results.Count);
        Assert.Contains(results, item => item.Category == "Chapter" && item.Name == "第 9 章 九州风起");
        Assert.Contains(results, item => item.Category == "Character" && item.Name == "九璃");
        Assert.Contains(results, item => item.Category == "World" && item.Name == "九州大陆");
        Assert.DoesNotContain(results, item => item.Name == "诸葛清");
    }

    [Fact]
    public async Task SuggestAsync_limits_results_to_ten_items()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        for (var index = 1; index <= 12; index++)
        {
            await adapter.AddAsync(new ChapterData
            {
                Id = $"chapter-{index}",
                Category = "章节",
                Name = $"九州篇章 {index}",
                ChapterTitle = $"九州篇章 {index}",
                IsEnabled = true,
            });
        }

        var source = new ReferenceSuggestionSource(new StubCurrentProjectService(workspace.Path));

        var results = await source.SuggestAsync("九州");

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task SuggestAsync_ch_returns_matching_chapter_fixture()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        await adapter.AddAsync(new ChapterData
        {
            Id = "chapter-ch001",
            Category = "章节",
            Name = "ch001 破局",
            ChapterTitle = "ch001 破局",
            IsEnabled = true,
        });
        var source = new ReferenceSuggestionSource(new StubCurrentProjectService(workspace.Path));

        var results = await source.SuggestAsync("ch");

        Assert.Contains(results, item =>
            item.Category == "Chapter"
            && item.Name.Contains("ch001", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SeedProjectDataAsync(string root)
    {
        var chapterAdapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), root);
        await chapterAdapter.LoadAsync();
        await chapterAdapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        await chapterAdapter.AddAsync(new ChapterData
        {
            Id = "chapter-9",
            Category = "章节",
            Name = "第 9 章 九州风起",
            ChapterTitle = "九州风起",
            IsEnabled = true,
        });

        var characterAdapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(new CharacterRulesSchema(), root);
        await characterAdapter.LoadAsync();
        await characterAdapter.AddCategoryAsync(new CharacterRulesCategory { Id = "character-cat", Name = "角色", IsEnabled = true });
        await characterAdapter.AddAsync(new CharacterRulesData
        {
            Id = "character-jiuli",
            Category = "角色",
            Name = "九璃",
            IsEnabled = true,
        });
        await characterAdapter.AddAsync(new CharacterRulesData
        {
            Id = "character-zhuge",
            Category = "角色",
            Name = "诸葛清",
            IsEnabled = true,
        });

        var worldAdapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(new WorldRulesSchema(), root);
        await worldAdapter.LoadAsync();
        await worldAdapter.AddCategoryAsync(new WorldRulesCategory { Id = "world-cat", Name = "世界观", IsEnabled = true });
        await worldAdapter.AddAsync(new WorldRulesData
        {
            Id = "world-jiuzhou",
            Category = "世界观",
            Name = "九州大陆",
            IsEnabled = true,
        });
    }

    private sealed class StubCurrentProjectService(string projectRoot) : ICurrentProjectService
    {
        public string ProjectRoot { get; } = projectRoot;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-reference-source-{Guid.NewGuid():N}");

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
