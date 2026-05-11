using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class TodoExecutionService
{
    private readonly object _sync = new();
    private readonly Action<ExecutionEvent> _publish;
    private bool _isRunning;
    private Guid _currentRunId = Guid.Empty;
    private CancellationTokenSource? _currentCancellation;
    private Task? _currentTask;

    public int MaxRetries { get; set; } = 1;

    public int RetryDelayMs { get; set; } = 2000;

    public TodoExecutionService(Action<ExecutionEvent>? publish = null)
    {
        _publish = publish ?? (_ => { });
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public Guid CurrentRunId
    {
        get
        {
            lock (_sync)
            {
                return _currentRunId;
            }
        }
    }

    public Guid StartSequentialRun(
        ChatMode mode,
        IReadOnlyList<TodoExecutionTask> tasks,
        Guid? runId = null)
    {
        var effectiveRunId = runId.HasValue && runId.Value != Guid.Empty ? runId.Value : Guid.NewGuid();
        CancellationTokenSource cancellation;
        lock (_sync)
        {
            if (_isRunning && _currentTask is { IsCompleted: false })
                return Guid.Empty;

            ResetCurrentRunLocked();
            _isRunning = true;
            _currentRunId = effectiveRunId;
            cancellation = new CancellationTokenSource();
            _currentCancellation = cancellation;
            _currentTask = Task.Run(async () =>
            {
                try
                {
                    await RunSequentialAsync(mode, tasks, effectiveRunId, cancellation.Token).ConfigureAwait(false);
                }
                finally
                {
                    lock (_sync)
                    {
                        if (_currentRunId == effectiveRunId)
                            ResetCurrentRunLocked();
                    }
                }
            });
        }

        return effectiveRunId;
    }

    public void CancelCurrentRun()
    {
        lock (_sync)
        {
            _currentCancellation?.Cancel();
        }
    }

    public async Task<TodoExecutionRunResult> RunSequentialAsync(
        ChatMode mode,
        IReadOnlyList<TodoExecutionTask> tasks,
        Guid? runId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRunId = runId.HasValue && runId.Value != Guid.Empty ? runId.Value : Guid.NewGuid();
        Publish(effectiveRunId, mode, ExecutionEventType.RunStarted, $"开始执行 {tasks.Count} 个步骤");

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            Publish(effectiveRunId, mode, ExecutionEventType.ToolCallStarted, task.Title,
                detail: "等待执行",
                stepIndex: GetStepIndex(task, i),
                pluginName: task.PluginName,
                functionName: task.FunctionName);
        }

        var completed = 0;
        var failed = 0;

        try
        {
            for (var i = 0; i < tasks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var task = tasks[i];
                var stepIndex = GetStepIndex(task, i);
                Publish(effectiveRunId, mode, ExecutionEventType.ToolCallStarted, task.Title,
                    detail: task.Detail,
                    stepIndex: stepIndex,
                    pluginName: task.PluginName,
                    functionName: task.FunctionName);

                var stepSucceeded = false;
                for (var attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (attempt > 0)
                        {
                            Publish(effectiveRunId, mode, ExecutionEventType.ToolCallStarted, task.Title,
                                detail: $"重试中（第{attempt}次）",
                                stepIndex: stepIndex,
                                pluginName: task.PluginName,
                                functionName: task.FunctionName);

                            var delay = Math.Max(0, RetryDelayMs) * attempt;
                            if (delay > 0)
                                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        }

                        var result = await ExecuteTaskAsync(task, cancellationToken).ConfigureAwait(false);
                        Publish(effectiveRunId, mode, ExecutionEventType.ToolCallCompleted, task.Title,
                            detail: string.IsNullOrWhiteSpace(result) ? "完成" : result,
                            stepIndex: stepIndex,
                            pluginName: task.PluginName,
                            functionName: task.FunctionName,
                            succeeded: true);
                        completed++;
                        stepSucceeded = true;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex))
                    {
                    }
                    catch (Exception ex)
                    {
                        Publish(effectiveRunId, mode, ExecutionEventType.ToolCallFailed, task.Title,
                            detail: ex.Message,
                            stepIndex: stepIndex,
                            pluginName: task.PluginName,
                            functionName: task.FunctionName,
                            succeeded: false);
                        failed++;
                        break;
                    }
                }

                if (!stepSucceeded && failed > 0)
                    break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var succeeded = failed == 0;
            Publish(effectiveRunId, mode, ExecutionEventType.RunCompleted, $"完成：{completed} 成功，{failed} 失败", succeeded: succeeded);
            return new TodoExecutionRunResult
            {
                RunId = effectiveRunId,
                Succeeded = succeeded,
                CompletedCount = completed,
                FailedCount = failed,
                Cancelled = false
            };
        }
        catch (OperationCanceledException)
        {
            Publish(effectiveRunId, mode, ExecutionEventType.RunFailed, "已取消", succeeded: false);
            return new TodoExecutionRunResult
            {
                RunId = effectiveRunId,
                Succeeded = false,
                CompletedCount = completed,
                FailedCount = failed,
                Cancelled = true
            };
        }
    }

    private static int GetStepIndex(TodoExecutionTask task, int listIndex)
    {
        return task.StepIndex > 0 ? task.StepIndex : listIndex + 1;
    }

    private static bool IsRetryable(Exception ex)
    {
        return !ex.Message.Contains("已达到最大重写次数", StringComparison.Ordinal);
    }

    private static Task<string?> ExecuteTaskAsync(TodoExecutionTask task, CancellationToken cancellationToken)
    {
        if (task.ExecuteAsync == null)
            return Task.FromResult<string?>("完成");

        return task.ExecuteAsync(cancellationToken);
    }

    private void Publish(
        Guid runId,
        ChatMode mode,
        ExecutionEventType eventType,
        string title,
        string? detail = null,
        int? stepIndex = null,
        string? pluginName = null,
        string? functionName = null,
        bool? succeeded = null)
    {
        _publish(new ExecutionEvent
        {
            RunId = runId,
            Mode = mode,
            EventType = eventType,
            Title = title,
            Detail = detail,
            StepIndex = stepIndex,
            PluginName = pluginName,
            FunctionName = functionName,
            Succeeded = succeeded
        });
    }

    private void ResetCurrentRunLocked()
    {
        _isRunning = false;
        _currentRunId = Guid.Empty;
        _currentCancellation?.Dispose();
        _currentCancellation = null;
        _currentTask = null;
    }
}

public sealed class TodoExecutionTask
{
    public int StepIndex { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string PluginName { get; init; } = string.Empty;

    public string FunctionName { get; init; } = string.Empty;

    public Func<CancellationToken, Task<string?>>? ExecuteAsync { get; init; }
}

public sealed class TodoExecutionRunResult
{
    public Guid RunId { get; init; }

    public bool Succeeded { get; init; }

    public int CompletedCount { get; init; }

    public int FailedCount { get; init; }

    public bool Cancelled { get; init; }
}
