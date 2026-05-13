using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using TM.Services.Framework.AI.SemanticKernel.Conversation;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class ConversationPanelViewModel : ObservableObject
{
    [ObservableProperty] private string _selectedMode = "ask";
    [ObservableProperty] private string _inputDraft = string.Empty;
    [ObservableProperty] private bool _isStreaming;

    public ObservableCollection<SegmentItem> ModeSegments { get; } = new()
    {
        new SegmentItem("ask", "Ask"),
        new SegmentItem("plan", "Plan"),
        new SegmentItem("agent", "Agent"),
    };

    public ObservableCollection<ConversationBubbleVm> SampleBubbles { get; } = new();

    public ConversationPanelViewModel(bool seedSamples = true)
    {
        if (seedSamples)
            SeedSamples();
    }

    [RelayCommand]
    private void SelectMode(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            SelectedMode = key;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var input = InputDraft.Trim();
        if (input.Length == 0)
            return;

        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.User,
            Content = input,
            Timestamp = DateTime.Now,
        });

        InputDraft = string.Empty;
        IsStreaming = true;

        var assistant = new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            ThinkingBlock = BuildThinkingBlock(input),
            Content = BuildLocalDemoResponse(input),
            Timestamp = DateTime.Now,
        };

        await Task.Yield();
        SampleBubbles.Add(assistant);
        IsStreaming = false;
    }

    [RelayCommand]
    private void NewSession()
    {
        SampleBubbles.Clear();
        InputDraft = string.Empty;
        IsStreaming = false;
    }

    private string BuildThinkingBlock(string input)
    {
        var mode = FormatMode(SelectedMode);
        return $"{mode} 模式本地预览\n输入长度：{input.Length}\n下一步：接入 ConversationOrchestrator 流式输出";
    }

    private string BuildLocalDemoResponse(string input)
    {
        return FormatMode(SelectedMode) switch
        {
            "Plan" => $"Plan 预览：已收到「{input}」。\n1. 明确章节目标\n2. 拆解冲突与转折\n3. 输出可执行写作步骤",
            "Agent" => $"Agent 预览：已收到「{input}」。工具调用通道已预留，后续接 LookupData / ReadChapter / SearchReferences。",
            _ => $"Ask 预览：已收到「{input}」。右栏已可交互，后续接真实流式 AI 回复。",
        };
    }

    private static string FormatMode(string mode)
        => mode.Equals("plan", StringComparison.OrdinalIgnoreCase)
            ? "Plan"
            : mode.Equals("agent", StringComparison.OrdinalIgnoreCase)
                ? "Agent"
                : "Ask";

    private void SeedSamples()
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.User,
            Content = "帮我写第 32 章后半段，主角与沈砚在雨夜的对峙，需要带出二人未解的旧账。",
            Timestamp = DateTime.Now.AddMinutes(-12),
        });
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            ThinkingBlock = "用户要求：主角 vs 沈砚 / 雨夜 / 旧账。\n需调取角色档案：沈砚的动机线，主角失忆背景。\n确认情绪基调：克制中带火药味。",
            Content = "好的。第 32 章后半段提纲已生成：\n1. 雨幕拉开两人距离，沈砚先开口。\n2. 主角拒绝回应，转身欲走。\n3. 沈砚抛出关键线索（旧友失踪日期），主角僵住。\n4. 两人对峙在屋檐下，台词压低但句句带刺。\n\n要我按这个 beat 直接续写正文，还是先扩写每段大意？",
            Timestamp = DateTime.Now.AddMinutes(-11),
        });
    }
}

public sealed class BulkEmitter
{
    public void Apply(ObservableCollection<ConversationBubbleVm> bubbles, ChatStreamDelta delta)
    {
        if (bubbles == null)
            throw new ArgumentNullException(nameof(bubbles));

        var assistant = EnsureAssistantBubble(bubbles);
        switch (delta)
        {
            case ThinkingDelta thinking:
                assistant.ThinkingBlock = (assistant.ThinkingBlock ?? string.Empty) + thinking.Text;
                break;
            case AnswerDelta answer:
                assistant.Content += answer.Text;
                break;
            case ToolCallDelta tool:
                assistant.Content += $"\n[tool:{tool.ToolName}] {tool.ArgumentsJson}";
                break;
            case ToolResultDelta result:
                assistant.Content += $"\n[result:{result.ToolCallId}] {result.ResultText}";
                break;
            case PlanStepDelta step:
                assistant.Content += $"\n{step.Step.Index}. {step.Step.Title}";
                break;
        }
    }

    private static ConversationBubbleVm EnsureAssistantBubble(ObservableCollection<ConversationBubbleVm> bubbles)
    {
        if (bubbles.Count > 0 && bubbles[^1].Role == ConversationRole.Assistant)
            return bubbles[^1];

        var assistant = new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
        };
        bubbles.Add(assistant);
        return assistant;
    }
}
