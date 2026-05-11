using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using Xunit;

namespace Tianming.AI.Tests;

public class PlanStepNormalizerTests
{
    [Fact]
    public void Normalize_merges_single_chapter_task_into_one_executable_step()
    {
        var steps = new List<PlanStep>
        {
            new() { Index = 1, Title = "整理背景", Detail = "读大纲" },
            new() { Index = 2, Title = "第十二章：风起", Detail = "写正文" },
            new() { Index = 3, Title = "检查伏笔", Detail = "写CHANGES" }
        };

        var normalized = PlanStepNormalizer.Normalize("帮我写第十二章", "", steps);

        var step = Assert.Single(normalized);
        Assert.Equal(1, step.Index);
        Assert.Equal("第12章：风起", step.Title);
        Assert.Contains("AI 原始计划", step.Detail);
        Assert.Contains("2. 第十二章：风起", step.Detail);
    }

    [Fact]
    public void Normalize_splits_chapter_range_from_user_input()
    {
        var steps = new List<PlanStep>
        {
            new() { Index = 1, Title = "生成章节", Detail = "保持节奏" },
            new() { Index = 2, Title = "回填账本", Detail = "记录变化" }
        };

        var normalized = PlanStepNormalizer.Normalize("请写第3-5章", "", steps);

        Assert.Equal([3, 4, 5], normalized.Select(step => step.ChapterNumber).ToArray());
        Assert.Equal(["第3章", "第4章", "第5章"], normalized.Select(step => step.Title).ToArray());
        Assert.Contains("创作计划概要", normalized[0].Detail);
        Assert.Contains("根据前文和大纲", normalized[1].Detail);
    }

    [Fact]
    public void Normalize_keeps_distinct_chapter_steps_without_merging()
    {
        var steps = new List<PlanStep>
        {
            new() { Index = 1, Title = "第1章：入山" },
            new() { Index = 2, Title = "第2章：试炼" }
        };

        var normalized = PlanStepNormalizer.Normalize("继续推进计划", "", steps);

        Assert.Same(steps, normalized);
    }

    [Theory]
    [InlineData("请写第10到12章", 10, 12)]
    [InlineData("从第十二章至第十四章", 12, 14)]
    public void ExtractChapterRange_parses_arabic_and_chinese_ranges(string input, int start, int end)
    {
        Assert.Equal((start, end), PlanStepNormalizer.ExtractChapterRange(input));
    }
}
