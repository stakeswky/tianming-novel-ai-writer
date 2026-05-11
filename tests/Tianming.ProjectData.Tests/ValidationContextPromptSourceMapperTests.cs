using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Contexts;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Models.Guides;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ValidationContextPromptSourceMapperTests
{
    [Fact]
    public void Map_builds_prompt_sources_from_validation_context_and_context_ids()
    {
        var sources = ValidationContextPromptSourceMapper.Map(new ValidationContext
        {
            Design =
            {
                Templates =
                {
                    CreativeMaterials =
                    [
                        new CreativeMaterialData
                        {
                            Id = "tpl-1",
                            Name = "东方玄幻模板",
                            Genre = "玄幻",
                            OverallIdea = "命火试炼",
                            WorldBuildingMethod = "以宗门法度塑造秩序",
                            ProtagonistDesign = "少年逆命"
                        }
                    ]
                },
                Worldview =
                {
                    WorldRules =
                    [
                        new WorldRulesData
                        {
                            Id = "world-1",
                            Name = "命火铁律",
                            HardRules = "命火不可无代价燃烧",
                            PowerSystem = "命火九阶"
                        }
                    ]
                },
                Characters =
                {
                    CharacterRules =
                    [
                        new CharacterRulesData
                        {
                            Id = "char-1",
                            Name = "沈天命",
                            Identity = "试炼弟子",
                            Race = "人族",
                            FlawBelief = "必须独自承担",
                            Want = "点燃命火",
                            GrowthPath = "学会信任同伴"
                        }
                    ]
                },
                Factions =
                {
                    FactionRules =
                    [
                        new FactionRulesData
                        {
                            Id = "faction-1",
                            Name = "青岚宗",
                            FactionType = "宗门",
                            Goal = "守住命火传承",
                            Leader = "char-1"
                        }
                    ]
                },
                Locations =
                {
                    LocationRules =
                    [
                        new LocationRulesData
                        {
                            Id = "loc-1",
                            Name = "试炼台",
                            LocationType = "秘境",
                            Description = "命火试炼核心场地",
                            Terrain = "悬空石台"
                        }
                    ]
                },
                Plot =
                {
                    PlotRules =
                    [
                        new PlotRulesData
                        {
                            Id = "plot-1",
                            Name = "命火试炼",
                            StoryPhase = "入门",
                            Goal = "点燃命火",
                            Conflict = "命火反噬",
                            Result = "获得内门资格"
                        }
                    ]
                }
            },
            Generate =
            {
                Outline =
                {
                    Outlines =
                    [
                        new OutlineData
                        {
                            Id = "outline-1",
                            OneLineOutline = "少年点燃命火",
                            CoreConflict = "命火代价与宗门规训冲突",
                            Theme = "自由与代价",
                            EndingState = "进入内门"
                        }
                    ]
                },
                Planning =
                {
                    Chapters =
                    [
                        new ChapterData
                        {
                            Id = "plan-1",
                            ChapterTitle = "第七章 命火入门",
                            ChapterTheme = "入门试炼",
                            MainGoal = "点燃命火",
                            KeyTurn = "命火反噬",
                            Hook = "内门长老出现",
                            Foreshadowing = "黑色火痕"
                        }
                    ]
                },
                Blueprint =
                {
                    Blueprints =
                    [
                        new BlueprintData
                        {
                            Id = "bp-1",
                            OneLineStructure = "入门-受阻-点燃",
                            PacingCurve = "慢-快-急",
                            Cast = "沈天命",
                            Locations = "试炼台"
                        }
                    ]
                },
                VolumeDesign =
                {
                    VolumeDesigns =
                    [
                        new VolumeDesignData
                        {
                            Id = "volume-1",
                            VolumeTitle = "命火试炼",
                            VolumeTheme = "代价",
                            StageGoal = "完成入门",
                            MainConflict = "命火失控",
                            KeyEvents = "点燃命火; 长老收徒"
                        }
                    ]
                }
            }
        }, new ContextIdCollection
        {
            TemplateIds = ["tpl-1"],
            WorldRuleIds = ["world-1"],
            Characters = ["char-1"],
            Factions = ["faction-1"],
            Locations = ["loc-1"],
            PlotRules = ["plot-1"],
            VolumeOutline = "outline-1",
            ChapterPlanId = "plan-1",
            BlueprintIds = ["bp-1"],
            VolumeDesignId = "volume-1"
        });

        Assert.NotNull(sources.DesignContextSource);
        Assert.NotNull(sources.PromptContextSource);
        Assert.Equal("东方玄幻模板", Assert.Single(sources.DesignContextSource.Templates).Name);
        Assert.Equal("命火铁律", Assert.Single(sources.DesignContextSource.WorldRules).Name);
        Assert.Equal("沈天命", Assert.Single(sources.DesignContextSource.Characters).Name);
        Assert.Equal("青岚宗", Assert.Single(sources.DesignContextSource.Factions).Name);
        Assert.Equal("试炼台", Assert.Single(sources.DesignContextSource.Locations).Name);
        Assert.Equal("命火试炼", Assert.Single(sources.DesignContextSource.PlotRules).Name);
        Assert.Equal("少年点燃命火", Assert.Single(sources.PromptContextSource.Outlines).OneLineOutline);
        Assert.Equal("第七章 命火入门", Assert.Single(sources.PromptContextSource.ChapterPlans).ChapterTitle);
        Assert.Equal("入门-受阻-点燃", Assert.Single(sources.PromptContextSource.Blueprints).OneLineStructure);
        Assert.Equal("命火试炼", Assert.Single(sources.PromptContextSource.VolumeDesigns).VolumeTitle);
        Assert.Same(sources.DesignContextSource.ContextIds, sources.PromptContextSource.ContextIds);
    }

    [Fact]
    public void Map_handles_null_context_by_returning_empty_sources_with_context_ids()
    {
        var ids = new ContextIdCollection { TemplateIds = ["missing"] };

        var sources = ValidationContextPromptSourceMapper.Map(null, ids);

        Assert.Same(ids, sources.DesignContextSource?.ContextIds);
        Assert.Same(ids, sources.PromptContextSource?.ContextIds);
        Assert.Empty(sources.DesignContextSource!.Templates);
        Assert.Empty(sources.PromptContextSource!.Outlines);
    }
}
