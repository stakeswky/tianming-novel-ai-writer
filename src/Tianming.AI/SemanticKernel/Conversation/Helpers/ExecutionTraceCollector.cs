using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;

public sealed class ExecutionTraceCollector
{
    private readonly Dictionary<int, ToolCallRecord> _records = new();

    public void Clear()
    {
        _records.Clear();
    }

    public void Record(ExecutionEvent evt)
    {
        var stepIndex = evt.StepIndex ?? 0;
        if (stepIndex <= 0)
            return;

        switch (evt.EventType)
        {
            case ExecutionEventType.ToolCallStarted:
                _records[stepIndex] = new ToolCallRecord
                {
                    StepIndex = stepIndex,
                    PluginName = evt.PluginName ?? string.Empty,
                    FunctionName = evt.FunctionName ?? string.Empty,
                    Title = evt.Title,
                    Arguments = evt.Detail ?? string.Empty,
                    Status = ToolCallStatus.Running,
                    StartTime = evt.Timestamp
                };
                break;

            case ExecutionEventType.ToolCallCompleted:
                if (_records.TryGetValue(stepIndex, out var completed))
                {
                    completed.Status = ToolCallStatus.Completed;
                    completed.Result = evt.Detail ?? "完成";
                    completed.EndTime = evt.Timestamp;
                }
                break;

            case ExecutionEventType.ToolCallFailed:
                if (_records.TryGetValue(stepIndex, out var failed))
                {
                    failed.Status = ToolCallStatus.Failed;
                    failed.ErrorMessage = evt.Detail;
                    failed.EndTime = evt.Timestamp;
                }
                break;
        }
    }

    public IReadOnlyList<ToolCallRecord> GetCurrentTrace()
    {
        return _records.Values.OrderBy(record => record.StepIndex).ToList();
    }

    public ExecutionTraceSummary GetSummary()
    {
        var records = _records.Values.ToList();
        return new ExecutionTraceSummary
        {
            TotalSteps = records.Count,
            CompletedSteps = records.Count(record => record.Status == ToolCallStatus.Completed),
            FailedSteps = records.Count(record => record.Status == ToolCallStatus.Failed),
            TotalDurationSeconds = records.Where(record => record.DurationSeconds.HasValue).Sum(record => record.DurationSeconds!.Value),
            FailedStepSummaries = records
                .Where(record => record.Status == ToolCallStatus.Failed)
                .OrderBy(record => record.StepIndex)
                .Select(FormatFailedStepSummary)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList()
        };
    }

    private static string FormatFailedStepSummary(ToolCallRecord record)
    {
        var title = record.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = string.IsNullOrWhiteSpace(record.PluginName) && string.IsNullOrWhiteSpace(record.FunctionName)
                ? "未命名步骤"
                : $"{record.PluginName}.{record.FunctionName}";
        }

        title = title
            .Replace("调用 ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("失败 ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("取消 ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return $"步骤 {record.StepIndex}「{title}」: {FormatUserFriendlyReason(record.ErrorMessage)}";
    }

    private static string FormatUserFriendlyReason(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "未知错误";

        var message = raw.Trim();
        var shortMessage = Truncate(message, 160);

        if (message.Contains("用户取消", StringComparison.OrdinalIgnoreCase)
            || message.Contains("已取消", StringComparison.OrdinalIgnoreCase)
            || message.Contains("[用户取消", StringComparison.OrdinalIgnoreCase))
            return "用户取消";

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("超时", StringComparison.OrdinalIgnoreCase)
            || message.Contains("A task was canceled", StringComparison.OrdinalIgnoreCase)
            || message.Contains("The operation was canceled", StringComparison.OrdinalIgnoreCase))
            return $"超时/取消: {shortMessage}";

        if (message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return $"限流(请求过多): {shortMessage}";

        if (message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            return $"鉴权失败: {shortMessage}";

        if (message.Contains("json", StringComparison.OrdinalIgnoreCase)
            || message.Contains("解析", StringComparison.OrdinalIgnoreCase)
            || message.Contains("parse", StringComparison.OrdinalIgnoreCase))
            return $"解析失败: {shortMessage}";

        if (message.Contains("content policy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("safety", StringComparison.OrdinalIgnoreCase)
            || message.Contains("违规", StringComparison.OrdinalIgnoreCase)
            || message.Contains("拒绝", StringComparison.OrdinalIgnoreCase))
            return $"模型拒绝/安全策略: {shortMessage}";

        return shortMessage;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || maxLength <= 0)
            return string.Empty;

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
