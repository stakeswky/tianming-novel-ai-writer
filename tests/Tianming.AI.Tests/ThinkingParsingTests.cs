using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using Xunit;

namespace Tianming.AI.Tests;

public class ThinkingParsingTests
{
    [Fact]
    public void ThinkingBlockParser_splits_markdown_numbered_and_colon_headings()
    {
        var blocks = ThinkingBlockParser.Parse("""
        # 目标
        明确任务
        1. 推理
        分析路径
        结论：
        可以执行
        """);

        Assert.Equal(["目标", "1. 推理", "结论"], blocks.Select(block => block.Title).ToArray());
        Assert.Equal("明确任务", blocks[0].Body);
        Assert.Equal("分析路径", blocks[1].Body);
        Assert.Equal("可以执行", blocks[2].Body);
    }

    [Fact]
    public void ThinkingBlockParser_wraps_body_without_heading_as_analysis_block()
    {
        var block = Assert.Single(ThinkingBlockParser.Parse("先观察\n再执行"));

        Assert.Equal("分析", block.Title);
        Assert.Equal("先观察\n再执行", block.Body);
    }

    [Fact]
    public void TagBasedThinkingStrategy_routes_think_and_answer_tags_across_chunks()
    {
        var strategy = new TagBasedThinkingStrategy();

        var first = strategy.Extract("开场<th");
        var second = strategy.Extract("ink>内部推理</think><answer>最终");
        var third = strategy.Extract("答案</answer>尾声");

        Assert.Equal("开场", first.AnswerContent);
        Assert.Null(first.ThinkingContent);
        Assert.Equal("内部推理", second.ThinkingContent);
        Assert.Equal("最终", second.AnswerContent);
        Assert.Equal("答案尾声", third.AnswerContent);
    }

    [Fact]
    public void TagBasedThinkingStrategy_supports_analysis_tag_noise_filtering_and_flush()
    {
        var strategy = new TagBasedThinkingStrategy();

        var first = strategy.Extract("Thinking...\n<analysis>分析中");
        var flushed = strategy.Flush();

        Assert.Null(first.AnswerContent);
        Assert.Equal("分析中", first.ThinkingContent);
        Assert.Null(flushed.ThinkingContent);
    }
}
