using System;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

/// <summary>单条样例对话气泡（M3 静态外观，M4.5 接真实 ConversationOrchestrator）。</summary>
public sealed class ConversationBubbleVm
{
    public ConversationRole Role          { get; set; } = ConversationRole.User;
    public string           Content       { get; set; } = string.Empty;
    public string?          ThinkingBlock { get; set; }
    public DateTime         Timestamp     { get; set; } = DateTime.Now;
}

public partial class RightConversationViewModel : ConversationPanelViewModel
{
    // 旧 PlaceholderText 保留向后兼容（不删除）
    public string PlaceholderText { get; } = "对话面板（M4.5 实装）";

    public RightConversationViewModel()
    {
    }
}
