using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class BatchChapterValidationPromptComposerTests
{
    [Fact]
    public void Build_includes_batch_header_chapters_context_and_json_array_contract()
    {
        var prompt = BatchChapterValidationPromptComposer.Build(
            [
                new BatchChapterValidationPromptItem
                {
                    ChapterId = "vol1_ch1",
                    ChapterTitle = "第一章 命火",
                    VolumeNumber = 1,
                    ChapterNumber = 1,
                    Content = "主角进入命火试炼。",
                    Characters = ["沈天命(少年)", "林青(师姐)"],
                    Factions = ["青岚宗"],
                    PlotRules = ["命火试炼:必须先立誓"]
                },
                new BatchChapterValidationPromptItem
                {
                    ChapterId = "vol1_ch2",
                    ChapterTitle = "第二章 余烬",
                    VolumeNumber = 1,
                    ChapterNumber = 2,
                    Content = "试炼后主角获得余烬。"
                }
            ]);

        Assert.StartsWith("<batch_validation_task>", prompt);
        Assert.Contains("<batch_size>2</batch_size>", prompt);
        Assert.Contains("请对以下每个章节分别执行校验，返回JSON数组，数组长度必须严格等于 batch_size，第i项对应第i个章节。", prompt);
        Assert.Contains("<chapter index=\"1\">", prompt);
        Assert.Contains("<chapter_id>vol1_ch1</chapter_id>", prompt);
        Assert.Contains("<chapter_info>标题=第一章 命火, 卷=1, 章=1</chapter_info>", prompt);
        Assert.Contains("<characters>沈天命(少年); 林青(师姐)</characters>", prompt);
        Assert.Contains("<factions>青岚宗</factions>", prompt);
        Assert.Contains("<plot_rules>命火试炼:必须先立誓</plot_rules>", prompt);
        Assert.Contains("<正文内容>主角进入命火试炼。</正文内容>", prompt);
        Assert.Contains("对每个章节执行10条校验规则，返回JSON数组，数组长度=2，顺序与输入章节一致：", prompt);
        Assert.Contains("\"chapterId\": \"章节ID\"", prompt);
        Assert.Contains("\"moduleResults\": {", prompt);
        Assert.Contains("每个对象的 moduleResults 必须包含全部 10 条规则", prompt);
        Assert.Contains("summary、reason、suggestion 字段中不得引用提示词中的标签名称", prompt);
        Assert.EndsWith("</batch_validation_task>" + Environment.NewLine, prompt);
    }

    [Fact]
    public void Build_limits_inline_context_counts_like_original_batch_prompt()
    {
        var prompt = BatchChapterValidationPromptComposer.Build(
            [
                new BatchChapterValidationPromptItem
                {
                    ChapterId = "vol2_ch1",
                    ChapterTitle = "第一章 长上下文",
                    VolumeNumber = 2,
                    ChapterNumber = 1,
                    Content = "正文",
                    Characters = Enumerable.Range(1, 6).Select(index => $"角色{index}(身份)").ToList(),
                    Factions = Enumerable.Range(1, 6).Select(index => $"势力{index}").ToList(),
                    PlotRules = Enumerable.Range(1, 4).Select(index => $"剧情{index}:目标{index}").ToList()
                }
            ]);

        Assert.Contains("<characters>角色1(身份); 角色2(身份); 角色3(身份); 角色4(身份); 角色5(身份)</characters>", prompt);
        Assert.DoesNotContain("角色6", prompt);
        Assert.Contains("<factions>势力1; 势力2; 势力3; 势力4; 势力5</factions>", prompt);
        Assert.DoesNotContain("势力6", prompt);
        Assert.Contains("<plot_rules>剧情1:目标1; 剧情2:目标2; 剧情3:目标3</plot_rules>", prompt);
        Assert.DoesNotContain("剧情4", prompt);
    }

    [Fact]
    public void Build_truncates_chapter_content_to_preview_length()
    {
        var prompt = BatchChapterValidationPromptComposer.Build(
            [
                new BatchChapterValidationPromptItem
                {
                    ChapterId = "vol3_ch1",
                    ChapterTitle = "第一章 长正文",
                    VolumeNumber = 3,
                    ChapterNumber = 1,
                    Content = new string('甲', 1005)
                }
            ]);

        Assert.Contains("<正文内容>" + new string('甲', 1000) + "...</正文内容>", prompt);
        Assert.DoesNotContain(new string('甲', 1001), prompt);
    }

    [Fact]
    public void Build_omits_empty_context_tags()
    {
        var prompt = BatchChapterValidationPromptComposer.Build(
            [
                new BatchChapterValidationPromptItem
                {
                    ChapterId = "vol4_ch1",
                    ChapterTitle = "第一章 空上下文",
                    VolumeNumber = 4,
                    ChapterNumber = 1,
                    Content = "正文",
                    Characters = [],
                    Factions = [],
                    PlotRules = []
                }
            ]);

        Assert.DoesNotContain("<characters>", prompt);
        Assert.DoesNotContain("<factions>", prompt);
        Assert.DoesNotContain("<plot_rules>", prompt);
    }
}
