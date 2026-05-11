using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using Xunit;

namespace Tianming.AI.Tests;

public class ConversationSummarizerTests
{
    [Fact]
    public void ForPlanGenerated_reports_step_count_and_panel_hint()
    {
        var summary = ConversationSummarizer.ForPlanGenerated(4);

        Assert.Contains("4 个步骤", summary);
        Assert.Contains("执行计划", summary);
    }

    [Fact]
    public void ForExecutionCompleted_prefers_chapter_title_then_chapter_id_then_generic_text()
    {
        Assert.Equal("「第1章：入山」生成完毕，共 2 步。", ConversationSummarizer.ForExecutionCompleted("第1章：入山", "vol1_ch1", "共 2 步"));
        Assert.Equal("章节 vol1_ch2 生成完毕，共 2 步。", ConversationSummarizer.ForExecutionCompleted(null, "vol1_ch2", "共 2 步"));
        Assert.Equal("创作任务执行完毕，共 2 步。", ConversationSummarizer.ForExecutionCompleted(null, null, "共 2 步"));
    }

    [Fact]
    public void ExecutionTraceSummary_formats_failures_duration_and_truncates_long_failure_list()
    {
        var summary = new ExecutionTraceSummary
        {
            TotalSteps = 5,
            CompletedSteps = 2,
            FailedSteps = 4,
            TotalDurationSeconds = 12.34,
            FailedStepSummaries =
            [
                "步骤 1「起草」: 超时",
                "步骤 2「校验」: 解析失败",
                "步骤 3「保存」: 鉴权失败",
                "步骤 4「索引」: 限流"
            ]
        };

        var text = summary.ToSummaryText();

        Assert.Contains("共 5 步（4 失败），耗时 12.3s", text);
        Assert.Contains("步骤 1", text);
        Assert.Contains("步骤 3", text);
        Assert.Contains("还有 1 个失败未展开", text);
        Assert.False(summary.AllSucceeded);
    }

    [Fact]
    public void ExecutionTraceSummary_reports_all_succeeded_when_no_failures_and_all_completed()
    {
        var summary = new ExecutionTraceSummary
        {
            TotalSteps = 3,
            CompletedSteps = 3,
            FailedSteps = 0
        };

        Assert.Equal("共 3 步", summary.ToSummaryText());
        Assert.True(summary.AllSucceeded);
    }
}
