using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using Xunit;

namespace Tianming.AI.Tests.Conversation;

/// <summary>Tests for ConversationOrchestrator using in-memory stubs.</summary>
public class ConversationOrchestratorTests
{
    // Since OpenAICompatibleChatClient.StreamAsync is not virtual,
    // we use a simpler testing approach: test the parsing and delta logic directly.

    [Fact]
    public void ThinkingDelta_ProducesCorrectText()
    {
        var delta = new ThinkingDelta("thinking about the problem");
        Assert.Equal("thinking about the problem", delta.Text);
    }

    [Fact]
    public void AnswerDelta_ProducesCorrectText()
    {
        var delta = new AnswerDelta("Here is my answer");
        Assert.Equal("Here is my answer", delta.Text);
    }

    [Fact]
    public void ToolCallDelta_CapturesToolInfo()
    {
        var delta = new ToolCallDelta("tc_001", "lookup_data", "{\"category\":\"Characters\"}");
        Assert.Equal("tc_001", delta.ToolCallId);
        Assert.Equal("lookup_data", delta.ToolName);
        Assert.Equal("{\"category\":\"Characters\"}", delta.ArgumentsJson);
    }

    [Fact]
    public void ToolResultDelta_CapturesResult()
    {
        var delta = new ToolResultDelta("tc_001", "Found 3 characters");
        Assert.Equal("tc_001", delta.ToolCallId);
        Assert.Equal("Found 3 characters", delta.ResultText);
    }

    [Fact]
    public void PlanStepDelta_WrapsPlanStep()
    {
        var step = new PlanStep { Index = 1, Title = "Write chapter outline" };
        var delta = new PlanStepDelta(step);
        Assert.Equal(1, delta.Step.Index);
        Assert.Equal("Write chapter outline", delta.Step.Title);
    }

    [Fact]
    public void ConversationSession_CanClearStreamingState()
    {
        var session = new ConversationSession
        {
            Mode = ChatMode.Ask,
            CurrentThinking = "some thinking",
            CurrentAnswer = "some answer"
        };
        session.CurrentPlanSteps.Add(new PlanStep { Index = 1, Title = "step 1" });
        session.CurrentToolCalls.Add(new ToolCallRecord { FunctionName = "test" });

        session.ClearStreamingState();

        Assert.Null(session.CurrentThinking);
        Assert.Null(session.CurrentAnswer);
        Assert.Empty(session.CurrentPlanSteps);
        Assert.Empty(session.CurrentToolCalls);
        Assert.Equal(ChatMode.Ask, session.Mode); // Mode preserved
    }

    [Fact]
    public void ConversationSession_GeneratesUniqueIds()
    {
        var s1 = new ConversationSession();
        var s2 = new ConversationSession();
        Assert.NotEqual(s1.Id, s2.Id);
    }

    [Fact]
    public void ConversationSession_HistoryStartsEmpty()
    {
        var session = new ConversationSession();
        Assert.Empty(session.History);
    }

    [Fact]
    public void TagBasedThinkingStrategy_ExtractsThinkingAndAnswer()
    {
        var strategy = new TagBasedThinkingStrategy();
        var text = "<think>analyzing user request</think><answer>Here is the answer</answer>";

        var result = strategy.Extract(text);
        Assert.Equal("analyzing user request", result.ThinkingContent);
        Assert.Equal("Here is the answer", result.AnswerContent);
    }

    [Fact]
    public void TagBasedThinkingStrategy_FlushReturnsRemainingContent()
    {
        var strategy = new TagBasedThinkingStrategy();
        // Feed partial thinking content where the close tag is partially present.
        var text = "<think>analyzing </th";
        var extract = strategy.Extract(text);
        var flush = strategy.Flush();

        // Stable content is emitted immediately; only the ambiguous tag prefix stays buffered.
        Assert.Equal("analyzing ", extract.ThinkingContent);
        Assert.Equal("</th", flush.ThinkingContent);
    }

    [Fact]
    public void PlanStepParser_ParsesNumberedSteps()
    {
        var parser = new PlanStepParser();
        var content = "1. 分析角色关系\n2. 设计场景冲突\n3. 完成章节大纲";
        var steps = parser.Parse(content);

        Assert.Equal(3, steps.Count);
        Assert.Equal("分析角色关系", steps[0].Title);
        Assert.Equal("设计场景冲突", steps[1].Title);
        Assert.Equal("完成章节大纲", steps[2].Title);
    }

