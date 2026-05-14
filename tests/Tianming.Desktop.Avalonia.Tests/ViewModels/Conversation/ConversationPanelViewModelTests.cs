using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Conversation;

public class ConversationPanelViewModelTests
{
    [Fact]
    public void SelectMode_updates_selected_mode()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false);

        vm.SelectModeCommand.Execute("agent");

        Assert.Equal("agent", vm.SelectedMode);
    }

    [Fact]
    public async Task Send_adds_user_and_assistant_messages_then_clears_input()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false)
        {
            InputDraft = "帮我规划第 3 章"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.SampleBubbles.Count);
        Assert.Equal(ConversationRole.User, vm.SampleBubbles[0].Role);
        Assert.Equal("帮我规划第 3 章", vm.SampleBubbles[0].Content);
        Assert.Equal(ConversationRole.Assistant, vm.SampleBubbles[1].Role);
        Assert.Contains("Ask", vm.SampleBubbles[1].Content);
        Assert.Equal(string.Empty, vm.InputDraft);
    }

    [Fact]
    public async Task Send_ignores_blank_input()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false)
        {
            InputDraft = "   "
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Empty(vm.SampleBubbles);
    }

    [Fact]
    public async Task Plan_mode_adds_plan_oriented_response()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false)
        {
            SelectedMode = "plan",
            InputDraft = "拆解章节"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Contains("Plan", vm.SampleBubbles.Last().Content);
    }

    [Fact]
    public async Task NewSession_clears_messages_and_input()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false)
        {
            InputDraft = "hello"
        };
        await vm.SendCommand.ExecuteAsync(null);

        vm.NewSessionCommand.Execute(null);

        Assert.Empty(vm.SampleBubbles);
        Assert.Equal(string.Empty, vm.InputDraft);
    }

    [Fact]
    public void BulkEmitter_applies_thinking_and_answer_deltas_to_assistant_bubble()
    {
        var vm = new ConversationPanelViewModel(seedSamples: false);
        var emitter = new BulkEmitter();

        emitter.Apply(vm.SampleBubbles, new ThinkingDelta("分析"));
        emitter.Apply(vm.SampleBubbles, new AnswerDelta("正文"));

        var bubble = Assert.Single(vm.SampleBubbles);
        Assert.Equal(ConversationRole.Assistant, bubble.Role);
        Assert.Equal("分析", bubble.ThinkingBlock);
        Assert.Equal("正文", bubble.Content);
    }

    [Fact]
    public async Task SendAsync_with_orchestrator_streams_deltas_into_bubbles()
    {
        var scheduler = new FakeDispatcherScheduler();
        var orchestrator = new StubOrchestrator
        {
            StreamFunc = (_, _) => AsyncDeltas(
                new ThinkingDelta("thinking..."),
                new AnswerDelta("hi"),
                new AnswerDelta(" there"))
        };
        var sessionStore = new StubSessionStore();
        var vm = new ConversationPanelViewModel(orchestrator, sessionStore, scheduler, seedSamples: false)
        {
            InputDraft = "hello"
        };

        await vm.SendCommand.ExecuteAsync(null);
        scheduler.Tick();

        Assert.Equal(2, vm.SampleBubbles.Count);
        Assert.Equal(ConversationRole.User, vm.SampleBubbles[0].Role);
        Assert.Equal("hello", vm.SampleBubbles[0].Content);
        Assert.Equal(ConversationRole.Assistant, vm.SampleBubbles[1].Role);
        Assert.Equal("hi there", vm.SampleBubbles[1].Content);
        Assert.Equal("thinking...", vm.SampleBubbles[1].ThinkingBlock);
    }

    [Fact]
    public async Task SendAsync_when_orchestrator_throws_renders_visible_error_bubble()
    {
        var scheduler = new FakeDispatcherScheduler();
        var orchestrator = new StubOrchestrator
        {
            StartSessionAsyncFunc = _ => Task.FromException<ConversationSession>(
                new InvalidOperationException("boom"))
        };
        var vm = new ConversationPanelViewModel(orchestrator, new StubSessionStore(), scheduler, seedSamples: false)
        {
            InputDraft = "hello"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.SampleBubbles.Count);
        Assert.Equal(ConversationRole.Assistant, vm.SampleBubbles[^1].Role);
        Assert.Contains("InvalidOperationException", vm.SampleBubbles[^1].Content);
        Assert.Contains("boom", vm.SampleBubbles[^1].Content);
        Assert.DoesNotContain("预览", vm.SampleBubbles[^1].Content);
    }

    [Fact]
    public async Task InputDraft_with_at_query_uses_injected_reference_provider_results()
    {
        var vm = new ConversationPanelViewModel(
            seedSamples: false,
            referenceSuggestionSource: new StubReferenceSuggestionSource(
            [
                new ReferenceItemVm { Id = "world-jiuzhou", Name = "九州大陆", Category = "World" },
                new ReferenceItemVm { Id = "char-jiu", Name = "九璃", Category = "Character" },
            ]))
        {
            InputDraft = "hello @九"
        };

        await WaitForAsync(() => vm.ReferenceCandidates.Count == 2);

        Assert.True(vm.IsReferencePopupOpen);
        Assert.Equal(["九州大陆", "九璃"], vm.ReferenceCandidates.Select(item => item.Name));
    }

    private static async IAsyncEnumerable<ChatStreamDelta> AsyncDeltas(params ChatStreamDelta[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        Assert.True(condition(), "Timed out waiting for async condition.");
    }

    private sealed class StubOrchestrator : IConversationOrchestrator
    {
        public Func<ConversationSession, string, IAsyncEnumerable<ChatStreamDelta>> StreamFunc { get; set; } = default!;
        public Func<TM.Framework.UI.Workspace.RightPanel.Modes.ChatMode, Task<ConversationSession>>? StartSessionAsyncFunc { get; set; }

        public Task<ConversationSession> StartSessionAsync(TM.Framework.UI.Workspace.RightPanel.Modes.ChatMode mode, string? sessionId = null, CancellationToken ct = default)
            => StartSessionAsyncFunc?.Invoke(mode)
               ?? Task.FromResult(new ConversationSession { Mode = mode });

        public IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession session, string userInput, CancellationToken ct = default)
            => StreamFunc(session, userInput);

        public Task PersistAsync(ConversationSession session, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubSessionStore : IFileSessionStore
    {
        public Task SaveSessionAsync(ConversationSession session, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<ConversationSession?> LoadSessionAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<ConversationSession?>(null);

        public Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionSummary>>(Array.Empty<SessionSummary>());

        public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubReferenceSuggestionSource(IReadOnlyList<ReferenceItemVm> results) : IReferenceSuggestionSource
    {
        public Task<IReadOnlyList<ReferenceItemVm>> SuggestAsync(string query, CancellationToken ct = default)
            => Task.FromResult(results);
    }
}
