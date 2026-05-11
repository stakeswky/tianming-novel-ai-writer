using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class ChatHistoryCompressionServiceTests
{
    [Fact]
    public void GetContextUsage_counts_history_and_upcoming_text()
    {
        var service = new ChatHistoryCompressionService(
            (_, _, _) => Task.FromResult("unused"),
            _ => 20);
        var history = new[]
        {
            new PortableChatMessage(PortableChatRole.System, "系统提示"),
            new PortableChatMessage(PortableChatRole.User, new string('你', 40))
        };

        var usage = service.GetContextUsage(history, "model-a", new string('好', 40));

        Assert.Equal(21, usage.EstimatedTokens);
        Assert.Equal(20, usage.ContextWindow);
        Assert.Equal(100, usage.UsagePercent);
    }

    [Fact]
    public async Task EnsureCompressionIfNeededAsync_skips_when_usage_is_below_threshold()
    {
        var called = false;
        var service = new ChatHistoryCompressionService(
            (_, _, _) =>
            {
                called = true;
                return Task.FromResult("summary");
            },
            _ => 1000);

        var history = new[]
        {
            new PortableChatMessage(PortableChatRole.System, "系统提示"),
            new PortableChatMessage(PortableChatRole.User, "短消息")
        };

        var result = await service.EnsureCompressionIfNeededAsync(history, "model-a", null, CancellationToken.None);

        Assert.False(result.Compressed);
        Assert.Same(history, result.CompressedHistory);
        Assert.False(called);
    }

    [Fact]
    public async Task EnsureCompressionIfNeededAsync_summarizes_old_messages_and_keeps_recent_rounds()
    {
        string? systemPrompt = null;
        string? userPrompt = null;
        var service = new ChatHistoryCompressionService(
            (system, user, _) =>
            {
                systemPrompt = system;
                userPrompt = user;
                return Task.FromResult("人物状态：林青入城\n当前卷目标：打开主线");
            },
            _ => 80);
        var history = BuildConversation(rounds: 14);

        var result = await service.EnsureCompressionIfNeededAsync(
            history,
            "model-a",
            new string('追', 80),
            CancellationToken.None,
            structuredMemory: "结构化记忆：银钥匙未回收");

        Assert.True(result.Compressed);
        Assert.NotNull(systemPrompt);
        Assert.Contains("Context Memory Compressor", systemPrompt);
        Assert.Contains("结构化记忆：银钥匙未回收", userPrompt);
        Assert.Contains("User: 用户第1轮", userPrompt);
        Assert.Contains("Assistant: 助手第1轮", userPrompt);
        Assert.DoesNotContain(result.CompressedHistory, message => message.Content.Contains("用户第1轮"));
        Assert.Equal(PortableChatRole.System, result.CompressedHistory[0].Role);
        Assert.Contains("<rolling_memory>", result.CompressedHistory[1].Content);
        Assert.Contains("林青入城", result.CompressedHistory[1].Content);
        Assert.Contains(result.CompressedHistory, message => message.Content.Contains("用户第14轮"));
    }

    [Fact]
    public async Task EnsureCompressionIfNeededAsync_hard_truncates_when_summary_fails()
    {
        var service = new ChatHistoryCompressionService(
            (_, _, _) => Task.FromResult("[错误] timeout"),
            _ => 80);
        var history = BuildConversation(rounds: 10);

        var result = await service.EnsureCompressionIfNeededAsync(
            history,
            "model-a",
            new string('追', 80),
            CancellationToken.None);

        Assert.True(result.Compressed);
        Assert.Equal(PortableChatRole.System, result.CompressedHistory[0].Role);
        Assert.DoesNotContain(result.CompressedHistory, message => message.Content.Contains("用户第1轮"));
        Assert.Contains(result.CompressedHistory, message => message.Content.Contains("用户第10轮"));
    }

    private static IReadOnlyList<PortableChatMessage> BuildConversation(int rounds)
    {
        var messages = new List<PortableChatMessage>
        {
            new(PortableChatRole.System, "系统提示"),
            new(PortableChatRole.System, "<rolling_memory>旧记忆</rolling_memory>")
        };

        for (var i = 1; i <= rounds; i++)
        {
            messages.Add(new PortableChatMessage(PortableChatRole.User, $"用户第{i}轮 " + new string('问', 20)));
            messages.Add(new PortableChatMessage(PortableChatRole.Assistant, $"助手第{i}轮 " + new string('答', 20)));
        }

        return messages;
    }
}
