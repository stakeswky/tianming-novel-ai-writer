using System.Collections.Generic;
using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Config;

public static class ConversationModeProfileCatalog
{
    private static readonly Dictionary<ChatMode, ConversationModeProfile> Profiles = new()
    {
        [ChatMode.Ask] = new ConversationModeProfile
        {
            Mode = ChatMode.Ask,
            RequiresExecutionEngine = false,
            DisplayPolicy = new ConversationDisplayPolicy
            {
                ShowAnalysis = true,
                AnalysisExpandedByDefault = false,
                DefaultPayloadTarget = null,
                HideRawContentInBubble = false
            },
            Description = "问答模式 - 直接对话，无执行"
        },
        [ChatMode.Agent] = new ConversationModeProfile
        {
            Mode = ChatMode.Agent,
            RequiresExecutionEngine = true,
            DisplayPolicy = new ConversationDisplayPolicy
            {
                ShowAnalysis = true,
                AnalysisExpandedByDefault = false,
                DefaultPayloadTarget = "ExecutionPanel",
                HideRawContentInBubble = false
            },
            Description = "代理模式 - 直接执行任务"
        },
        [ChatMode.Plan] = new ConversationModeProfile
        {
            Mode = ChatMode.Plan,
            RequiresExecutionEngine = true,
            DisplayPolicy = new ConversationDisplayPolicy
            {
                ShowAnalysis = true,
                AnalysisExpandedByDefault = false,
                DefaultPayloadTarget = "ExecutionPlan",
                HideRawContentInBubble = true
            },
            Description = "计划模式 - 生成计划后执行"
        }
    };

    public static IReadOnlyDictionary<ChatMode, ConversationModeProfile> All => Profiles;

    public static ConversationModeProfile GetProfile(ChatMode mode)
    {
        return Profiles.TryGetValue(mode, out var profile) ? profile : Profiles[ChatMode.Ask];
    }
}
