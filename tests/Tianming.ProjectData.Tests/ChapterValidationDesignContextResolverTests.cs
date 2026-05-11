using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationDesignContextResolverTests
{
    [Fact]
    public void Resolve_formats_design_prompt_sections_like_original_validation_prompt()
    {
        var sections = ChapterValidationDesignContextResolver.Resolve(new ChapterValidationDesignContextSource
        {
            ContextIds = new ContextIdCollection
            {
                TemplateIds = ["tpl-1"],
                WorldRuleIds = ["world-1"],
                Characters = ["char-1"],
                Factions = ["faction-1"],
                Locations = ["loc-1"],
                PlotRules = ["plot-1"]
            },
            Templates =
            [
                new PromptTemplateContext
                {
                    Id = "tpl-1",
                    Name = "东方玄幻模板",
                    Genre = "玄幻",
                    OverallIdea = "命火修炼与宗门试炼交织",
                    WorldBuildingMethod = "以命火规则推动势力秩序",
                    ProtagonistDesign = "少年逆命"
                }
            ],
            WorldRules =
            [
                new PromptWorldRuleContext
                {
                    Id = "world-1",
                    Name = "命火铁律",
                    HardRules = "命火不可无代价燃烧",
                    PowerSystem = "命火九阶"
                }
            ],
            Characters =
            [
                new PromptCharacterContext
                {
                    Id = "char-1",
                    Name = "沈天命",
                    Identity = "试炼弟子",
                    Race = "人族",
                    FlawBelief = "相信独自承担才是强大",
                    Want = "点燃命火",
                    GrowthPath = "学会信任同伴"
                }
            ],
            Factions =
            [
                new PromptFactionContext
                {
                    Id = "faction-1",
                    Name = "青岚宗",
                    FactionType = "宗门",
                    Goal = "守住命火传承",
                    Leader = "char-1"
                }
            ],
            Locations =
            [
                new PromptLocationContext
                {
                    Id = "loc-1",
                    Name = "试炼台",
                    LocationType = "秘境",
                    Description = "青岚宗命火试炼核心场地",
                    Terrain = "悬空石台"
                }
            ],
            PlotRules =
            [
                new PromptPlotContext
                {
                    Id = "plot-1",
                    Name = "命火试炼",
                    StoryPhase = "入门",
                    Goal = "点燃命火",
                    Conflict = "命火反噬",
                    Result = "获得内门资格"
                }
            ]
        });

        Assert.Equal(["东方玄幻模板: 类型=玄幻, 构思=命火修炼与宗门试炼交织, 世界观构建=以命火规则推动势力秩序, 主角塑造=少年逆命"], sections.TemplateItems);
        Assert.Equal(["命火铁律: 硬规则=命火不可无代价燃烧, 力量体系=命火九阶"], sections.WorldRuleItems);
        Assert.Equal(["沈天命: 身份=试炼弟子, 种族=人族, 核心缺陷=相信独自承担才是强大, 外在目标=点燃命火, 成长路径=学会信任同伴"], sections.CharacterItems);
        Assert.Equal(["青岚宗: 类型=宗门, 目标=守住命火传承, 领袖=沈天命"], sections.FactionItems);
        Assert.Equal(["试炼台: 类型=秘境, 描述=青岚宗命火试炼核心场地, 地形=悬空石台"], sections.LocationItems);
        Assert.Equal(["命火试炼: 阶段=入门, 目标=点燃命火, 冲突=命火反噬, 结果=获得内门资格"], sections.PlotItems);
    }

    [Fact]
    public void Resolve_applies_original_take_limits_and_truncation_lengths()
    {
        var sections = ChapterValidationDesignContextResolver.Resolve(new ChapterValidationDesignContextSource
        {
            ContextIds = new ContextIdCollection
            {
                TemplateIds = Enumerable.Range(1, 4).Select(i => $"tpl-{i}").ToList(),
                WorldRuleIds = Enumerable.Range(1, 6).Select(i => $"world-{i}").ToList(),
                Characters = Enumerable.Range(1, 11).Select(i => $"char-{i}").ToList(),
                Factions = Enumerable.Range(1, 9).Select(i => $"faction-{i}").ToList(),
                Locations = Enumerable.Range(1, 9).Select(i => $"loc-{i}").ToList(),
                PlotRules = Enumerable.Range(1, 9).Select(i => $"plot-{i}").ToList()
            },
            Templates = Enumerable.Range(1, 4)
                .Select(i => new PromptTemplateContext
                {
                    Id = $"tpl-{i}",
                    Name = $"模板{i}",
                    Genre = "玄幻",
                    OverallIdea = new string('构', 61),
                    WorldBuildingMethod = new string('界', 41),
                    ProtagonistDesign = new string('主', 41)
                })
                .ToList(),
            WorldRules = Enumerable.Range(1, 6)
                .Select(i => new PromptWorldRuleContext
                {
                    Id = $"world-{i}",
                    Name = $"世界{i}",
                    HardRules = new string('硬', 61),
                    PowerSystem = new string('力', 41)
                })
                .ToList(),
            Characters = Enumerable.Range(1, 11)
                .Select(i => new PromptCharacterContext
                {
                    Id = $"char-{i}",
                    Name = $"角色{i}",
                    Identity = "身份",
                    Race = "种族",
                    FlawBelief = new string('缺', 31),
                    Want = new string('目', 31),
                    GrowthPath = new string('成', 31)
                })
                .ToList(),
            Factions = Enumerable.Range(1, 9)
                .Select(i => new PromptFactionContext
                {
                    Id = $"faction-{i}",
                    Name = $"势力{i}",
                    FactionType = "宗门",
                    Goal = new string('标', 41),
                    Leader = "unknown-leader"
                })
                .ToList(),
            Locations = Enumerable.Range(1, 9)
                .Select(i => new PromptLocationContext
                {
                    Id = $"loc-{i}",
                    Name = $"地点{i}",
                    LocationType = "秘境",
                    Description = new string('描', 41),
                    Terrain = new string('地', 31)
                })
                .ToList(),
            PlotRules = Enumerable.Range(1, 9)
                .Select(i => new PromptPlotContext
                {
                    Id = $"plot-{i}",
                    Name = $"剧情{i}",
                    StoryPhase = "阶段",
                    Goal = new string('目', 41),
                    Conflict = new string('冲', 41),
                    Result = new string('结', 41)
                })
                .ToList()
        });

        Assert.Equal(3, sections.TemplateItems.Count);
        Assert.Equal(5, sections.WorldRuleItems.Count);
        Assert.Equal(10, sections.CharacterItems.Count);
        Assert.Equal(8, sections.FactionItems.Count);
        Assert.Equal(8, sections.LocationItems.Count);
        Assert.Equal(8, sections.PlotItems.Count);
        Assert.Contains("构思=" + new string('构', 60) + "...", sections.TemplateItems[0]);
        Assert.Contains("世界观构建=" + new string('界', 40) + "...", sections.TemplateItems[0]);
        Assert.Contains("硬规则=" + new string('硬', 60) + "...", sections.WorldRuleItems[0]);
        Assert.Contains("核心缺陷=" + new string('缺', 30) + "...", sections.CharacterItems[0]);
        Assert.Contains("目标=" + new string('标', 40) + "...", sections.FactionItems[0]);
        Assert.Contains("领袖=unknown-leader", sections.FactionItems[0]);
        Assert.Contains("描述=" + new string('描', 40) + "...", sections.LocationItems[0]);
        Assert.Contains("结果=" + new string('结', 40) + "...", sections.PlotItems[0]);
    }

    [Fact]
    public void Resolve_returns_empty_sections_when_context_ids_are_missing()
    {
        var sections = ChapterValidationDesignContextResolver.Resolve(new ChapterValidationDesignContextSource
        {
            ContextIds = null,
            Templates = [new PromptTemplateContext { Id = "tpl", Name = "不会出现" }]
        });

        Assert.Empty(sections.TemplateItems);
        Assert.Empty(sections.WorldRuleItems);
        Assert.Empty(sections.CharacterItems);
        Assert.Empty(sections.FactionItems);
        Assert.Empty(sections.LocationItems);
        Assert.Empty(sections.PlotItems);
    }
}
