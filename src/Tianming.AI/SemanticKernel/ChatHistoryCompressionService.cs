using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel;

public enum PortableChatRole
{
    System,
    User,
    Assistant
}

public sealed record PortableChatMessage(PortableChatRole Role, string Content);

public sealed class ChatHistoryCompressionService
{
    private const int HybridRecentRounds = 12;
    private const int MinRecentRounds = 2;
    private const double CompressionTriggerPercent = 90;
    private const double PostCompressionTokenTargetPercent = 0.75;
    private const double SummaryBudgetPercentOfContextWindow = 0.06;
    private const int SummaryMinChars = 1200;
    private const int SummaryMaxChars = 12000;

    private readonly Func<string, string, CancellationToken, Task<string>> _oneShot;
    private readonly Func<string, int> _getModelContextWindow;

    public ChatHistoryCompressionService(
        Func<string, string, CancellationToken, Task<string>> oneShot,
        Func<string, int> getModelContextWindow)
    {
        _oneShot = oneShot ?? throw new ArgumentNullException(nameof(oneShot));
        _getModelContextWindow = getModelContextWindow ?? throw new ArgumentNullException(nameof(getModelContextWindow));
    }

    public (int EstimatedTokens, int ContextWindow, double UsagePercent) GetContextUsage(
        IReadOnlyList<PortableChatMessage> history,
        string modelId,
        string? additionalText = null,
        int? overrideContextWindow = null)
    {
        if (history == null)
            throw new ArgumentNullException(nameof(history));
        if (string.IsNullOrEmpty(modelId))
            return (0, 0, 0);

        var contextWindow = overrideContextWindow.HasValue && overrideContextWindow.Value > 0
            ? overrideContextWindow.Value
            : _getModelContextWindow(modelId);
        if (contextWindow <= 0)
            return (0, 0, 0);

        var estimatedTokens = EstimateSessionTokens(history);
        if (!string.IsNullOrEmpty(additionalText))
            estimatedTokens += EstimateTokenCount(additionalText);

        var usagePercent = Math.Min(100, (double)estimatedTokens / contextWindow * 100);
        return (estimatedTokens, contextWindow, usagePercent);
    }

    public async Task<(IReadOnlyList<PortableChatMessage> CompressedHistory, bool Compressed)> EnsureCompressionIfNeededAsync(
        IReadOnlyList<PortableChatMessage> history,
        string modelId,
        string? upcomingText,
        CancellationToken cancellationToken,
        int? overrideContextWindow = null,
        string? structuredMemory = null)
    {
        if (history == null)
            throw new ArgumentNullException(nameof(history));
        if (history.Count == 0)
            return (history, false);

        var (_, contextWindow, usagePercent) = GetContextUsage(history, modelId, upcomingText, overrideContextWindow);
        if (contextWindow <= 0 || usagePercent < CompressionTriggerPercent)
            return (history, false);

        var compressed = await CompressChatHistoryAsync(history, modelId, contextWindow, cancellationToken, structuredMemory);
        return (compressed, true);
    }

    public async Task<IReadOnlyList<PortableChatMessage>> CompressChatHistoryAsync(
        IReadOnlyList<PortableChatMessage> history,
        string modelId,
        int contextWindow,
        CancellationToken cancellationToken,
        string? structuredMemory = null)
    {
        if (history == null)
            throw new ArgumentNullException(nameof(history));
        if (history.Count == 0)
            return history;

        var systemPrompt = history.FirstOrDefault(message => message.Role == PortableChatRole.System)?.Content;
        var existingSummary = history
            .Where(message => message.Role == PortableChatRole.System)
            .Skip(1)
            .FirstOrDefault()
            ?.Content;

        var keptStartIndex = 0;
        var kept = new List<PortableChatMessage>();
        var rounds = 0;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];
            if (message.Role == PortableChatRole.System || string.IsNullOrWhiteSpace(message.Content))
                continue;

