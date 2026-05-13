using System;
using System.Collections.Generic;
using TM.Framework.Common.Helpers.Id;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

/// <summary>
/// 单次对话会话实体。由 ConversationOrchestrator 创建/恢复，
/// 包含历史消息列表及当前流式输出状态。
/// </summary>
public sealed class ConversationSession
{
    public string Id { get; } = ShortIdGenerator.New("S");
    public ChatMode Mode { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>持久化的历史消息列表（user + assistant）。</summary>
    public List<ConversationMessage> History { get; } = new();

    // ---- 当前流式输出状态（每轮 SendAsync 开始前清空，结束后合并到 History） ----

    public string? CurrentThinking { get; set; }
    public string? CurrentAnswer { get; set; }
    public List<PlanStep> CurrentPlanSteps { get; } = new();
    public List<ToolCallRecord> CurrentToolCalls { get; } = new();

    public void ClearStreamingState()
    {
        CurrentThinking = null;
        CurrentAnswer = null;
        CurrentPlanSteps.Clear();
        CurrentToolCalls.Clear();
    }
}
