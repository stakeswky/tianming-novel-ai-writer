using System.Collections.ObjectModel;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Conversation;

public class BulkEmitterTests
{
    [Fact]
    public void Enqueue_does_not_apply_until_tick()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var scheduler = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(scheduler);
        emitter.Start(bubbles);

        emitter.Enqueue(new AnswerDelta("hello"));

        Assert.Empty(bubbles);
    }

    [Fact]
    public void Tick_flushes_pending_deltas_into_assistant_bubble()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var scheduler = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(scheduler);
        emitter.Start(bubbles);

        emitter.Enqueue(new AnswerDelta("hello "));
        emitter.Enqueue(new AnswerDelta("world"));
        scheduler.Tick();

        Assert.Single(bubbles);
        Assert.Equal(ConversationRole.Assistant, bubbles[0].Role);
        Assert.Equal("hello world", bubbles[0].Content);
    }

    [Fact]
    public void ThinkingDelta_accumulates_into_thinking_block()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var scheduler = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(scheduler);
        emitter.Start(bubbles);

        emitter.Enqueue(new ThinkingDelta("step 1\n"));
        emitter.Enqueue(new ThinkingDelta("step 2"));
        scheduler.Tick();

        Assert.Equal("step 1\nstep 2", bubbles[0].ThinkingBlock);
    }

    [Fact]
    public void Staged_tool_result_renders_tool_call_card_instead_of_plain_text()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var emitter = new BulkEmitter();

        emitter.Apply(bubbles, new ToolCallDelta("tool-1", "content_edit", "{\"chapterId\":\"ch-001\"}"));
        emitter.Apply(bubbles, new ToolResultDelta("tool-1", "已提议修改章节 ch-001。", "stg-abc123"));

        var bubble = Assert.Single(bubbles);
        var card = Assert.Single(bubble.ToolCalls);
        Assert.Equal("content_edit", card.ToolName);
        Assert.Equal("stg-abc123", card.StagedId);
        Assert.Equal(ToolCallState.Pending, card.State);
        Assert.DoesNotContain("[tool:", bubble.Content);
        Assert.DoesNotContain("[result:", bubble.Content);
    }

    [Fact]
    public void BulkEmitter_uses_structured_staged_id_not_text_scan()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var emitter = new BulkEmitter();

        emitter.Apply(bubbles, new ToolCallDelta("tool-1", "workspace_edit", "{\"relativePath\":\"README.md\"}"));
        emitter.Apply(bubbles, new ToolResultDelta("tool-1", "[result]", "stg-xyz"));

        var bubble = Assert.Single(bubbles);
        var card = Assert.Single(bubble.ToolCalls);
        Assert.Equal("stg-xyz", card.StagedId);
        Assert.Equal("workspace_edit", card.ToolName);
    }

    [Fact]
    public void BulkEmitter_does_not_extract_staged_id_from_text_when_structured_field_is_missing()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var emitter = new BulkEmitter();

        emitter.Apply(bubbles, new ToolCallDelta("tool-1", "content_edit", "{\"chapterId\":\"ch-001\"}"));
        emitter.Apply(bubbles, new ToolResultDelta("tool-1", "stg-fake-not-from-tool"));

        var bubble = Assert.Single(bubbles);
        Assert.Empty(bubble.ToolCalls);
        Assert.Contains("[result:tool-1] stg-fake-not-from-tool", bubble.Content);
    }

    [Fact]
    public void Stop_disposes_recurring_schedule()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var scheduler = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(scheduler);
        emitter.Start(bubbles);
        emitter.Enqueue(new AnswerDelta("x"));
        emitter.Stop();

        scheduler.Tick();
        Assert.Empty(bubbles);
    }
}