            kept.Add(message);
            if (message.Role == PortableChatRole.User)
            {
                rounds++;
                if (rounds >= HybridRecentRounds)
                {
                    keptStartIndex = i;
                    break;
                }
            }
        }

        kept.Reverse();

        var toSummarize = new List<PortableChatMessage>();
        if (kept.Count > 0)
        {
            for (var i = 0; i < keptStartIndex; i++)
            {
                var message = history[i];
                if (message.Role != PortableChatRole.System && !string.IsNullOrWhiteSpace(message.Content))
                    toSummarize.Add(message);
            }
        }

        if (toSummarize.Count == 0)
            return CompressChatHistoryHardTruncate(history, contextWindow);

        var summaryUserPrompt = BuildSummaryUserPrompt(existingSummary, structuredMemory, toSummarize);
        var rawSummary = await _oneShot(BuildNovelMemorySystemPrompt(), summaryUserPrompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawSummary)
            || rawSummary.StartsWith("[错误]", StringComparison.OrdinalIgnoreCase)
            || rawSummary.StartsWith("[已取消]", StringComparison.OrdinalIgnoreCase))
        {
            return CompressChatHistoryHardTruncate(history, contextWindow);
        }

        var cleanedSummary = rawSummary.Trim();
        var maxSummaryChars = GetSummaryMaxChars(contextWindow);
        if (cleanedSummary.Length > maxSummaryChars)
            cleanedSummary = cleanedSummary[..maxSummaryChars];

        var result = new List<PortableChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            result.Add(new PortableChatMessage(PortableChatRole.System, systemPrompt));
        result.Add(new PortableChatMessage(PortableChatRole.System, $"<rolling_memory>\n{cleanedSummary}\n</rolling_memory>"));
        result.AddRange(kept);

        var tokenLimit = (int)(contextWindow * PostCompressionTokenTargetPercent);
        while (EstimateSessionTokens(result) > tokenLimit)
        {
            var removeIndex = result.FindIndex(message => message.Role != PortableChatRole.System);
            if (removeIndex < 0)
                break;

            result.RemoveAt(removeIndex);
        }

        return result;
    }

    private static string BuildSummaryUserPrompt(
        string? existingSummary,
        string? structuredMemory,
        IReadOnlyList<PortableChatMessage> toSummarize)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(existingSummary))
        {
            builder.AppendLine("旧记忆块：");
            builder.AppendLine(existingSummary);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(structuredMemory))
        {
            builder.AppendLine("结构化记忆（实时抽取）：");
            builder.AppendLine(structuredMemory);
            builder.AppendLine();
        }

        builder.AppendLine("对话片段：");
        foreach (var message in toSummarize)
            builder.AppendLine($"{ToPromptRole(message.Role)}: {message.Content}");

        return builder.ToString();
    }

    private static IReadOnlyList<PortableChatMessage> CompressChatHistoryHardTruncate(
        IReadOnlyList<PortableChatMessage> history,
        int contextWindow)
    {
        var result = new List<PortableChatMessage>();
        var firstSystem = history.FirstOrDefault(message => message.Role == PortableChatRole.System);
        if (!string.IsNullOrWhiteSpace(firstSystem?.Content))
            result.Add(firstSystem);

        var secondSystem = history
            .Where(message => message.Role == PortableChatRole.System)
            .Skip(1)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(secondSystem?.Content))
        {
            var summaryContent = secondSystem.Content;
            var maxLength = Math.Max(1, (int)(contextWindow * 0.04));
            if (summaryContent.Length > maxLength)
                summaryContent = summaryContent[..maxLength] + "\n[已截断]";

            result.Add(new PortableChatMessage(PortableChatRole.System, summaryContent));
        }

        var kept = new List<PortableChatMessage>();
        var tokens = 0;
        var tokenLimit = (int)(contextWindow * PostCompressionTokenTargetPercent);
        var rounds = 0;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];
            if (message.Role == PortableChatRole.System || string.IsNullOrWhiteSpace(message.Content))
                continue;

            var tokenCount = EstimateTokenCount(message.Content);
            if (rounds >= MinRecentRounds && tokens + tokenCount > tokenLimit)
                break;

            tokens += tokenCount;
            kept.Add(message);
            if (message.Role == PortableChatRole.User)
                rounds++;
        }

        kept.Reverse();
        result.AddRange(kept);
        return result;
    }

    private static string ToPromptRole(PortableChatRole role)
    {
        return role switch
        {
            PortableChatRole.User => "User",
            PortableChatRole.Assistant => "Assistant",
            _ => "System"
        };
    }

    private static int EstimateSessionTokens(IReadOnlyList<PortableChatMessage> history)
    {
        return history.Sum(message => EstimateTokenCount(message.Content));
    }

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    private static int GetSummaryMaxChars(int contextWindow)
    {
        var chars = (int)Math.Round(contextWindow * SummaryBudgetPercentOfContextWindow);
        if (chars < SummaryMinChars)
            return SummaryMinChars;
        if (chars > SummaryMaxChars)
            return SummaryMaxChars;
        return chars;
    }

    private static string BuildNovelMemorySystemPrompt()
    {
        return "<role>Context Memory Compressor for novel writing. Merge given inputs into a new rolling memory block.</role>\n\n" +
               "<input_description>\n" +
               "- 旧记忆块：上一轮压缩的结果（如有）\n" +
               "- 结构化记忆：实时抽取的角色/剧情/世界状态（如有，优先保留）\n" +
               "- 对话片段：需要压缩的历史对话\n" +
               "</input_description>\n\n" +
               "<retention_priority>\n" +
               "1. MUST RETAIN: 角色当前状态、未回收伏笔、世界规则\n" +
               "2. IMPORTANT: 剧情里程碑、当前任务目标\n" +
               "3. COMPRESSIBLE: 日常对话、已完成任务、已回收伏笔\n" +
               "</retention_priority>\n\n" +
               "<output_rules>\n" +
               "1) 只输出纯文本，不要Markdown代码块，不要解释。\n" +
               "2) 不要包含推理过程。\n" +
               "3) 必须保持字段稳定，尽量不要改写已有事实，只做补充/合并/纠错。\n" +
               "4) 结构化记忆中的信息优先级最高，必须完整保留。\n" +
               "</output_rules>";
    }
}
