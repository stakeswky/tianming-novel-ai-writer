using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

/// <summary>单条样例对话气泡（M3 静态外观，M4.5 接真实 ConversationOrchestrator）。</summary>
public sealed class ConversationBubbleVm
{
    public ConversationRole Role          { get; init; } = ConversationRole.User;
    public string           Content       { get; init; } = string.Empty;
    public string?          ThinkingBlock { get; init; }
    public DateTime         Timestamp     { get; init; } = DateTime.Now;
}

public partial class RightConversationViewModel : ObservableObject
{
    // 旧 PlaceholderText 保留向后兼容（不删除）
    [ObservableProperty]
    private string _placeholderText = "对话面板（M4.5 实装）";

    // SegmentedTabs：Ask / Plan / Agent
    [ObservableProperty]
    private string _selectedMode = "ask";

    public ObservableCollection<SegmentItem> ModeSegments { get; } = new()
    {
        new SegmentItem("ask",   "Ask"),
        new SegmentItem("plan",  "Plan"),
        new SegmentItem("agent", "Agent"),
    };

    public ObservableCollection<ConversationBubbleVm> SampleBubbles { get; } = new();

    [ObservableProperty]
    private string _inputDraft = string.Empty;

    public RightConversationViewModel()
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role      = ConversationRole.User,
            Content   = "帮我写第 32 章后半段，主角与沈砚在雨夜的对峙，需要带出二人未解的旧账。",
            Timestamp = DateTime.Now.AddMinutes(-12),
        });
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role          = ConversationRole.Assistant,
            ThinkingBlock = "用户要求：主角 vs 沈砚 / 雨夜 / 旧账。\n需调取角色档案：沈砚的动机线，主角失忆背景。\n确认情绪基调：克制中带火药味。",
            Content       = "好的。第 32 章后半段提纲已生成：\n1. 雨幕拉开两人距离，沈砚先开口。\n2. 主角拒绝回应，转身欲走。\n3. 沈砚抛出关键线索（旧友失踪日期），主角僵住。\n4. 两人对峙在屋檐下，台词压低但句句带刺。\n\n要我按这个 beat 直接续写正文，还是先扩写每段大意？",
            Timestamp     = DateTime.Now.AddMinutes(-11),
        });
    }

    [RelayCommand]
    private void SelectMode(string? key)
    {
        if (!string.IsNullOrEmpty(key))
            SelectedMode = key;
    }

    [RelayCommand]
    private void Send()
    {
        // M3 阶段不接逻辑；M4.5 接 ConversationOrchestrator
        InputDraft = string.Empty;
    }
}
