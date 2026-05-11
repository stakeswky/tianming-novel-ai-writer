using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationPromptInputBuilderTests
{
    [Fact]
    public void Build_merges_chapter_metadata_design_context_generate_context_and_known_issues()
    {
        var builder = new ChapterValidationPromptInputBuilder();

        var input = builder.Build(new ChapterValidationPromptInputBuildRequest
        {
            ChapterId = "vol1_ch7",
            ChapterTitle = "第七章 命火入门",
            VolumeNumber = 1,
            ChapterNumber = 7,
            VolumeName = "第一卷 试炼",
            ChapterContent = "沈天命踏入试炼台，命火第一次燃起。",
            DesignContextSource = new ChapterValidationDesignContextSource
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
                Templates = [new PromptTemplateContext { Id = "tpl-1", Name = "东方玄幻模板", Genre = "玄幻" }],
                WorldRules = [new PromptWorldRuleContext { Id = "world-1", Name = "命火铁律", HardRules = "命火不可无代价燃烧" }],
                Characters = [new PromptCharacterContext { Id = "char-1", Name = "沈天命", Identity = "试炼弟子" }],
                Factions = [new PromptFactionContext { Id = "faction-1", Name = "青岚宗", FactionType = "宗门", Leader = "char-1" }],
                Locations = [new PromptLocationContext { Id = "loc-1", Name = "试炼台", LocationType = "秘境" }],
                PlotRules = [new PromptPlotContext { Id = "plot-1", Name = "命火试炼", StoryPhase = "入门" }]
            },
            PromptContextSource = new ChapterValidationPromptContextSource
            {
                ContextIds = new ContextIdCollection
                {
                    VolumeOutline = "outline-1",
                    ChapterPlanId = "plan-1",
                    BlueprintIds = ["bp-1"],
                    VolumeDesignId = "volume-1"
                },
                Outlines = [new PromptOutlineContext { Id = "outline-1", OneLineOutline = "少年点燃命火" }],
                ChapterPlans = [new PromptChapterPlanContext { Id = "plan-1", ChapterTitle = "命火入门", MainGoal = "点燃命火" }],
                Blueprints = [new PromptBlueprintContext { Id = "bp-1", OneLineStructure = "入门-受阻-点燃" }],
                VolumeDesigns = [new PromptVolumeDesignContext { Id = "volume-1", VolumeTitle = "命火试炼" }]
            },
            KnownStructuralIssues =
            [
                new UnifiedValidationIssue { Type = "MissingEntity", Message = "正文引用未登记角色" }
            ]
        });

        Assert.Equal("vol1_ch7", input.ChapterId);
        Assert.Equal("第七章 命火入门", input.ChapterTitle);
        Assert.Equal(1, input.VolumeNumber);
        Assert.Equal(7, input.ChapterNumber);
        Assert.Equal("第一卷 试炼", input.VolumeName);
        Assert.Equal("沈天命踏入试炼台，命火第一次燃起。", input.ChapterContent);
        Assert.Contains(input.TemplateItems, item => item.Contains("东方玄幻模板: 类型=玄幻", StringComparison.Ordinal));
        Assert.Contains(input.WorldRuleItems, item => item.Contains("命火铁律: 硬规则=命火不可无代价燃烧", StringComparison.Ordinal));
        Assert.Contains("沈天命: 身份=试炼弟子", input.CharacterItems[0], StringComparison.Ordinal);
        Assert.Contains("青岚宗: 类型=宗门", input.FactionItems[0], StringComparison.Ordinal);
        Assert.Contains("试炼台: 类型=秘境", input.LocationItems[0], StringComparison.Ordinal);
        Assert.Contains("命火试炼: 阶段=入门", input.PlotItems[0], StringComparison.Ordinal);
        Assert.Contains(input.OutlineItems, item => item.Contains("一句话大纲=少年点燃命火", StringComparison.Ordinal));
        Assert.Contains(input.ChapterPlanItems, item => item.Contains("标题=命火入门", StringComparison.Ordinal));
        Assert.Contains("结构=入门-受阻-点燃", input.BlueprintItems[0], StringComparison.Ordinal);
        Assert.Contains(input.VolumeDesignItems, item => item.Contains("卷标题=命火试炼", StringComparison.Ordinal));
        Assert.Equal("正文引用未登记角色", Assert.Single(input.KnownStructuralIssues).Message);
    }

    [Fact]
    public void BuildPrompt_uses_resolved_context_without_missing_rules_when_all_sections_are_present()
    {
        var builder = new ChapterValidationPromptInputBuilder();

        var prompt = builder.BuildPrompt(new ChapterValidationPromptInputBuildRequest
        {
            ChapterId = "vol1_ch8",
            ChapterTitle = "第八章 星火成阵",
            VolumeNumber = 1,
            ChapterNumber = 8,
            VolumeName = "第一卷",
            ChapterContent = "命火在阵纹中稳定下来。",
            DesignContextSource = AllDesignSections(),
            PromptContextSource = AllPromptSections()
        });

        Assert.Contains("<section name=\"创作模板（文风约束）\">", prompt);
        Assert.Contains("<section name=\"全书大纲\">", prompt);
        Assert.Contains("命火在阵纹中稳定下来。", prompt);
        Assert.DoesNotContain("<缺失数据说明>", prompt);
    }

    [Fact]
    public void Build_handles_null_context_sources_by_returning_empty_context_sections()
    {
        var builder = new ChapterValidationPromptInputBuilder();

        var input = builder.Build(new ChapterValidationPromptInputBuildRequest
        {
            ChapterId = "vol1_ch9",
            ChapterTitle = "第九章 空白",
            VolumeNumber = 1,
            ChapterNumber = 9,
            VolumeName = "第一卷",
            ChapterContent = "正文"
        });

        Assert.Empty(input.TemplateItems);
        Assert.Empty(input.WorldRuleItems);
        Assert.Empty(input.CharacterItems);
        Assert.Empty(input.FactionItems);
        Assert.Empty(input.LocationItems);
        Assert.Empty(input.PlotItems);
        Assert.Empty(input.OutlineItems);
        Assert.Empty(input.ChapterPlanItems);
        Assert.Empty(input.BlueprintItems);
        Assert.Empty(input.VolumeDesignItems);
        Assert.Empty(input.KnownStructuralIssues);
    }

    private static ChapterValidationDesignContextSource AllDesignSections()
    {
        return new ChapterValidationDesignContextSource
        {
            ContextIds = new ContextIdCollection
            {
                TemplateIds = ["tpl"],
                WorldRuleIds = ["world"],
                Characters = ["char"],
                Factions = ["faction"],
                Locations = ["loc"],
                PlotRules = ["plot"]
            },
            Templates = [new PromptTemplateContext { Id = "tpl", Name = "模板", Genre = "玄幻" }],
            WorldRules = [new PromptWorldRuleContext { Id = "world", Name = "世界", HardRules = "硬规则" }],
            Characters = [new PromptCharacterContext { Id = "char", Name = "角色", Identity = "弟子" }],
            Factions = [new PromptFactionContext { Id = "faction", Name = "势力", FactionType = "宗门" }],
            Locations = [new PromptLocationContext { Id = "loc", Name = "地点", LocationType = "秘境" }],
            PlotRules = [new PromptPlotContext { Id = "plot", Name = "剧情", StoryPhase = "入门" }]
        };
    }

    private static ChapterValidationPromptContextSource AllPromptSections()
    {
        return new ChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection
            {
                VolumeOutline = "outline",
                ChapterPlanId = "plan",
                BlueprintIds = ["bp"],
                VolumeDesignId = "volume"
            },
            Outlines = [new PromptOutlineContext { Id = "outline", OneLineOutline = "大纲" }],
            ChapterPlans = [new PromptChapterPlanContext { Id = "plan", ChapterTitle = "章节", MainGoal = "目标" }],
            Blueprints = [new PromptBlueprintContext { Id = "bp", OneLineStructure = "结构" }],
            VolumeDesigns = [new PromptVolumeDesignContext { Id = "volume", VolumeTitle = "分卷" }]
        };
    }
}
