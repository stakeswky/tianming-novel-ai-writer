using TM.Services.Modules.ProjectData.Implementations;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationPromptComposerTests
{
    [Fact]
    public void Build_includes_chapter_info_sections_requirements_template_and_rules()
    {
        var prompt = ChapterValidationPromptComposer.Build(new ChapterValidationPromptInput
        {
            ChapterId = "vol1_ch7",
            ChapterTitle = "第七章 命火入门",
            VolumeNumber = 1,
            ChapterNumber = 7,
            VolumeName = "第一卷 试炼",
            ChapterContent = "这一章主角第一次进入命火试炼。",
            TemplateItems = ["玄幻模板: 类型=东方玄幻"],
            WorldRuleItems = ["命火规则: 硬规则=不可逆转"],
            CharacterItems = ["沈天命: 身份=少年"],
            FactionItems = ["青岚宗: 类型=宗门"],
            LocationItems = ["试炼台: 类型=秘境"],
            PlotItems = ["命火试炼: 阶段=入门"],
            OutlineItems = ["一句话大纲=少年入门"],
            ChapterPlanItems = ["第七章: 主目标=点燃命火"],
            BlueprintItems = ["结构=引入-试炼-收束"],
            VolumeDesignItems = ["卷标题=试炼卷"]
        });

        Assert.StartsWith("<validation_task>", prompt);
        Assert.Contains("<chapter_info>", prompt);
        Assert.Contains("- 章节ID: vol1_ch7", prompt);
        Assert.Contains("<section name=\"创作模板（文风约束）\">", prompt);
        Assert.Contains("- 玄幻模板: 类型=东方玄幻", prompt);
        Assert.Contains("<正文内容>", prompt);
        Assert.Contains("这一章主角第一次进入命火试炼。", prompt);
        Assert.DoesNotContain("<缺失数据说明>", prompt);
        Assert.Contains("请对章节执行10条校验规则，返回JSON格式的校验结果。", prompt);
        Assert.Contains("\"moduleResults\"", prompt);
        Assert.Contains("StyleConsistency（文风模板一致性）", prompt);
        Assert.EndsWith("</validation_task>" + Environment.NewLine, prompt);
    }

    [Fact]
    public void Build_lists_missing_rules_when_context_sections_are_empty()
    {
        var prompt = ChapterValidationPromptComposer.Build(new ChapterValidationPromptInput
        {
            ChapterId = "vol1_ch8",
            ChapterTitle = "第八章 空白",
            VolumeNumber = 1,
            ChapterNumber = 8,
            VolumeName = "第一卷",
            ChapterContent = "正文"
        });

        Assert.Contains("<缺失数据说明>", prompt);
        Assert.Contains("- StyleConsistency（文风模板一致性）", prompt);
        Assert.Contains("- BlueprintConsistency（章节蓝图一致性）", prompt);
        Assert.Contains("以下规则缺少对应数据，请将 result 填写为\"未校验\"（系统按警告处理），problemItems 可为空：", prompt);
    }

    [Fact]
    public void Build_truncates_chapter_content_to_preview_length()
    {
        var prompt = ChapterValidationPromptComposer.Build(new ChapterValidationPromptInput
        {
            ChapterId = "vol1_ch9",
            ChapterTitle = "第九章 长文",
            VolumeNumber = 1,
            ChapterNumber = 9,
            VolumeName = "第一卷",
            ChapterContent = new string('中', 1005)
        });

        Assert.Contains(new string('中', 1000) + "...", prompt);
        Assert.DoesNotContain(new string('中', 1001), prompt);
    }

    [Fact]
    public void Build_includes_known_structural_issues_and_instructs_ai_not_to_duplicate_them()
    {
        var prompt = ChapterValidationPromptComposer.Build(new ChapterValidationPromptInput
        {
            ChapterId = "vol2_ch1",
            ChapterTitle = "第一章 裂隙",
            VolumeNumber = 2,
            ChapterNumber = 1,
            VolumeName = "第二卷",
            ChapterContent = "正文",
            KnownStructuralIssues =
            [
                new UnifiedValidationIssue { Type = "MissingEntity", Message = "正文引用未登记角色" }
            ]
        });

        Assert.Contains("<已确认结构性问题>", prompt);
        Assert.Contains("- [MissingEntity] 正文引用未登记角色", prompt);
        Assert.Contains("不要重复检查上述已确认问题", prompt);
    }

    [Fact]
    public void Build_limits_each_section_to_eight_non_empty_lines()
    {
        var prompt = ChapterValidationPromptComposer.Build(new ChapterValidationPromptInput
        {
            ChapterId = "vol3_ch1",
            ChapterTitle = "第一章 多素材",
            VolumeNumber = 3,
            ChapterNumber = 1,
            VolumeName = "第三卷",
            ChapterContent = "正文",
            PlotItems = Enumerable.Range(1, 10).Select(index => index == 5 ? "" : $"剧情{index}").ToList()
        });

        Assert.Contains("- 剧情1", prompt);
        Assert.Contains("- 剧情9", prompt);
        Assert.DoesNotContain("- 剧情10", prompt);
    }
}
