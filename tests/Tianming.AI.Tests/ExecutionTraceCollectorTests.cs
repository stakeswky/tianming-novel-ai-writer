using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using Xunit;

namespace Tianming.AI.Tests;

public class ExecutionTraceCollectorTests
{
    [Fact]
    public void Records_started_completed_and_failed_tool_calls_in_step_order()
    {
        var collector = new ExecutionTraceCollector();
        var runId = Guid.NewGuid();

        collector.Record(new ExecutionEvent
        {
            RunId = runId,
            EventType = ExecutionEventType.ToolCallStarted,
            StepIndex = 2,
            PluginName = "Writer",
            FunctionName = "Save",
            Title = "调用 保存章节",
            Detail = "{chapter:2}"
        });
        collector.Record(new ExecutionEvent
        {
            RunId = runId,
            EventType = ExecutionEventType.ToolCallStarted,
            StepIndex = 1,
            PluginName = "Writer",
            FunctionName = "Draft",
            Title = "起草章节"
        });
        collector.Record(new ExecutionEvent
        {
            RunId = runId,
            EventType = ExecutionEventType.ToolCallCompleted,
            StepIndex = 1,
            Detail = "完成"
        });
        collector.Record(new ExecutionEvent
        {
            RunId = runId,
            EventType = ExecutionEventType.ToolCallFailed,
            StepIndex = 2,
            Detail = "429 Too Many Requests"
        });

        var trace = collector.GetCurrentTrace();
        var summary = collector.GetSummary();

        Assert.Equal([1, 2], trace.Select(record => record.StepIndex).ToArray());
        Assert.Equal(ToolCallStatus.Completed, trace[0].Status);
        Assert.Equal(ToolCallStatus.Failed, trace[1].Status);
        Assert.Equal("完成", trace[0].Result);
        Assert.Equal("{chapter:2}", trace[1].Arguments);
        Assert.Equal(2, summary.TotalSteps);
        Assert.Equal(1, summary.CompletedSteps);
        Assert.Equal(1, summary.FailedSteps);
        Assert.Contains("限流(请求过多)", summary.FailedStepSummaries[0]);
        Assert.Contains("保存章节", summary.FailedStepSummaries[0]);
    }

    [Theory]
    [InlineData("用户取消执行", "用户取消")]
    [InlineData("The operation was canceled after timeout", "超时/取消")]
    [InlineData("401 Unauthorized", "鉴权失败")]
    [InlineData("JSON parse failed", "解析失败")]
    [InlineData("content policy safety refusal", "模型拒绝/安全策略")]
    public void Summary_formats_common_failure_reasons(string raw, string expected)
    {
        var collector = new ExecutionTraceCollector();
        collector.Record(new ExecutionEvent
        {
            EventType = ExecutionEventType.ToolCallStarted,
            StepIndex = 1,
            Title = "调用 工具"
        });
        collector.Record(new ExecutionEvent
        {
            EventType = ExecutionEventType.ToolCallFailed,
            StepIndex = 1,
            Detail = raw
        });

        var failure = Assert.Single(collector.GetSummary().FailedStepSummaries);

        Assert.Contains(expected, failure);
    }

    [Fact]
    public void Ignores_events_without_positive_step_index_and_unknown_completion()
    {
        var collector = new ExecutionTraceCollector();

        collector.Record(new ExecutionEvent { EventType = ExecutionEventType.ToolCallStarted, StepIndex = 0 });
        collector.Record(new ExecutionEvent { EventType = ExecutionEventType.ToolCallCompleted, StepIndex = 9 });

        Assert.Empty(collector.GetCurrentTrace());
        Assert.Equal("共 0 步", collector.GetSummary().ToSummaryText());
    }
}
