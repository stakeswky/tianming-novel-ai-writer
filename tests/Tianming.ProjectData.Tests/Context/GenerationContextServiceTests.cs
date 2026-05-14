using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class GenerationContextServiceTests
{
    [Fact]
    public async Task Build_returns_context_with_chapter_id_set()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new GenerationContextService(
            root,
            (chapterId, ct) => Task.FromResult(new FactSnapshot()),
            ct => Task.FromResult(new DesignElementNames()),
            (chapterId, ct) => Task.FromResult("Previous chapters summary text"));

        var context = await svc.BuildAsync("ch-005");

        Assert.Equal("ch-005", context.ChapterId);
        Assert.NotNull(context.FactSnapshot);
        Assert.NotNull(context.DesignElements);
        Assert.Equal("Previous chapters summary text", context.PreviousChaptersSummary);
    }

    [Fact]
    public async Task Build_passes_chapter_id_to_fact_and_summary_providers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string? factChapterId = null;
        string? summaryChapterId = null;

        var svc = new GenerationContextService(
            root,
            (chapterId, ct) =>
            {
                factChapterId = chapterId;
                return Task.FromResult(new FactSnapshot());
            },
            ct => Task.FromResult(new DesignElementNames()),
            (chapterId, ct) =>
            {
                summaryChapterId = chapterId;
                return Task.FromResult(string.Empty);
            });

        await svc.BuildAsync("ch-009");

        Assert.Equal("ch-009", factChapterId);
        Assert.Equal("ch-009", summaryChapterId);
    }

    [Fact]
    public async Task Build_uses_real_module_store_dependencies_to_assemble_names_and_previous_summary()
    {
        using var workspace = new TempDirectory();
        var chapterAdapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), workspace.Path);
        var characterAdapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(new CharacterRulesSchema(), workspace.Path);
        var factionAdapter = new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(new FactionRulesSchema(), workspace.Path);
        var locationAdapter = new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(new LocationRulesSchema(), workspace.Path);
        var plotAdapter = new ModuleDataAdapter<PlotRulesCategory, PlotRulesData>(new PlotRulesSchema(), workspace.Path);
        var worldRuleAdapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(new WorldRulesSchema(), workspace.Path);

        await SeedModuleDataAsync(chapterAdapter, characterAdapter, factionAdapter, locationAdapter, plotAdapter, worldRuleAdapter);

        var svc = new GenerationContextService(
            workspace.Path,
            chapterAdapter,
            characterAdapter,
            factionAdapter,
            locationAdapter,
            plotAdapter,
            worldRuleAdapter);

        var context = await svc.BuildAsync("chapter-current");

        Assert.Equal("chapter-current", context.ChapterId);
        Assert.Contains("沈砚", context.DesignElements.CharacterNames);
        Assert.Contains("青岚宗", context.DesignElements.FactionNames);
        Assert.Contains("试炼台", context.DesignElements.LocationNames);
        Assert.Contains("命火试炼", context.DesignElements.PlotKeyNames);
        Assert.Contains("第1章 命火初醒", context.PreviousChaptersSummary);
        Assert.Equal("黑发", context.FactSnapshot.CharacterDescriptions["char-001"].HairColor);
        Assert.Contains("试炼台", context.FactSnapshot.LocationDescriptions["location-001"].Name);
        Assert.Contains(context.FactSnapshot.WorldRuleConstraints, rule => rule.RuleId == "world-001" && rule.Constraint == "命火不可无代价燃烧");
    }

    private static async Task SeedModuleDataAsync(
        ModuleDataAdapter<ChapterCategory, ChapterData> chapterAdapter,
        ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> characterAdapter,
        ModuleDataAdapter<FactionRulesCategory, FactionRulesData> factionAdapter,
        ModuleDataAdapter<LocationRulesCategory, LocationRulesData> locationAdapter,
        ModuleDataAdapter<PlotRulesCategory, PlotRulesData> plotAdapter,
        ModuleDataAdapter<WorldRulesCategory, WorldRulesData> worldRuleAdapter)
    {
        await chapterAdapter.LoadAsync();
        await characterAdapter.LoadAsync();
        await factionAdapter.LoadAsync();
        await locationAdapter.LoadAsync();
        await plotAdapter.LoadAsync();
        await worldRuleAdapter.LoadAsync();

        await chapterAdapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        await characterAdapter.AddCategoryAsync(new CharacterRulesCategory { Id = "character-cat", Name = "角色", IsEnabled = true });
        await factionAdapter.AddCategoryAsync(new FactionRulesCategory { Id = "faction-cat", Name = "势力", IsEnabled = true });
        await locationAdapter.AddCategoryAsync(new LocationRulesCategory { Id = "location-cat", Name = "地点", IsEnabled = true });
        await plotAdapter.AddCategoryAsync(new PlotRulesCategory { Id = "plot-cat", Name = "剧情", IsEnabled = true });
        await worldRuleAdapter.AddCategoryAsync(new WorldRulesCategory { Id = "world-cat", Name = "世界观", IsEnabled = true });

        await chapterAdapter.AddAsync(new ChapterData
        {
            Id = "chapter-prev",
            Category = "章节",
            Name = "第1章 命火初醒",
            ChapterTitle = "命火初醒",
            ChapterNumber = 1,
            MainGoal = "点燃命火",
            IsEnabled = true,
        });
        await chapterAdapter.AddAsync(new ChapterData
        {
            Id = "chapter-current",
            Category = "章节",
            Name = "第2章 宗门试炼",
            ChapterTitle = "宗门试炼",
            ChapterNumber = 2,
            MainGoal = "通过试炼",
            ReferencedCharacterNames = ["沈砚"],
            ReferencedFactionNames = ["青岚宗"],
            ReferencedLocationNames = ["试炼台"],
            IsEnabled = true,
        });
        await characterAdapter.AddAsync(new CharacterRulesData
        {
            Id = "char-001",
            Category = "角色",
            Name = "沈砚",
            Appearance = "黑发剑客",
            Identity = "试炼弟子",
            Want = "点燃命火",
            IsEnabled = true
        });
        await factionAdapter.AddAsync(new FactionRulesData { Id = "faction-001", Category = "势力", Name = "青岚宗", IsEnabled = true });
        await locationAdapter.AddAsync(new LocationRulesData
        {
            Id = "location-001",
            Category = "地点",
            Name = "试炼台",
            Description = "命火试炼核心场地",
            Terrain = "悬空石台",
            IsEnabled = true
        });
        await plotAdapter.AddAsync(new PlotRulesData { Id = "plot-001", Category = "剧情", Name = "命火试炼", IsEnabled = true });
        await worldRuleAdapter.AddAsync(new WorldRulesData
        {
            Id = "world-001",
            Category = "世界观",
            Name = "命火铁律",
            HardRules = "命火不可无代价燃烧",
            IsEnabled = true
        });
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");

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
