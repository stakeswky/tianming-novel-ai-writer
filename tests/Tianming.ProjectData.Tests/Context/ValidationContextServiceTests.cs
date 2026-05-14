using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class ValidationContextServiceTests
{
    [Fact]
    public async Task Build_returns_bundle_with_rule_and_snapshot()
    {
        var svc = new ValidationContextService(
            ct => Task.FromResult(new LedgerRuleSet()),
            (chapterId, ct) => Task.FromResult(new FactSnapshot()));

        var bundle = await svc.BuildAsync("ch-001");

        Assert.Equal("ch-001", bundle.ChapterId);
        Assert.NotNull(bundle.RuleSet);
        Assert.NotNull(bundle.FactSnapshot);
    }

    [Fact]
    public async Task Build_passes_chapter_id_to_snapshot_provider()
    {
        string? requestedChapterId = null;
        var svc = new ValidationContextService(
            ct => Task.FromResult(new LedgerRuleSet()),
            (chapterId, ct) =>
            {
                requestedChapterId = chapterId;
                return Task.FromResult(new FactSnapshot());
            });

        await svc.BuildAsync("ch-002");

        Assert.Equal("ch-002", requestedChapterId);
    }

    [Fact]
    public async Task Build_uses_real_module_store_dependencies_to_assemble_bundle()
    {
        using var workspace = new TempDirectory();
        var chapterAdapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), workspace.Path);
        var characterAdapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(new CharacterRulesSchema(), workspace.Path);
        var factionAdapter = new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(new FactionRulesSchema(), workspace.Path);
        var locationAdapter = new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(new LocationRulesSchema(), workspace.Path);
        var worldRuleAdapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(new WorldRulesSchema(), workspace.Path);
        await SeedModuleDataAsync(chapterAdapter, characterAdapter, factionAdapter, locationAdapter, worldRuleAdapter);

        var svc = new ValidationContextService(
            workspace.Path,
            chapterAdapter,
            characterAdapter,
            factionAdapter,
            locationAdapter,
            worldRuleAdapter,
            new LedgerRuleSetProvider());

        var bundle = await svc.BuildAsync("chapter-current");

        Assert.Equal("chapter-current", bundle.ChapterId);
        Assert.True(bundle.RuleSet.EnableConflictFlowCheck);
        Assert.NotNull(bundle.FactSnapshot);
        Assert.Equal("黑发", bundle.FactSnapshot.CharacterDescriptions["char-001"].HairColor);
        Assert.Contains("试炼台", bundle.FactSnapshot.LocationDescriptions["location-001"].Name);
        Assert.Contains(bundle.FactSnapshot.WorldRuleConstraints, rule => rule.RuleId == "world-001");
    }

    private static async Task SeedModuleDataAsync(
        ModuleDataAdapter<ChapterCategory, ChapterData> chapterAdapter,
        ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> characterAdapter,
        ModuleDataAdapter<FactionRulesCategory, FactionRulesData> factionAdapter,
        ModuleDataAdapter<LocationRulesCategory, LocationRulesData> locationAdapter,
        ModuleDataAdapter<WorldRulesCategory, WorldRulesData> worldRuleAdapter)
    {
        await chapterAdapter.LoadAsync();
        await characterAdapter.LoadAsync();
        await factionAdapter.LoadAsync();
        await locationAdapter.LoadAsync();
        await worldRuleAdapter.LoadAsync();

        await chapterAdapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        await characterAdapter.AddCategoryAsync(new CharacterRulesCategory { Id = "character-cat", Name = "角色", IsEnabled = true });
        await factionAdapter.AddCategoryAsync(new FactionRulesCategory { Id = "faction-cat", Name = "势力", IsEnabled = true });
        await locationAdapter.AddCategoryAsync(new LocationRulesCategory { Id = "location-cat", Name = "地点", IsEnabled = true });
        await worldRuleAdapter.AddCategoryAsync(new WorldRulesCategory { Id = "world-cat", Name = "世界观", IsEnabled = true });

        await chapterAdapter.AddAsync(new ChapterData
        {
            Id = "chapter-current",
            Category = "章节",
            Name = "第2章 宗门试炼",
            ChapterTitle = "宗门试炼",
            ChapterNumber = 2,
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
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-vc-{System.Guid.NewGuid():N}");

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
