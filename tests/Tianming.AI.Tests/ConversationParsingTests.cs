using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using Xunit;

namespace Tianming.AI.Tests;

public class ConversationParsingTests
{
    [Theory]
    [InlineData("十二", 12)]
    [InlineData("二十三", 23)]
    [InlineData("一百零五", 105)]
    [InlineData("2万零三", 20003)]
    [InlineData("0", 0)]
    [InlineData("〇", 0)]
    public void ChineseNumberParser_parses_arabic_and_chinese_numbers(string input, int expected)
    {
        Assert.Equal(expected, ChineseNumberParser.Parse(input));
        Assert.True(ChineseNumberParser.TryParse(input, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PlanStepParser_parses_numbered_step_and_chinese_step_formats_with_details()
    {
        var parser = new PlanStepParser();
        var content = """
        **1. 梳理大纲**
        读取主线与伏笔。

        步骤二：生成第十二章
        写出正文。

        第三步：回填CHANGES
        校验状态。
        """;

        var steps = parser.Parse(content);

        Assert.Equal(3, steps.Count);
        Assert.Equal((1, "梳理大纲", "读取主线与伏笔。"), (steps[0].Index, steps[0].Title, steps[0].Detail));
        Assert.Equal((2, "生成第十二章", "写出正文。"), (steps[1].Index, steps[1].Title, steps[1].Detail));
        Assert.Equal((3, "回填CHANGES", "校验状态。"), (steps[2].Index, steps[2].Title, steps[2].Detail));
    }

    [Fact]
    public void PlanStepParser_count_steps_ignores_plain_chat_without_plan_markers()
    {
        var parser = new PlanStepParser();

        Assert.Equal(0, parser.CountSteps("随便聊聊，没有计划。"));
        Assert.Equal(2, parser.CountSteps("计划：\n1. 起草\n2. 校验"));
    }

    [Fact]
    public void ChapterDirectiveParser_extracts_continue_and_rewrite_ids()
    {
        var detail = "先 @续写:CH-12 再检查；必要时 @rewrite CH-10";

        Assert.True(ChapterDirectiveParser.HasContinueDirective(detail));
        Assert.True(ChapterDirectiveParser.HasRewriteDirective(detail));
        Assert.Equal("CH-12", ChapterDirectiveParser.ParseSourceChapterId(detail));
        Assert.Equal("CH-10", ChapterDirectiveParser.ParseTargetChapterId(detail));
    }
}
