using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationPromptContextResolverTests
{
    [Fact]
    public void Resolve_matches_original_generate_context_section_formats()
    {
        var sections = ChapterValidationPromptContextResolver.Resolve(new ChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection
            {
                VolumeOutline = "outline-1",
                ChapterPlanId = "plan-1",
                BlueprintIds = ["bp-1", "bp-2"],
                VolumeDesignId = "volume-1"
            },
            Outlines =
            [
                new PromptOutlineContext
                {
                    Id = "outline-1",
                    OneLineOutline = "少年点燃命火并踏入宗门试炼",
                    CoreConflict = "命火代价与宗门规训冲突",
                    Theme = "自由与代价",
                    EndingState = "主角进入内门"
                }
            ],
            ChapterPlans =
            [
                new PromptChapterPlanContext
                {
                    Id = "plan-1",
                    ChapterTitle = "第七章 命火入门",
                    ChapterTheme = "入门试炼",
                    MainGoal = "点燃命火",
                    KeyTurn = "命火反噬",
                    Hook = "内门长老出现",
                    Foreshadowing = "黑色火痕"
                }
            ],
            Blueprints =
            [
                new PromptBlueprintContext
                {
                    Id = "bp-1",
                    OneLineStructure = "入门-受阻-点燃",
                    PacingCurve = "慢-快-急",
                    Cast = "沈天命, 林青",
                    Locations = "试炼台"
                },
                new PromptBlueprintContext
                {
                    Id = "bp-2",
                    OneLineStructure = "余波-试探",
                    PacingCurve = "缓",
                    Cast = "长老",
                    Locations = "内门"
                }
            ],
            VolumeDesigns =
            [
                new PromptVolumeDesignContext
                {
                    Id = "volume-1",
                    VolumeTitle = "命火试炼",
                    VolumeTheme = "代价",
                    StageGoal = "完成入门",
                    MainConflict = "命火失控",
                    KeyEvents = "点燃命火; 长老收徒"
                }
            ]
        });

        Assert.Equal(
            [
                "一句话大纲=少年点燃命火并踏入宗门试炼",
                "核心冲突=命火代价与宗门规训冲突",
                "主题=自由与代价",
                "结局状态=主角进入内门"
            ],
            sections.OutlineItems);
        Assert.Equal(
            [
                "标题=第七章 命火入门",
                "主题=入门试炼",
                "主目标=点燃命火",
                "关键转折=命火反噬",
                "结尾钩子=内门长老出现",
                "伏笔=黑色火痕"
            ],
            sections.ChapterPlanItems);
        Assert.Equal(2, sections.BlueprintItems.Count);
        Assert.Equal("结构=入门-受阻-点燃, 节奏=慢-快-急, 角色=沈天命, 林青, 地点=试炼台", sections.BlueprintItems[0]);
        Assert.Equal(
            [
                "卷标题=命火试炼",
                "卷主题=代价",
                "阶段目标=完成入门",
                "主冲突=命火失控",
                "关键事件=点燃命火; 长老收徒"
            ],
            sections.VolumeDesignItems);
    }

    [Fact]
    public void Resolve_applies_original_truncation_limits_and_blueprint_take_limit()
    {
        var sections = ChapterValidationPromptContextResolver.Resolve(new ChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection
            {
                VolumeOutline = "outline",
                ChapterPlanId = "plan",
                BlueprintIds = Enumerable.Range(1, 7).Select(i => $"bp-{i}").ToList(),
                VolumeDesignId = "volume"
            },
            Outlines =
            [
                new PromptOutlineContext
                {
                    Id = "outline",
                    OneLineOutline = new string('大', 81),
                    CoreConflict = new string('冲', 61),
                    Theme = new string('题', 61),
                    EndingState = new string('终', 61)
                }
            ],
            ChapterPlans =
            [
                new PromptChapterPlanContext
                {
                    Id = "plan",
                    ChapterTitle = "长章",
                    ChapterTheme = new string('主', 61),
                    MainGoal = new string('目', 61),
                    KeyTurn = new string('转', 61),
                    Hook = new string('钩', 61),
                    Foreshadowing = new string('伏', 61)
                }
            ],
            Blueprints = Enumerable.Range(1, 7)
                .Select(i => new PromptBlueprintContext
                {
                    Id = $"bp-{i}",
                    OneLineStructure = new string('结', 61),
                    PacingCurve = new string('节', 41),
                    Cast = new string('角', 41),
                    Locations = new string('地', 41)
                })
                .ToList(),
            VolumeDesigns =
            [
                new PromptVolumeDesignContext
                {
                    Id = "volume",
                    VolumeTitle = "长卷",
                    VolumeTheme = new string('卷', 61),
                    StageGoal = new string('阶', 61),
                    MainConflict = new string('矛', 61),
                    KeyEvents = new string('事', 61)
                }
            ]
        });

        Assert.Contains("一句话大纲=" + new string('大', 80) + "...", sections.OutlineItems);
        Assert.Contains("主题=" + new string('主', 60) + "...", sections.ChapterPlanItems);
        Assert.Equal(5, sections.BlueprintItems.Count);
        Assert.Contains("结构=" + new string('结', 60) + "...", sections.BlueprintItems[0]);
        Assert.Contains("节奏=" + new string('节', 40) + "...", sections.BlueprintItems[0]);
        Assert.Contains("卷主题=" + new string('卷', 60) + "...", sections.VolumeDesignItems);
    }

    [Fact]
    public void Resolve_returns_empty_sections_when_ids_are_missing_or_unmatched()
    {
        var sections = ChapterValidationPromptContextResolver.Resolve(new ChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection { ChapterPlanId = "missing" },
            ChapterPlans =
            [
                new PromptChapterPlanContext { Id = "other", ChapterTitle = "不会出现" }
            ]
        });

        Assert.Empty(sections.OutlineItems);
        Assert.Empty(sections.ChapterPlanItems);
        Assert.Empty(sections.BlueprintItems);
        Assert.Empty(sections.VolumeDesignItems);
    }
}
