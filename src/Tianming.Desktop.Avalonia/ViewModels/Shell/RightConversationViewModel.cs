using System;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

/// <summary>单条样例对话气泡（M3 静态外观，M4.5 接真实 ConversationOrchestrator）。</summary>
public sealed class ConversationBubbleVm
{
    public ConversationRole Role          { get; set; } = ConversationRole.User;
    public string           Content       { get; set; } = string.Empty;
    public string?          ThinkingBlock { get; set; }
    public DateTime         Timestamp     { get; set; } = DateTime.Now;
    public System.Collections.ObjectModel.ObservableCollection<ConversationToolCallVm> ToolCalls { get; } = new();
}

public sealed partial class ConversationToolCallVm : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string ToolCallId { get; set; } = string.Empty;
    public string StagedId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsPreview { get; set; } = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private ToolCallState _state = ToolCallState.Pending;
}

public partial class RightConversationViewModel : ConversationPanelViewModel
{
    // 旧 PlaceholderText 保留向后兼容（不删除）
    public string PlaceholderText { get; } = "对话面板（M4.5 实装）";

    public RightConversationViewModel(
        IConversationOrchestrator orchestrator,
        IFileSessionStore sessionStore,
        IDispatcherScheduler scheduler,
        IReferenceSuggestionSource? referenceSuggestionSource = null,
        IStagedChangeApprover? approver = null)
        : base(orchestrator, sessionStore, scheduler, referenceSuggestionSource, approver, seedSamples: true)
    {
    }
}
