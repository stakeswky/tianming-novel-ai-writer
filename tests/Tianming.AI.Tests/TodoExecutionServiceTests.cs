using System.Collections.Concurrent;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class TodoExecutionServiceTests
{
    [Fact]
    public async Task RunSequentialAsync_executes_tasks_in_order_and_publishes_run_summary()
    {
        var events = new List<ExecutionEvent>();
        var calls = new List<string>();
        var service = new TodoExecutionService(events.Add);

        var result = await service.RunSequentialAsync(
            ChatMode.Plan,
            [
                new TodoExecutionTask
                {
                    StepIndex = 1,
                    Title = "第一章",
                    Detail = "生成第一章",
                    PluginName = "WriterPlugin",
                    FunctionName = "GenerateChapter",
                    ExecuteAsync = _ =>
                    {
                        calls.Add("first");
                        return Task.FromResult<string?>("第一章完成");
                    }
                },
                new TodoExecutionTask
                {
                    StepIndex = 2,
                    Title = "第二章",
                    Detail = "生成第二章",
                    PluginName = "WriterPlugin",
                    FunctionName = "GenerateChapter",
                    ExecuteAsync = _ =>
                    {
                        calls.Add("second");
                        return Task.FromResult<string?>("第二章完成");
                    }
                }
            ],
            runId: Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.CompletedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(["first", "second"], calls);
        Assert.Equal(ExecutionEventType.RunStarted, events.First().EventType);
        Assert.Contains(events, item => item.EventType == ExecutionEventType.ToolCallCompleted && item.StepIndex == 1 && item.Detail == "第一章完成");
        var completed = Assert.Single(events, item => item.EventType == ExecutionEventType.RunCompleted);
        Assert.Equal("完成：2 成功，0 失败", completed.Title);
        Assert.Equal(ChatMode.Plan, completed.Mode);
    }

    [Fact]
    public async Task RunSequentialAsync_retries_transient_failure_before_completing_step()
    {
        var events = new List<ExecutionEvent>();
        var attempts = 0;
        var service = new TodoExecutionService(events.Add)
        {
            MaxRetries = 1,
            RetryDelayMs = 0
        };

        var result = await service.RunSequentialAsync(
            ChatMode.Agent,
            [
                new TodoExecutionTask
                {
                    StepIndex = 3,
                    Title = "重试章节",
                    ExecuteAsync = _ =>
                    {
                        attempts++;
                        if (attempts == 1)
                            throw new InvalidOperationException("temporary");
                        return Task.FromResult<string?>("重试后完成");
                    }
                }
            ],
            runId: Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, attempts);
        Assert.Contains(events, item => item.EventType == ExecutionEventType.ToolCallStarted && item.StepIndex == 3 && item.Detail == "重试中（第1次）");
        Assert.Contains(events, item => item.EventType == ExecutionEventType.ToolCallCompleted && item.StepIndex == 3 && item.Detail == "重试后完成");
    }

    [Fact]
    public async Task RunSequentialAsync_stops_after_non_retryable_failure()
    {
        var events = new List<ExecutionEvent>();
        var calls = new List<string>();
        var service = new TodoExecutionService(events.Add)
        {
            MaxRetries = 1,
            RetryDelayMs = 0
        };

        var result = await service.RunSequentialAsync(
            ChatMode.Agent,
            [
                new TodoExecutionTask
                {
                    StepIndex = 1,
                    Title = "失败章节",
                    ExecuteAsync = _ =>
                    {
                        calls.Add("failed");
                        throw new InvalidOperationException("已达到最大重写次数");
                    }
                },
                new TodoExecutionTask
                {
                    StepIndex = 2,
                    Title = "不应执行",
                    ExecuteAsync = _ =>
                    {
                        calls.Add("second");
                        return Task.FromResult<string?>("不应完成");
                    }
                }
            ],
            runId: Guid.Parse("33333333-3333-3333-3333-333333333333"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(["failed"], calls);
        Assert.Contains(events, item => item.EventType == ExecutionEventType.ToolCallFailed && item.StepIndex == 1 && item.Detail == "已达到最大重写次数");
        var completed = Assert.Single(events, item => item.EventType == ExecutionEventType.RunCompleted);
        Assert.Equal("完成：0 成功，1 失败", completed.Title);
        Assert.False(completed.Succeeded);
    }

    [Fact]
    public async Task StartSequentialRun_rejects_overlap_and_cancel_resets_running_state()
    {
        var events = new ConcurrentQueue<ExecutionEvent>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new TodoExecutionService(events.Enqueue);
        var runId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var startedRunId = service.StartSequentialRun(
            ChatMode.Agent,
            [
                new TodoExecutionTask
                {
                    StepIndex = 1,
                    Title = "长任务",
                    ExecuteAsync = async ct =>
                    {
                        started.SetResult();
                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                        return "不应完成";
                    }
                }
            ],
            runId);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var rejectedRunId = service.StartSequentialRun(ChatMode.Agent, [], Guid.NewGuid());
        service.CancelCurrentRun();

        await WaitUntilAsync(() =>
            events.Any(item => item.EventType == ExecutionEventType.RunFailed && item.Title == "已取消")
            && !service.IsRunning
            && service.CurrentRunId == Guid.Empty);

        Assert.Equal(runId, startedRunId);
        Assert.Equal(Guid.Empty, rejectedRunId);
        Assert.False(service.IsRunning);
        Assert.Equal(Guid.Empty, service.CurrentRunId);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }
}
