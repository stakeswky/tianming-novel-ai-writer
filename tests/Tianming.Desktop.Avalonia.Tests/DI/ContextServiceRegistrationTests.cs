using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using TM.Services.Framework.AI;
using TM.Framework;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Infrastructure;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class ContextServiceRegistrationTests
{
    [Fact]
    public void Build_resolves_all_four_context_services()
    {
        using var sp = (ServiceProvider)AppHost.Build();

        Assert.NotNull(sp.GetRequiredService<IDesignContextService>());
        Assert.NotNull(sp.GetRequiredService<IGenerationContextService>());
        Assert.NotNull(sp.GetRequiredService<IValidationContextService>());
        Assert.NotNull(sp.GetRequiredService<IPackagingContextService>());
    }

    [Fact]
    public async Task Services_execute_against_real_project_layout_via_di()
    {
        using var workspace = new TempDirectory();
        await SeedProjectDataAsync(workspace.Path);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
        services.AddProjectDataServices();
        services.AddAIServices();
        services.AddFrameworkServices();
        services.AddAvaloniaShell();
        services.AddSingleton<ICurrentProjectService>(new StubCurrentProjectService(workspace.Path));

        using var sp = services.BuildServiceProvider();
        var design = sp.GetRequiredService<IDesignContextService>();
        var generation = sp.GetRequiredService<IGenerationContextService>();
        var validation = sp.GetRequiredService<IValidationContextService>();
        var packaging = sp.GetRequiredService<IPackagingContextService>();

        var designItems = await design.ListByCategoryAsync("Characters");
        var generationContext = await generation.BuildAsync("chapter-current");
        var validationContext = await validation.BuildAsync("chapter-current");
        var packagingSnapshot = await packaging.BuildSnapshotAsync();

        Assert.Contains(designItems, item => item.Name == "沈砚");
        Assert.Contains("沈砚", generationContext.DesignElements.CharacterNames);
        Assert.True(validationContext.RuleSet.EnableConflictFlowCheck);
        Assert.Contains("vol1_ch1", packagingSnapshot.ChapterIds);
    }

    private static async Task SeedProjectDataAsync(string root)
    {
        var chapterAdapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), root);
        var characterAdapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(new CharacterRulesSchema(), root);
        var factionAdapter = new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(new FactionRulesSchema(), root);
        var locationAdapter = new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(new LocationRulesSchema(), root);
        var plotAdapter = new ModuleDataAdapter<PlotRulesCategory, PlotRulesData>(new PlotRulesSchema(), root);

        await chapterAdapter.LoadAsync();
        await characterAdapter.LoadAsync();
        await factionAdapter.LoadAsync();
        await locationAdapter.LoadAsync();
        await plotAdapter.LoadAsync();

        await chapterAdapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
        await characterAdapter.AddCategoryAsync(new CharacterRulesCategory { Id = "character-cat", Name = "角色", IsEnabled = true });
        await factionAdapter.AddCategoryAsync(new FactionRulesCategory { Id = "faction-cat", Name = "势力", IsEnabled = true });
        await locationAdapter.AddCategoryAsync(new LocationRulesCategory { Id = "location-cat", Name = "地点", IsEnabled = true });
        await plotAdapter.AddCategoryAsync(new PlotRulesCategory { Id = "plot-cat", Name = "剧情", IsEnabled = true });

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
        await characterAdapter.AddAsync(new CharacterRulesData { Id = "char-001", Category = "角色", Name = "沈砚", IsEnabled = true });
        await factionAdapter.AddAsync(new FactionRulesData { Id = "faction-001", Category = "势力", Name = "青岚宗", IsEnabled = true });
        await locationAdapter.AddAsync(new LocationRulesData { Id = "location-001", Category = "地点", Name = "试炼台", IsEnabled = true });
        await plotAdapter.AddAsync(new PlotRulesData { Id = "plot-001", Category = "剧情", Name = "命火试炼", IsEnabled = true });

        var chapterStore = new ChapterContentStore(Path.Combine(root, "Generated", "chapters"));
        await chapterStore.SaveChapterAsync("vol1_ch1", "# 第1章 命火初醒\n\n星火入梦。");
    }

    private sealed class StubCurrentProjectService(string projectRoot) : ICurrentProjectService
    {
        public string ProjectRoot { get; } = projectRoot;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-di-{Guid.NewGuid():N}");

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
