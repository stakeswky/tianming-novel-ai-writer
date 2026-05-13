using System.Linq;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
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
}