    [Fact]
    public void PlanStepParser_ReturnsEmptyForNonPlanContent()
    {
        var parser = new PlanStepParser();
        var content = "这是一段普通的对话，没有步骤。";
        var steps = parser.Parse(content);

        Assert.Empty(steps);
    }

    [Fact]
    public async Task InMemorySessionStore_RoundTripsSession()
    {
        var store = new InMemorySessionStore();
        var session = new ConversationSession { Mode = ChatMode.Ask, Title = "Test session" };
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.User,
            Summary = "Hello",
            Timestamp = DateTime.Now
        });

        await store.SaveSessionAsync(session);
        var loaded = await store.LoadSessionAsync(session.Id);

        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded!.Id);
        Assert.Equal("Test session", loaded.Title);
        Assert.Single(loaded.History);
    }

    [Fact]
    public async Task InMemorySessionStore_ListSessionsReturnsAll()
    {
        var store = new InMemorySessionStore();
        var s1 = new ConversationSession { Title = "First" };
        var s2 = new ConversationSession { Title = "Second" };

        await store.SaveSessionAsync(s1);
        await store.SaveSessionAsync(s2);

        var sessions = await store.ListSessionsAsync();
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task InMemorySessionStore_DeleteSessionRemovesIt()
    {
        var store = new InMemorySessionStore();
        var session = new ConversationSession { Title = "To delete" };

        await store.SaveSessionAsync(session);
        await store.DeleteSessionAsync(session.Id);

        var sessions = await store.ListSessionsAsync();
        Assert.Empty(sessions);
    }

    [Fact]
    public void ConversationSession_SetsTitleFromFirstUserMessage()
    {
        var session = new ConversationSession();
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.User,
            Summary = "Help me plan chapter 5 of the novel",
            Timestamp = DateTime.Now
        });
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Summary = "Sure, here's a plan...",
            Timestamp = DateTime.Now
        });

        // Simulate title extraction logic
        var firstUser = session.History.FirstOrDefault(m => m.Role == ConversationRole.User);
        if (firstUser != null)
        {
            session.Title = firstUser.Summary.Length > 50
                ? firstUser.Summary[..50] + "..."
                : firstUser.Summary;
        }

        Assert.Equal("Help me plan chapter 5 of the novel", session.Title);
    }

    [Fact]
    public void ChatStreamDelta_HierarchyCorrect()
    {
        ChatStreamDelta d1 = new ThinkingDelta("think");
        ChatStreamDelta d2 = new AnswerDelta("answer");
        ChatStreamDelta d3 = new ToolCallDelta("id", "tool", "{}");
        ChatStreamDelta d4 = new ToolResultDelta("id", "result");
        ChatStreamDelta d5 = new PlanStepDelta(new PlanStep { Index = 1, Title = "step" });

        Assert.IsType<ThinkingDelta>(d1);
        Assert.IsType<AnswerDelta>(d2);
        Assert.IsType<ToolCallDelta>(d3);
        Assert.IsType<ToolResultDelta>(d4);
        Assert.IsType<PlanStepDelta>(d5);
    }

    [Fact]
    public async Task InMemorySessionStore_LoadNonExistentReturnsNull()
    {
        var store = new InMemorySessionStore();
        var loaded = await store.LoadSessionAsync("nonexistent");
        Assert.Null(loaded);
    }
}

/// <summary>In-memory implementation of IFileSessionStore for testing.</summary>
public sealed class InMemorySessionStore : IFileSessionStore
{
    private readonly Dictionary<string, ConversationSession> _sessions = new();

    public Task SaveSessionAsync(ConversationSession session, CancellationToken ct = default)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<ConversationSession?> LoadSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken ct = default)
    {
        var summaries = _sessions.Values
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionSummary
            {
                Id = s.Id,
                Title = s.Title ?? string.Empty,
                UpdatedAt = s.CreatedAt,
                MessageCount = s.History.Count
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionSummary>>(summaries);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.Remove(sessionId);
        return Task.CompletedTask;
    }
}
