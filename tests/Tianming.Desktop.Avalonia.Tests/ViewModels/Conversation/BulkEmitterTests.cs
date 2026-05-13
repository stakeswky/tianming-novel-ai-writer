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
