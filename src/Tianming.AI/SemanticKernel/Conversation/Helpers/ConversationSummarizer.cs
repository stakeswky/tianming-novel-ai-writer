using System;
using System.Collections.Generic;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers
{
    public static class ConversationSummarizer
    {
        public static string ForPlanGenerated(int stepCount)
        {
            return $"已生成创作计划，共 {stepCount} 个步骤。\n请在左侧「执行计划」面板查看详细步骤，确认后点击「开始执行」。";
        }

        public static string ForPlanParseFailed()
        {
            return "⚠️ 计划解析失败，请重新描述您的需求。";
        }

        public static string ForExecutionCompleted(string? chapterTitle, string? chapterId, string traceText)
        {
            if (!string.IsNullOrEmpty(chapterTitle))
                return $"「{chapterTitle}」生成完毕，{traceText}。";

            if (!string.IsNullOrEmpty(chapterId))
                return $"章节 {chapterId} 生成完毕，{traceText}。";

            return $"创作任务执行完毕，{traceText}。";
        }

        public static string ForExecutionCompleted(string? chapterTitle, string? chapterId, ExecutionTraceSummary summary)
        {
            return ForExecutionCompleted(chapterTitle, chapterId, summary.ToSummaryText());
        }

        public static string ForExecutionNotStarted()
        {
            return "当前已有任务在执行中，请稍后再试。";
        }

        public static string ForExecutionCancelled()
        {
            return "创作任务已取消。";
        }
    }

    public class ExecutionTraceSummary
    {
        public int TotalSteps { get; init; }
        public int CompletedSteps { get; init; }
        public int FailedSteps { get; init; }
        public double TotalDurationSeconds { get; init; }

        public IReadOnlyList<string> FailedStepSummaries { get; init; } = Array.Empty<string>();

        public bool AllSucceeded => FailedSteps == 0 && CompletedSteps == TotalSteps;

        public string ToSummaryText()
        {
            var text = $"共 {TotalSteps} 步";
            if (FailedSteps > 0)
                text += $"（{FailedSteps} 失败）";
            if (TotalDurationSeconds > 0)
                text += $"，耗时 {TotalDurationSeconds:F1}s";

            if (FailedSteps > 0 && FailedStepSummaries.Count > 0)
            {
                const int max = 3;
                var shown = Math.Min(max, FailedStepSummaries.Count);
                text += "\n失败原因：";
                for (var i = 0; i < shown; i++)
                    text += $"\n- {FailedStepSummaries[i]}";

                if (FailedStepSummaries.Count > max)
                    text += $"\n- 还有 {FailedStepSummaries.Count - max} 个失败未展开";
            }

            return text;
        }
    }
}
